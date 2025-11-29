using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamsService.Data;
using ExamsService.DTOs;
using ExamsService.Models;
using System.Security.Claims;
using API_ThiTracNghiem.Services;
using API_ThiTracNghiem.Middleware;
using ExamsService.Services;
using Microsoft.Extensions.Configuration;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace ExamsService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExamsController : ControllerBase
    {
        private readonly ExamsDbContext _context;
        private readonly IUserSyncService _userSyncService;
        private readonly ILogger<ExamsController> _logger;
        private readonly IExamProgressCache _progressCache;
        private readonly IConfiguration _config;
        private readonly Cloudinary _cloudinary;

        public ExamsController(ExamsDbContext context, IUserSyncService userSyncService, ILogger<ExamsController> logger, IExamProgressCache progressCache, IConfiguration config)
        {
            _context = context;
            _userSyncService = userSyncService;
            _logger = logger;
            _progressCache = progressCache;
            _config = config;
            
            // ✅ Initialize Cloudinary for exam image uploads
            try
            {
                var cloud = config["Cloudinary:CloudName"];
                var key = config["Cloudinary:ApiKey"];
                var secret = config["Cloudinary:ApiSecret"];
                
                if (string.IsNullOrEmpty(cloud) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(secret))
                {
                    _logger.LogError("❌ [ExamsService] Cloudinary config is missing! CloudName: {Cloud}, ApiKey: {Key}, ApiSecret: {Secret}", 
                        string.IsNullOrEmpty(cloud) ? "NULL" : "SET", 
                        string.IsNullOrEmpty(key) ? "NULL" : "SET", 
                        string.IsNullOrEmpty(secret) ? "NULL" : "SET");
                    _cloudinary = null!;
                }
                else
                {
                    _cloudinary = new Cloudinary(new Account(cloud, key, secret)) { Api = { Secure = true } };
                    _logger.LogInformation("✅ [ExamsService] Cloudinary initialized successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [ExamsService] Failed to initialize Cloudinary");
                _cloudinary = null!;
            }
        }

        private async Task<ExamAttempt?> ValidateAttemptAsync(int examId, int attemptId, int userId)
        {
            var attempt = await _context.ExamAttempts
                .Include(ea => ea.Exam)
                .FirstOrDefaultAsync(ea => ea.ExamAttemptId == attemptId && ea.ExamId == examId && ea.UserId == userId);
            if (attempt == null) return null;
            if (attempt.Status != "InProgress") return null;
            return attempt;
        }

        private TimeSpan GetAttemptTtlMinutes() => TimeSpan.FromMinutes(_config.GetSection("Redis").GetValue<int>("AttemptTtlMinutes", 180));

        private TimeSpan ComputeDynamicTtl(ExamAttempt attempt, int? bufferMinutes)
        {
            var defaultTtl = GetAttemptTtlMinutes();
            if (attempt.EndTime.HasValue)
            {
                var now = DateTime.UtcNow;
                var computed = attempt.EndTime.Value - now;
                if (bufferMinutes.HasValue)
                {
                    computed += TimeSpan.FromMinutes(bufferMinutes.Value);
                }
                if (computed < TimeSpan.FromMinutes(1))
                {
                    computed = TimeSpan.FromMinutes(1);
                }
                return computed;
            }
            return defaultTtl;
        }

        /// <summary>
        /// Lưu một câu trả lời vào Redis (Manual Save)
        /// </summary>
        /// <remarks>
        /// Yêu cầu JWT. TTL được tính động theo `EndTime` của attempt cộng thêm `bufferMinutes` (nếu truyền).
        ///
        /// Sample request:
        ///
        /// {
        ///   "examAttemptId": 123,
        ///   "questionId": 456,
        ///   "selectedOptionIds": [1,2],
        ///   "textAnswer": null,
        ///   "bufferMinutes": 5
        /// }
        ///
        /// Sample response (200):
        /// {
        ///   "success": true,
        ///   "data": { "message": "Đã lưu tiến trình", "attemptId": 123, "questionId": 456 }
        /// }
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("{id}/attempts/{attemptId}/save")]
        [Authorize]
        public async Task<IActionResult> SaveAnswer(int id, int attemptId, [FromBody] SaveAnswerRequest request)
        {
            try
            {
                var userId = HttpContext.GetSyncedUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                if (request.ExamAttemptId != attemptId)
                {
                    return BadRequest(ApiResponse.ErrorResponse("ExamAttemptId không khớp", 400));
                }

                var attempt = await ValidateAttemptAsync(id, attemptId, userId.Value);
                if (attempt == null)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Phiên thi không hợp lệ hoặc đã kết thúc", 400));
                }

                var cacheItem = new ExamsService.Services.AttemptAnswerCache
                {
                    QuestionId = request.QuestionId,
                    SelectedOptionIds = request.SelectedOptionIds ?? new List<int>(),
                    TextAnswer = request.TextAnswer,
                    SavedAt = DateTime.UtcNow
                };

                var ttl = ComputeDynamicTtl(attempt, request.BufferMinutes);
                await _progressCache.SaveAnswerAsync(attemptId, request.QuestionId, cacheItem, ttl);

                return Ok(ApiResponse.SuccessResponse(new { message = "Đã lưu tiến trình", attemptId, questionId = request.QuestionId }));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lưu tiến trình", 500));
            }
        }

        /// <summary>
        /// Lưu batch các câu trả lời vào Redis (Manual Save)
        /// </summary>
        /// <remarks>
        /// Yêu cầu JWT. TTL được tính động theo `EndTime` của attempt cộng thêm `bufferMinutes` (nếu truyền).
        ///
        /// Sample request:
        /// {
        ///   "examAttemptId": 123,
        ///   "answers": [
        ///     { "questionId": 456, "selectedOptionIds": [1,2], "textAnswer": null },
        ///     { "questionId": 789, "selectedOptionIds": [], "textAnswer": "tự luận" }
        ///   ],
        ///   "bufferMinutes": 5
        /// }
        ///
        /// Sample response (200):
        /// {
        ///   "success": true,
        ///   "data": { "message": "Đã lưu batch tiến trình", "attemptId": 123, "count": 2 }
        /// }
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("{id}/attempts/{attemptId}/save-batch")]
        [Authorize]
        public async Task<IActionResult> SaveAnswersBatch(int id, int attemptId, [FromBody] SaveBatchAnswersRequest request)
        {
            try
            {
                var userId = HttpContext.GetSyncedUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                if (request.ExamAttemptId != attemptId)
                {
                    return BadRequest(ApiResponse.ErrorResponse("ExamAttemptId không khớp", 400));
                }

                var attempt = await ValidateAttemptAsync(id, attemptId, userId.Value);
                if (attempt == null)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Phiên thi không hợp lệ hoặc đã kết thúc", 400));
                }

                var items = (request.Answers ?? new List<SaveAnswerItem>())
                    .Select(a => new ExamsService.Services.AttemptAnswerCache
                    {
                        QuestionId = a.QuestionId,
                        SelectedOptionIds = a.SelectedOptionIds ?? new List<int>(),
                        TextAnswer = a.TextAnswer,
                        SavedAt = DateTime.UtcNow
                    })
                    .ToList();

                var ttl = ComputeDynamicTtl(attempt, request.BufferMinutes);
                await _progressCache.SaveBatchAsync(attemptId, items, ttl);

                return Ok(ApiResponse.SuccessResponse(new { message = "Đã lưu batch tiến trình", attemptId, count = items.Count }));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lưu batch tiến trình", 500));
            }
        }

        /// <summary>
        /// Khôi phục tiến trình đã lưu từ Redis
        /// </summary>
        /// <remarks>
        /// Yêu cầu JWT. Trả về tất cả câu trả lời đang lưu trong Redis cho attempt.
        ///
        /// Sample response (200):
        /// {
        ///   "success": true,
        ///   "data": {
        ///     "examAttemptId": 123,
        ///     "count": 2,
        ///     "answers": [
        ///       { "questionId": 456, "selectedOptionIds": [1,2], "textAnswer": null },
        ///       { "questionId": 789, "selectedOptionIds": [], "textAnswer": "tự luận" }
        ///     ]
        ///   }
        /// }
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{id}/attempts/{attemptId}/progress")]
        [Authorize]
        public async Task<IActionResult> RestoreProgress(int id, int attemptId)
        {
            try
            {
                var userId = HttpContext.GetSyncedUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                var attempt = await ValidateAttemptAsync(id, attemptId, userId.Value);
                if (attempt == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy phiên thi hợp lệ", 404));
                }

                var cache = await _progressCache.GetAllAsync(attemptId);
                var answers = cache.Values.Select(v => new SubmittedAnswerDto
                {
                    QuestionId = v.QuestionId,
                    SelectedOptionIds = v.SelectedOptionIds ?? new List<int>(),
                    TextAnswer = v.TextAnswer
                }).ToList();

                var response = new RestoreProgressResponse
                {
                    ExamAttemptId = attemptId,
                    Count = answers.Count,
                    Answers = answers
                };

                return Ok(ApiResponse.SuccessResponse(response));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi khôi phục tiến trình", 500));
            }
        }

        /// <summary>
        /// Lấy danh sách bài thi theo môn, giáo viên
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetExams(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? courseId = null,
            [FromQuery] int? teacherId = null,
            [FromQuery] int? subjectId = null)
        {
            try
            {
                if (pageIndex <= 0) pageIndex = 1;
                if (pageSize <= 0) pageSize = 10;

                var query = _context.Exams
                    .AsNoTracking()
                    .Where(e => !e.HasDelete)
                    .Include(e => e.Course)
                    .ThenInclude(c => c!.Teacher)
                    .Include(e => e.Course)
                    .ThenInclude(c => c!.Subject)
                    .Include(e => e.Creator)
                    .AsQueryable();

                // Apply filters
                if (courseId.HasValue)
                {
                    query = query.Where(e => e.CourseId == courseId.Value);
                }

                if (teacherId.HasValue)
                {
                    query = query.Where(e => e.Course!.TeacherId == teacherId.Value);
                }

                if (subjectId.HasValue)
                {
                    query = query.Where(e => e.Course!.SubjectId == subjectId.Value);
                }

                query = query.OrderByDescending(e => e.CreatedAt);

                var total = await query.LongCountAsync();

                var items = await query
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new ExamListItemDto
                    {
                        Id = e.ExamId,
                        Title = e.Title,
                        Description = e.Description,
                        CourseId = e.CourseId,
                        CourseName = e.Course != null ? e.Course.Title : null,
                        TeacherId = e.Course != null ? e.Course.TeacherId : null,
                        TeacherName = e.Course != null && e.Course.Teacher != null ? e.Course.Teacher.FullName : null,
                        SubjectId = e.Course != null ? e.Course.SubjectId : null,
                        SubjectName = e.Course != null && e.Course.Subject != null ? e.Course.Subject.Name : null,
                        DurationMinutes = e.DurationMinutes,
                        TotalQuestions = e.TotalQuestions,
                        TotalMarks = e.TotalMarks,
                        PassingMark = e.PassingMark,
                        ExamType = e.ExamType,
                        StartAt = e.StartAt,
                        EndAt = e.EndAt,
                        Status = e.Status,
                        CreatedAt = e.CreatedAt,
                        CreatedBy = e.CreatedBy,
                        CreatedByName = e.Creator != null ? e.Creator.FullName : null,
                        // ✅ NEW FIELDS FOR CERTIFICATION EXAMS
                        ImageUrl = e.ImageUrl,
                        Price = e.Price,
                        OriginalPrice = e.OriginalPrice,
                        Level = e.Level,
                        Difficulty = e.Difficulty,
                        Provider = e.Provider,
                        FeaturesJson = e.FeaturesJson,
                        ValidPeriod = e.ValidPeriod
                    })
                    .ToListAsync();

                var result = new PagedResponse<ExamListItemDto>
                {
                    Items = items,
                    Total = total,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                };

                return Ok(ApiResponse.SuccessResponse(result, "Lấy danh sách bài thi thành công"));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Có lỗi xảy ra khi lấy danh sách bài thi", 500));
            }
        }

        /// <summary>
        /// Lấy thông tin exam attempt theo attemptId
        /// </summary>
        [HttpGet("attempts/{attemptId}")]
        [Authorize]
        public async Task<IActionResult> GetExamAttempt(int attemptId)
        {
            try
            {
                _logger.LogInformation("GetExamAttempt called with attemptId: {AttemptId}", attemptId);
                
                var userId = HttpContext.GetSyncedUserId();
                if (!userId.HasValue)
                {
                    _logger.LogWarning("GetExamAttempt: User not authenticated");
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }
                
                _logger.LogInformation("GetExamAttempt: UserId: {UserId}, AttemptId: {AttemptId}", userId.Value, attemptId);

                // Kiểm tra xem ExamAttempt có tồn tại không (không check userId trước)
                var anyAttempt = await _context.ExamAttempts
                    .FirstOrDefaultAsync(ea => ea.ExamAttemptId == attemptId && !ea.HasDelete);
                
                if (anyAttempt == null)
                {
                    _logger.LogWarning("GetExamAttempt: No exam attempt found with attemptId: {AttemptId}", attemptId);
                    return NotFound(ApiResponse.ErrorResponse($"Không tìm thấy bài thi với ID: {attemptId}", 404));
                }
                
                _logger.LogInformation("GetExamAttempt: Found attempt - UserId: {AttemptUserId}, Status: {Status}, ExamId: {ExamId}", 
                    anyAttempt.UserId, anyAttempt.Status, anyAttempt.ExamId);

                // Get exam attempt with exam info
                var examAttempt = await _context.ExamAttempts
                    .Include(ea => ea.Exam)
                        .ThenInclude(e => e.Course)
                    .FirstOrDefaultAsync(ea => ea.ExamAttemptId == attemptId && ea.UserId == userId.Value && !ea.HasDelete);

                if (examAttempt == null)
                {
                    _logger.LogWarning("GetExamAttempt: Attempt exists but userId mismatch. Expected: {ExpectedUserId}, Found: {FoundUserId}", 
                        userId.Value, anyAttempt.UserId);
                    return StatusCode(403, ApiResponse.ErrorResponse("Bạn không có quyền truy cập bài thi này", 403));
                }

                // Check if attempt is still in progress
                if (examAttempt.Status != "InProgress")
                {
                    _logger.LogWarning("GetExamAttempt: Attempt status is not InProgress: {Status}", examAttempt.Status);
                    return BadRequest(ApiResponse.ErrorResponse($"Bài thi này đã {examAttempt.Status}", 400));
                }

                // Get exam questions
                _logger.LogInformation("GetExamAttempt: Loading questions for examId: {ExamId}", examAttempt.ExamId);
                
                var examQuestions = await _context.ExamQuestions
                    .Include(eq => eq.Question)
                    .ThenInclude(q => q.Bank)
                    .Where(eq => eq.ExamId == examAttempt.ExamId && !eq.HasDelete && !eq.Question.HasDelete)
                    .OrderBy(eq => eq.SequenceIndex)
                    .ToListAsync();

                _logger.LogInformation("GetExamAttempt: Found {QuestionCount} questions", examQuestions.Count);

                // Prepare questions for response
                var questions = new List<ExamQuestionDto>();
                foreach (var examQuestion in examQuestions)
                {
                    var answerOptions = await _context.AnswerOptions
                         .Where(ao => ao.QuestionId == examQuestion.QuestionId && !ao.HasDelete)
                         .Select(ao => new AnswerOptionDto
                         {
                             OptionId = ao.OptionId,
                             Content = ao.Content,
                             IsCorrect = false, // Don't reveal correct answers
                             SequenceIndex = ao.OrderIndex
                         })
                         .OrderBy(ao => ao.SequenceIndex)
                         .ToListAsync();

                     questions.Add(new ExamQuestionDto
                     {
                         QuestionId = examQuestion.QuestionId,
                         Content = examQuestion.Question.Content,
                         QuestionType = examQuestion.Question.QuestionType,
                         Difficulty = examQuestion.Question.Difficulty,
                         Marks = examQuestion.Marks,
                         Options = answerOptions
                     });
                }

                // Randomize questions if enabled (same order as when started)
                if (examAttempt.Exam.RandomizeQuestions)
                {
                    // Note: This should use the same randomization seed as when the attempt was created
                    // For now, we'll just keep the order from the database
                }

                var response = new StartExamResponse
                {
                    ExamAttemptId = examAttempt.ExamAttemptId,
                    ExamId = examAttempt.ExamId,
                    ExamTitle = examAttempt.Exam.Title,
                    VariantCode = examAttempt.VariantCode,
                    StartTime = examAttempt.StartTime,
                    EndTime = examAttempt.EndTime,
                    DurationMinutes = examAttempt.Exam.DurationMinutes ?? 0,
                    Questions = questions,
                    TotalMarks = examAttempt.Exam.TotalMarks ?? 0,
                    PassingMark = examAttempt.Exam.PassingMark ?? 0,
                    Instructions = examAttempt.Exam.Description ?? ""
                };

                _logger.LogInformation("GetExamAttempt: Returning response with {QuestionCount} questions, AttemptId: {AttemptId}", 
                    questions.Count, examAttempt.ExamAttemptId);

                return Ok(ApiResponse.SuccessResponse(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exam attempt {AttemptId}", attemptId);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy thông tin bài thi", 500));
            }
        }

        /// <summary>
        /// Lấy chi tiết đề thi (thời gian, câu hỏi, mô tả)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetExam(int id)
        {
            try
            {
                // Get exam basic info first
                var exam = await _context.Exams
                    .AsNoTracking()
                    .Where(e => e.ExamId == id && !e.HasDelete)
                    .FirstOrDefaultAsync();

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy bài thi", 404));
                }

                // Get course info separately
                var course = await _context.Courses
                    .AsNoTracking()
                    .Where(c => c.CourseId == exam.CourseId)
                    .FirstOrDefaultAsync();

                // Get teacher info separately if course exists
                User? teacher = null;
                if (course?.TeacherId != null)
                {
                    teacher = await _context.Users
                        .AsNoTracking()
                        .Where(u => u.UserId == course.TeacherId)
                        .FirstOrDefaultAsync();
                }

                // Get subject info separately if course exists
                Subject? subject = null;
                if (course?.SubjectId != null)
                {
                    subject = await _context.Subjects
                        .AsNoTracking()
                        .Where(s => s.SubjectId == course.SubjectId)
                        .FirstOrDefaultAsync();
                }

                // Get creator info separately if exam has CreatedBy
                User? creator = null;
                if (exam.CreatedBy != null)
                {
                    creator = await _context.Users
                        .AsNoTracking()
                        .Where(u => u.UserId == exam.CreatedBy)
                        .FirstOrDefaultAsync();
                }

                // Get exam questions separately
                var examQuestions = await _context.ExamQuestions
                    .AsNoTracking()
                    .Where(eq => eq.ExamId == id)
                    .OrderBy(eq => eq.SequenceIndex ?? eq.ExamQuestionId)
                    .ToListAsync();

                var questions = new List<ExamQuestionDto>();
                
                foreach (var eq in examQuestions)
                {
                    // Get question info
                    var question = await _context.Questions
                        .AsNoTracking()
                        .Where(q => q.QuestionId == eq.QuestionId)
                        .FirstOrDefaultAsync();

                    // Get answer options for this question
                    var answerOptions = await _context.AnswerOptions
                        .AsNoTracking()
                        .Where(ao => ao.QuestionId == eq.QuestionId && !ao.HasDelete)
                        .OrderBy(ao => ao.OrderIndex ?? ao.OptionId)
                        .ToListAsync();

                    questions.Add(new ExamQuestionDto
                    {
                        ExamQuestionId = eq.ExamQuestionId,
                        QuestionId = eq.QuestionId,
                        Content = question?.Content ?? "",
                        QuestionType = question?.QuestionType,
                        Difficulty = question?.Difficulty,
                        Marks = eq.Marks,
                        SequenceIndex = eq.SequenceIndex,
                        Options = answerOptions.Select(ao => new AnswerOptionDto
                        {
                            OptionId = ao.OptionId,
                            Content = ao.Content,
                            IsCorrect = ao.IsCorrect,
                            SequenceIndex = ao.OrderIndex
                        }).ToList()
                    });
                }

                var examDetail = new ExamDetailDto
                {
                    Id = exam.ExamId,
                    Title = exam.Title,
                    Description = exam.Description,
                    CourseId = exam.CourseId,
                    CourseName = course?.Title,
                    TeacherId = course?.TeacherId,
                    TeacherName = teacher?.FullName,
                    SubjectId = course?.SubjectId,
                    SubjectName = subject?.Name,
                    DurationMinutes = exam.DurationMinutes,
                    TotalQuestions = exam.TotalQuestions,
                    TotalMarks = exam.TotalMarks,
                    PassingMark = exam.PassingMark,
                    ExamType = exam.ExamType,
                    StartAt = exam.StartAt,
                    EndAt = exam.EndAt,
                    RandomizeQuestions = exam.RandomizeQuestions,
                    AllowMultipleAttempts = exam.AllowMultipleAttempts,
                    Status = exam.Status,
                    CreatedAt = exam.CreatedAt,
                    CreatedBy = exam.CreatedBy,
                    CreatedByName = creator?.FullName,
                    Questions = questions,
                    // ✅ NEW FIELDS FOR CERTIFICATION EXAMS
                    ImageUrl = exam.ImageUrl,
                    Price = exam.Price,
                    OriginalPrice = exam.OriginalPrice,
                    Level = exam.Level,
                    Difficulty = exam.Difficulty,
                    Provider = exam.Provider,
                    FeaturesJson = exam.FeaturesJson,
                    ValidPeriod = exam.ValidPeriod
                };

                return Ok(ApiResponse.SuccessResponse(examDetail));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy thông tin bài thi", 500));
            }
        }

        /// <summary>
        /// Tạo bài thi mới (chỉ dành cho giáo viên)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> CreateExam([FromBody] CreateExamRequest request)
        {
            try
            {
                _logger.LogInformation("📝 Creating exam. Title: {Title}, TeacherId: {TeacherId}", request.Title, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                _logger.LogInformation("📦 Request data: ImageUrl={ImageUrl}, Price={Price}, Level={Level}, Difficulty={Difficulty}", 
                    request.ImageUrl, request.Price, request.Level, request.Difficulty);

                var teacherId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                // ✅ Check if user exists in database (fix Foreign Key constraint)
                int? createdByUserId = null;
                var existingUser = await _context.Users.FindAsync(teacherId);
                if (existingUser != null)
                {
                    createdByUserId = teacherId;
                    _logger.LogInformation("✅ User {UserId} found in ExamsService database. Using as CreatedBy.", teacherId);
                }
                else
                {
                    _logger.LogWarning("⚠️ User {UserId} not found in ExamsService database. Attempting to sync from AuthService...", teacherId);
                    
                    // Try to sync user from AuthService (create new user with auto-generated UserId)
                    var userFromAuth = await _userSyncService.GetUserByIdAsync(teacherId);
                    if (userFromAuth != null)
                    {
                        try
                        {
                            // Create new user WITHOUT setting UserId (let database auto-generate)
                            // This breaks the link between AuthService and ExamsService, but avoids IDENTITY_INSERT issues
                            var newUser = new ExamsService.Models.User
                            {
                                // UserId will be auto-generated by database
                                Email = userFromAuth.Email ?? "",
                                FullName = userFromAuth.FullName ?? "",
                                PasswordHash = "", // Not needed in ExamsService
                                RoleId = userFromAuth.RoleId ?? 3, // Default Student role
                                Status = userFromAuth.Status ?? "Active",
                                IsEmailVerified = userFromAuth.IsEmailVerified,
                                CreatedAt = userFromAuth.CreatedAt != default(DateTime) ? userFromAuth.CreatedAt : DateTime.UtcNow,
                                HasDelete = userFromAuth.HasDelete
                            };
                            
                            _context.Users.Add(newUser);
                            await _context.SaveChangesAsync();
                            
                            // Use the newly created UserId (auto-generated)
                            createdByUserId = newUser.UserId;
                            _logger.LogInformation("✅ User synced from AuthService with new UserId={NewUserId} (original AuthService UserId={OriginalUserId}). Using as CreatedBy.", 
                                newUser.UserId, teacherId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Error syncing user {UserId} from AuthService. Setting CreatedBy to null.", teacherId);
                            createdByUserId = null; // Set to null to avoid FK constraint
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Could not fetch user {UserId} from AuthService. Setting CreatedBy to null.", teacherId);
                        createdByUserId = null; // Set to null to avoid FK constraint
                    }
                }

                // Validate course exists if provided
                if (request.CourseId.HasValue && !await _context.Courses.AnyAsync(c => c.CourseId == request.CourseId.Value && !c.HasDelete))
                {
                    return BadRequest(ApiResponse.ErrorResponse("Course không tồn tại", 400));
                }

                // ✅ Final validation: If createdByUserId is set, verify user exists
                if (createdByUserId.HasValue)
                {
                    var verifyUser = await _context.Users.FindAsync(createdByUserId.Value);
                    if (verifyUser == null)
                    {
                        _logger.LogWarning("⚠️ User {UserId} does not exist in database. Setting CreatedBy to null.", createdByUserId.Value);
                        createdByUserId = null;
                    }
                }

                _logger.LogInformation("✅ Creating Exam entity with CreatedBy={CreatedBy}...", createdByUserId);
                var exam = new Exam
                {
                    Title = request.Title,
                    Description = request.Description,
                    CourseId = request.CourseId,
                    SubjectId = request.SubjectId,
                    DurationMinutes = request.DurationMinutes,
                    TotalQuestions = request.TotalQuestions,
                    TotalMarks = request.TotalMarks,
                    PassingMark = request.PassingMark,
                    ExamType = request.ExamType,
                    StartAt = request.StartAt,
                    EndAt = request.EndAt,
                    RandomizeQuestions = request.RandomizeQuestions,
                    AllowMultipleAttempts = request.AllowMultipleAttempts,
                    Status = request.Status ?? "Draft",
                    CreatedBy = createdByUserId, // ✅ Set null if user not found to avoid FK constraint
                    CreatedAt = DateTime.UtcNow,
                    HasDelete = false,
                    // ✅ NEW FIELDS FOR CERTIFICATION EXAMS
                    ImageUrl = request.ImageUrl,
                    Price = request.Price,
                    OriginalPrice = request.OriginalPrice,
                    Level = request.Level,
                    Difficulty = request.Difficulty,
                    Provider = request.Provider,
                    FeaturesJson = request.FeaturesJson,
                    ValidPeriod = request.ValidPeriod
                };

                _logger.LogInformation("💾 Saving exam to database with CreatedBy={CreatedBy} (nullable: {IsNullable})...", 
                    exam.CreatedBy, exam.CreatedBy == null ? "YES" : "NO");
                
                // Ensure no navigation properties are loaded that might trigger user insert
                _context.ChangeTracker.Clear();
                _context.Exams.Add(exam);
                
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ Exam saved successfully. ExamId: {ExamId}, CreatedBy: {CreatedBy}", exam.ExamId, exam.CreatedBy);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "❌ Database error when saving exam. CreatedBy={CreatedBy}, Error: {Error}", 
                        exam.CreatedBy, dbEx.InnerException?.Message ?? dbEx.Message);
                    
                    // If FK constraint error and CreatedBy is not null, try setting it to null
                    if (dbEx.InnerException?.Message?.Contains("FK_Exams_Users_CreatedBy") == true && exam.CreatedBy.HasValue)
                    {
                        _logger.LogWarning("⚠️ Retrying with CreatedBy=null due to FK constraint error...");
                        exam.CreatedBy = null;
                        _context.ChangeTracker.Clear();
                        _context.Exams.Update(exam);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("✅ Exam saved successfully with CreatedBy=null. ExamId: {ExamId}", exam.ExamId);
                    }
                    else
                    {
                        throw; // Re-throw to be caught by outer catch block
                    }
                }

                // Add questions to exam if provided
                if (request.Questions != null && request.Questions.Any())
                {
                    foreach (var questionRequest in request.Questions)
                    {
                        // Validate question exists
                        if (!await _context.Questions.AnyAsync(q => q.QuestionId == questionRequest.QuestionId && !q.HasDelete))
                        {
                            continue; // Skip invalid questions
                        }

                        var examQuestion = new ExamQuestion
                        {
                            ExamId = exam.ExamId,
                            QuestionId = questionRequest.QuestionId,
                            Marks = questionRequest.Marks,
                            SequenceIndex = questionRequest.SequenceIndex,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.ExamQuestions.Add(examQuestion);
                    }

                    await _context.SaveChangesAsync();
                }

                // Return the created exam details
                var createdExamDetail = new ExamDetailDto
                {
                    Id = exam.ExamId,
                    Title = exam.Title,
                    Description = exam.Description,
                    CourseId = exam.CourseId,
                    SubjectId = exam.SubjectId, // ✅ NEW FIELD
                    DurationMinutes = exam.DurationMinutes,
                    TotalQuestions = exam.TotalQuestions,
                    TotalMarks = exam.TotalMarks,
                    PassingMark = exam.PassingMark,
                    ExamType = exam.ExamType,
                    StartAt = exam.StartAt,
                    EndAt = exam.EndAt,
                    RandomizeQuestions = exam.RandomizeQuestions,
                    AllowMultipleAttempts = exam.AllowMultipleAttempts,
                    Status = exam.Status,
                    CreatedAt = exam.CreatedAt,
                    Questions = new List<ExamQuestionDto>(),
                    // ✅ NEW FIELDS FOR CERTIFICATION EXAMS
                    ImageUrl = exam.ImageUrl,
                    Price = exam.Price,
                    OriginalPrice = exam.OriginalPrice,
                    Level = exam.Level,
                    Difficulty = exam.Difficulty,
                    Provider = exam.Provider,
                    FeaturesJson = exam.FeaturesJson,
                    ValidPeriod = exam.ValidPeriod
                };

                return CreatedAtAction(nameof(GetExam), new { id = exam.ExamId }, ApiResponse.SuccessResponse(createdExamDetail));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error when creating exam");
                return BadRequest(ApiResponse.ErrorResponse(ex.Message, 400));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error when creating exam. Inner exception: {InnerException}", dbEx.InnerException?.Message);
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi database khi tạo bài thi: {dbEx.InnerException?.Message ?? dbEx.Message}", 500));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when creating exam. Message: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống khi tạo bài thi: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Cập nhật thông tin bài thi
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> UpdateExam(int id, [FromBody] UpdateExamRequest request)
        {
            try
            {
                var exam = await _context.Exams
                    .AsNoTracking() // ✅ Don't track navigation properties to avoid user insert issues
                    .FirstOrDefaultAsync(e => e.ExamId == id && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Bài thi không tồn tại", 404));
                }

                // ✅ Clear ChangeTracker and re-attach exam to avoid navigation property issues
                _context.ChangeTracker.Clear();
                
                // Re-attach exam to context for update
                _context.Exams.Attach(exam);
                _context.Entry(exam).State = Microsoft.EntityFrameworkCore.EntityState.Modified;

                // Update exam properties if provided
                if (!string.IsNullOrEmpty(request.Title))
                    exam.Title = request.Title;

                if (request.Description != null)
                    exam.Description = request.Description;

                if (request.CourseId.HasValue)
                {
                    var courseExists = await _context.Courses
                        .AnyAsync(c => c.CourseId == request.CourseId.Value && !c.HasDelete);
                    if (!courseExists)
                    {
                        return BadRequest(ApiResponse.ErrorResponse("Course không tồn tại", 400));
                    }
                    exam.CourseId = request.CourseId;
                }

                if (request.DurationMinutes.HasValue)
                    exam.DurationMinutes = request.DurationMinutes;

                if (request.TotalQuestions.HasValue)
                    exam.TotalQuestions = request.TotalQuestions;

                if (request.TotalMarks.HasValue)
                    exam.TotalMarks = request.TotalMarks;

                if (request.PassingMark.HasValue)
                    exam.PassingMark = request.PassingMark;

                if (!string.IsNullOrEmpty(request.ExamType))
                    exam.ExamType = request.ExamType;

                if (request.StartAt.HasValue)
                    exam.StartAt = request.StartAt;

                if (request.EndAt.HasValue)
                    exam.EndAt = request.EndAt;

                if (request.RandomizeQuestions.HasValue)
                    exam.RandomizeQuestions = request.RandomizeQuestions.Value;

                if (request.AllowMultipleAttempts.HasValue)
                    exam.AllowMultipleAttempts = request.AllowMultipleAttempts.Value;

                if (!string.IsNullOrEmpty(request.Status))
                    exam.Status = request.Status;

                // ✅ UPDATE NEW FIELDS FOR CERTIFICATION EXAMS
                if (request.SubjectId.HasValue)
                    exam.SubjectId = request.SubjectId;

                if (request.ImageUrl != null)
                    exam.ImageUrl = request.ImageUrl;

                if (request.Price.HasValue)
                    exam.Price = request.Price;

                if (request.OriginalPrice.HasValue)
                    exam.OriginalPrice = request.OriginalPrice;

                if (!string.IsNullOrEmpty(request.Level))
                    exam.Level = request.Level;

                if (!string.IsNullOrEmpty(request.Difficulty))
                    exam.Difficulty = request.Difficulty;

                if (!string.IsNullOrEmpty(request.Provider))
                    exam.Provider = request.Provider;

                if (request.FeaturesJson != null)
                    exam.FeaturesJson = request.FeaturesJson;

                if (!string.IsNullOrEmpty(request.ValidPeriod))
                    exam.ValidPeriod = request.ValidPeriod;

                // ✅ Validate CreatedBy exists before saving (fix Foreign Key constraint)
                if (exam.CreatedBy.HasValue)
                {
                    var createdByUser = await _context.Users
                        .AsNoTracking() // Don't track to avoid issues
                        .FirstOrDefaultAsync(u => u.UserId == exam.CreatedBy.Value);
                    if (createdByUser == null)
                    {
                        _logger.LogWarning("⚠️ Exam {ExamId} has CreatedBy={CreatedBy} but user does not exist. Setting to null.", 
                            exam.ExamId, exam.CreatedBy.Value);
                        exam.CreatedBy = null;
                        // Update the state after changing CreatedBy
                        _context.Entry(exam).Property(e => e.CreatedBy).IsModified = true;
                    }
                }

                try
                {
                    // ✅ Explicitly mark only the properties we want to update as modified
                    // This prevents EF Core from trying to insert related entities
                    _context.Entry(exam).Property(e => e.Title).IsModified = !string.IsNullOrEmpty(request.Title);
                    _context.Entry(exam).Property(e => e.Description).IsModified = request.Description != null;
                    _context.Entry(exam).Property(e => e.CourseId).IsModified = request.CourseId.HasValue;
                    _context.Entry(exam).Property(e => e.DurationMinutes).IsModified = request.DurationMinutes.HasValue;
                    _context.Entry(exam).Property(e => e.TotalQuestions).IsModified = request.TotalQuestions.HasValue;
                    _context.Entry(exam).Property(e => e.TotalMarks).IsModified = request.TotalMarks.HasValue;
                    _context.Entry(exam).Property(e => e.PassingMark).IsModified = request.PassingMark.HasValue;
                    _context.Entry(exam).Property(e => e.ExamType).IsModified = !string.IsNullOrEmpty(request.ExamType);
                    _context.Entry(exam).Property(e => e.StartAt).IsModified = request.StartAt.HasValue;
                    _context.Entry(exam).Property(e => e.EndAt).IsModified = request.EndAt.HasValue;
                    _context.Entry(exam).Property(e => e.RandomizeQuestions).IsModified = request.RandomizeQuestions.HasValue;
                    _context.Entry(exam).Property(e => e.AllowMultipleAttempts).IsModified = request.AllowMultipleAttempts.HasValue;
                    _context.Entry(exam).Property(e => e.Status).IsModified = !string.IsNullOrEmpty(request.Status);
                    _context.Entry(exam).Property(e => e.SubjectId).IsModified = request.SubjectId.HasValue;
                    _context.Entry(exam).Property(e => e.ImageUrl).IsModified = request.ImageUrl != null;
                    _context.Entry(exam).Property(e => e.Price).IsModified = request.Price.HasValue;
                    _context.Entry(exam).Property(e => e.OriginalPrice).IsModified = request.OriginalPrice.HasValue;
                    _context.Entry(exam).Property(e => e.Level).IsModified = !string.IsNullOrEmpty(request.Level);
                    _context.Entry(exam).Property(e => e.Difficulty).IsModified = !string.IsNullOrEmpty(request.Difficulty);
                    _context.Entry(exam).Property(e => e.Provider).IsModified = !string.IsNullOrEmpty(request.Provider);
                    _context.Entry(exam).Property(e => e.FeaturesJson).IsModified = request.FeaturesJson != null;
                    _context.Entry(exam).Property(e => e.ValidPeriod).IsModified = !string.IsNullOrEmpty(request.ValidPeriod);
                    _context.Entry(exam).Property(e => e.CreatedBy).IsModified = true; // Always allow CreatedBy to be updated if needed
                    
                    // ✅ Explicitly exclude navigation properties from being tracked/modified
                    _context.Entry(exam).Reference(e => e.Creator).IsModified = false;
                    _context.Entry(exam).Reference(e => e.Course).IsModified = false;
                    _context.Entry(exam).Reference(e => e.Subject).IsModified = false;
                    
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ Exam {ExamId} updated successfully", exam.ExamId);
                    return Ok(ApiResponse.SuccessResponse(null, "Cập nhật bài thi thành công"));
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "❌ Database error when updating exam {ExamId}. Error: {Error}", 
                        exam.ExamId, dbEx.InnerException?.Message ?? dbEx.Message);
                    
                    // If FK constraint error and CreatedBy is not null, try setting it to null
                    if (dbEx.InnerException?.Message?.Contains("FK_Exams_Users_CreatedBy") == true && exam.CreatedBy.HasValue)
                    {
                        _logger.LogWarning("⚠️ Retrying update with CreatedBy=null due to FK constraint error...");
                        exam.CreatedBy = null;
                        _context.ChangeTracker.Clear();
                        _context.Exams.Update(exam);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("✅ Exam {ExamId} updated successfully with CreatedBy=null", exam.ExamId);
                        return Ok(ApiResponse.SuccessResponse(null, "Cập nhật bài thi thành công"));
                    }
                    
                    return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi database khi cập nhật bài thi: {dbEx.InnerException?.Message ?? dbEx.Message}", 500));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when updating exam. Message: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống khi cập nhật bài thi: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Upload exam cover image to Cloudinary (giống upload avatar)
        /// </summary>
        [HttpPost("upload-image")]
        [Authorize(Roles = "Teacher,Admin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20_000_000)] // 20MB
        public async Task<IActionResult> UploadExamImage(IFormFile file)
        {
            try
            {
                _logger.LogInformation("📤 [ExamsService] Upload exam image request. File: {FileName}, Size: {FileSize}, ContentType: {ContentType}", 
                    file?.FileName, file?.Length, file?.ContentType);

                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("❌ [ExamsService] File is null or empty");
                    return BadRequest(ApiResponse.ErrorResponse("File rỗng", 400));
                }

                // Validate file type (only images)
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                var contentType = file.ContentType?.ToLower() ?? "";
                if (!allowedTypes.Contains(contentType))
                {
                    _logger.LogWarning("❌ [ExamsService] Invalid file type: {ContentType}", contentType);
                    return BadRequest(ApiResponse.ErrorResponse($"Chỉ chấp nhận file ảnh (jpg, png, gif, webp). File type: {contentType}", 400));
                }

                // Check Cloudinary initialization
                if (_cloudinary == null)
                {
                    _logger.LogError("❌ [ExamsService] Cloudinary is null - check config");
                    return StatusCode(500, ApiResponse.ErrorResponse("Cloudinary chưa được cấu hình", 500));
                }

                _logger.LogInformation("☁️ [ExamsService] Starting Cloudinary upload to folder: exams/covers");

                await using var stream = file.OpenReadStream();
                var upload = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "exams/covers",  // Different folder from avatars
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false,
                    Transformation = new Transformation()
                        .Width(800)
                        .Height(450)
                        .Crop("fill")
                        .Quality("auto")
                };

                var result = await _cloudinary.UploadAsync(upload);

                _logger.LogInformation("☁️ [ExamsService] Cloudinary upload result - StatusCode: {StatusCode}, Error: {Error}", 
                    result.StatusCode, result.Error?.Message ?? "None");

                if (result.StatusCode == System.Net.HttpStatusCode.OK || 
                    result.StatusCode == System.Net.HttpStatusCode.Created)
                {
                    var url = result.SecureUrl?.ToString();
                    _logger.LogInformation("✅ [ExamsService] Upload successful: {Url}", url);
                    return Ok(ApiResponse.SuccessResponse(new { url }, "Upload ảnh thành công"));
                }

                _logger.LogError("❌ [ExamsService] Cloudinary upload failed: StatusCode={StatusCode}, Error={Error}", 
                    result.StatusCode, result.Error?.Message ?? "Unknown");
                return StatusCode(500, ApiResponse.ErrorResponse($"Upload thất bại: {result.Error?.Message ?? "Unknown error"}", 500));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [ExamsService] Exception during image upload: {Message} | StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                
                var errorMessage = $"Lỗi hệ thống: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" | Inner: {ex.InnerException.Message}";
                    _logger.LogError("Inner exception: {InnerMessage}", ex.InnerException.Message);
                }
                return StatusCode(500, ApiResponse.ErrorResponse(errorMessage, 500));
            }
        }

        /// <summary>
        /// Xóa bài thi
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> DeleteExam(int id)
        {
            try
            {
                // ✅ Use raw SQL to avoid navigation property issues
                // First check if exam exists
                var examExists = await _context.Exams
                    .AsNoTracking()
                    .AnyAsync(e => e.ExamId == id && !e.HasDelete);

                if (!examExists)
                {
                    return NotFound(ApiResponse.ErrorResponse("Bài thi không tồn tại", 404));
                }

                // ✅ Use raw SQL to soft delete exam (avoids navigation property tracking issues)
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE Exams SET HasDelete = 1 WHERE ExamId = {0}", id);

                // Delete related exam questions (hard delete since ExamQuestion doesn't have HasDelete)
                var examQuestions = await _context.ExamQuestions
                    .AsNoTracking()
                    .Where(eq => eq.ExamId == id)
                    .Select(eq => eq.ExamQuestionId)
                    .ToListAsync();
                
                if (examQuestions.Any())
                {
                    // Use raw SQL to delete exam questions
                    await _context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM ExamQuestions WHERE ExamId = {0}", id);
                }

                _logger.LogInformation("✅ Exam {ExamId} deleted successfully", id);
                return Ok(ApiResponse.SuccessResponse(null, "Xóa bài thi thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting exam {ExamId}: {Message}", id, ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerMessage}", ex.InnerException.Message);
                }
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống khi xóa bài thi: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Thêm câu hỏi vào bài thi
        /// </summary>
        [HttpPost("{id}/questions")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> AddQuestionToExam(int id, [FromBody] AddQuestionToExamRequest request)
        {
            try
            {
                // ✅ Use AsNoTracking to avoid tracking navigation properties
                var exam = await _context.Exams
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.ExamId == id && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Bài thi không tồn tại", 404));
                }

                // ✅ Clear ChangeTracker to avoid navigation property issues
                _context.ChangeTracker.Clear();

                // Validate answer options
                if (request.AnswerOptions == null || !request.AnswerOptions.Any())
                {
                    return BadRequest(ApiResponse.ErrorResponse("Câu hỏi phải có ít nhất một đáp án", 400));
                }

                var hasCorrectAnswer = request.AnswerOptions.Any(o => o.IsCorrect);
                if (!hasCorrectAnswer)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Câu hỏi phải có ít nhất một đáp án đúng", 400));
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Create new question
                    var question = new Question
                    {
                        BankId = 1, // Using default question bank
                        Content = request.Content,
                        QuestionType = request.QuestionType ?? "MultipleChoice",
                        Difficulty = request.Difficulty ?? "Medium",
                        Marks = request.Marks ?? 1,
                        CreatedAt = DateTime.UtcNow,
                        HasDelete = false
                    };

                    _context.Questions.Add(question);
                    await _context.SaveChangesAsync();

                    // Create answer options
                    for (int i = 0; i < request.AnswerOptions.Count; i++)
                    {
                        var option = request.AnswerOptions[i];
                        var answerOption = new AnswerOption
                        {
                            QuestionId = question.QuestionId,
                            Content = option.Content,
                            IsCorrect = option.IsCorrect,
                            OrderIndex = option.OrderIndex ?? i + 1,
                            CreatedAt = DateTime.UtcNow,
                            HasDelete = false
                        };

                        _context.AnswerOptions.Add(answerOption);
                    }

                    // Add question to exam
                    var examQuestion = new ExamQuestion
                    {
                        ExamId = id,
                        QuestionId = question.QuestionId,
                        Marks = request.Marks ?? 1,
                        SequenceIndex = request.SequenceIndex ?? 1,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ExamQuestions.Add(examQuestion);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return Ok(ApiResponse.SuccessResponse(new { QuestionId = question.QuestionId }, "Thêm câu hỏi vào bài thi thành công"));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi thêm câu hỏi", 500));
            }
        }

        /// <summary>
        /// Thêm câu hỏi từ ngân hàng có sẵn vào một bài thi cụ thể
        /// </summary>
        [HttpPost("{examId}/add-from-bank")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> AddQuestionsFromBank(int examId, [FromBody] AddQuestionsFromBankRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Dữ liệu không hợp lệ", 400));
            }

            try
            {
                // ✅ Check if exam exists and get its course/subject info (use AsNoTracking to avoid tracking)
                var exam = await _context.Exams
                    .AsNoTracking()
                    .Include(e => e.Course)
                    .ThenInclude(c => c.Subject)
                    .FirstOrDefaultAsync(e => e.ExamId == examId && !e.HasDelete);
                
                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy bài thi", 404));
                }

                // ✅ Clear ChangeTracker before making changes
                _context.ChangeTracker.Clear();

                // Validate question IDs exist in question bank and check subject compatibility
                var existingQuestions = new List<Question>();
                var incompatibleQuestions = new List<int>();
                
                foreach (var questionId in request.QuestionIds)
                {
                    var question = await _context.Questions
                        .Include(q => q.Bank)
                        .ThenInclude(b => b.Subject)
                        .FirstOrDefaultAsync(q => q.QuestionId == questionId && !q.HasDelete);
                    
                    if (question != null)
                    {
                        // Check if question's subject matches exam's subject (if SubjectId is provided in request)
                        if (request.SubjectId.HasValue)
                        {
                            if (question.Bank?.SubjectId != request.SubjectId.Value)
                            {
                                incompatibleQuestions.Add(questionId);
                                continue;
                            }
                        }
                        // If no SubjectId in request, check against exam's subject
                        else if (exam.Course?.SubjectId.HasValue == true)
                        {
                            if (question.Bank?.SubjectId != exam.Course.SubjectId.Value)
                            {
                                incompatibleQuestions.Add(questionId);
                                continue;
                            }
                        }
                        
                        existingQuestions.Add(question);
                    }
                }

                // Report incompatible questions
                if (incompatibleQuestions.Any())
                {
                    var examSubjectName = exam.Course?.Subject?.Name ?? "không xác định";
                    return BadRequest(ApiResponse.ErrorResponse(
                        $"Các câu hỏi sau không thuộc môn học '{examSubjectName}' của bài thi: {string.Join(", ", incompatibleQuestions)}", 
                        400));
                }

                if (existingQuestions.Count != request.QuestionIds.Count)
                {
                    var foundIds = existingQuestions.Select(q => q.QuestionId).ToList();
                    var missingIds = request.QuestionIds.Except(foundIds).ToList();
                    return BadRequest(ApiResponse.ErrorResponse($"Không tìm thấy câu hỏi với ID: {string.Join(", ", missingIds)}", 400));
                }

                // Check if any questions are already in the exam
                var existingExamQuestions = new List<int>();
                foreach (var questionId in request.QuestionIds)
                {
                    var existingExamQuestion = await _context.ExamQuestions
                        .FirstOrDefaultAsync(eq => eq.ExamId == examId && eq.QuestionId == questionId && !eq.HasDelete);
                    if (existingExamQuestion != null)
                    {
                        existingExamQuestions.Add(questionId);
                    }
                }

                if (existingExamQuestions.Any())
                {
                    return BadRequest(ApiResponse.ErrorResponse($"Các câu hỏi sau đã có trong bài thi: {string.Join(", ", existingExamQuestions)}", 400));
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Get current max sequence index for the exam
                    var examQuestions = await _context.ExamQuestions
                        .Where(eq => eq.ExamId == examId && !eq.HasDelete)
                        .Select(eq => eq.SequenceIndex)
                        .ToListAsync();
                    var maxSequenceIndex = examQuestions.Any() ? examQuestions.Max() : 0;

                    var addedQuestions = new List<object>();

                    // Add questions to exam
                    foreach (var questionId in request.QuestionIds)
                    {
                        var question = existingQuestions.First(q => q.QuestionId == questionId);
                        
                        var examQuestion = new ExamQuestion
                        {
                            ExamId = examId,
                            QuestionId = questionId,
                            Marks = request.DefaultMarks ?? question.Marks ?? 1,
                            SequenceIndex = ++maxSequenceIndex,
                            CreatedAt = DateTime.UtcNow,
                            HasDelete = false
                        };

                        _context.ExamQuestions.Add(examQuestion);

                        addedQuestions.Add(new
                        {
                            QuestionId = questionId,
                            Content = question.Content,
                            QuestionType = question.QuestionType,
                            Difficulty = question.Difficulty,
                            Marks = examQuestion.Marks,
                            SequenceIndex = examQuestion.SequenceIndex,
                            SubjectName = question.Bank?.Subject?.Name
                        });
                    }

                    // Update exam's total questions and marks
                    var currentQuestionCount = await _context.ExamQuestions
                        .Where(eq => eq.ExamId == examId && !eq.HasDelete)
                        .CountAsync();
                    var totalQuestions = currentQuestionCount + request.QuestionIds.Count;

                    var currentTotalMarks = await _context.ExamQuestions
                        .Where(eq => eq.ExamId == examId && !eq.HasDelete)
                        .SumAsync(eq => eq.Marks ?? 0);
                    var newQuestionsMarks = request.QuestionIds.Sum(id => request.DefaultMarks ?? existingQuestions.First(q => q.QuestionId == id).Marks ?? 1);
                    var totalMarks = currentTotalMarks + newQuestionsMarks;

                    // ✅ Re-attach exam and update only specific properties
                    _context.Exams.Attach(exam);
                    _context.Entry(exam).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                    _context.Entry(exam).Property(e => e.TotalQuestions).IsModified = true;
                    _context.Entry(exam).Property(e => e.TotalMarks).IsModified = true;
                    
                    // ✅ Exclude navigation properties from being tracked
                    _context.Entry(exam).Reference(e => e.Creator).IsModified = false;
                    _context.Entry(exam).Reference(e => e.Course).IsModified = false;
                    _context.Entry(exam).Reference(e => e.Subject).IsModified = false;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(ApiResponse.SuccessResponse(new 
                    { 
                        AddedQuestions = addedQuestions,
                        ExamSummary = new
                        {
                            ExamId = examId,
                            TotalQuestions = totalQuestions,
                            TotalMarks = totalMarks,
                            ExamSubject = exam.Course?.Subject?.Name
                        }
                    }, $"Đã thêm {request.QuestionIds.Count} câu hỏi vào bài thi thành công"));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi thêm câu hỏi từ ngân hàng", 500));
            }
        }

        /// <summary>
        /// Trộn câu hỏi theo độ khó để tạo đề thi
        /// </summary>
        [HttpPost("{id}/mix-questions")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> MixQuestions(int id, [FromBody] MixQuestionsRequest request)
        {
            try
            {
                var teacherId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                // Validate exam exists and user has permission
                var exam = await _context.Exams
                    .FirstOrDefaultAsync(e => e.ExamId == id && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Bài thi không tồn tại", 404));
                }

                // Check if user has permission (teacher who created the exam or admin)
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != "Admin" && exam.CreatedBy != teacherId)
                {
                    return Forbid();
                }

                // Get available questions from question banks
                var availableQuestions = await _context.Questions
                    .Include(q => q.Bank)
                    .Where(q => !q.HasDelete && q.Bank != null && !q.Bank.HasDelete)
                    .ToListAsync();

                var variants = new List<ExamVariant>();

                for (int variantIndex = 0; variantIndex < request.NumberOfVariants; variantIndex++)
                {
                    var variantCode = $"V{variantIndex + 1:D2}";
                    var selectedQuestions = new List<ExamQuestionDto>();
                    decimal totalMarks = 0;

                    foreach (var distribution in request.DifficultyDistribution)
                    {
                        var questionsForDifficulty = availableQuestions
                            .Where(q => q.Difficulty?.ToLower() == distribution.Difficulty.ToLower())
                            .OrderBy(x => Guid.NewGuid()) // Random order
                            .Take(distribution.QuestionCount)
                            .ToList();

                        foreach (var question in questionsForDifficulty)
                        {
                            var answerOptions = await _context.AnswerOptions
                                 .Where(ao => ao.QuestionId == question.QuestionId && !ao.HasDelete)
                                 .Select(ao => new AnswerOptionDto
                                 {
                                     OptionId = ao.OptionId,
                                     Content = ao.Content,
                                     IsCorrect = ao.IsCorrect,
                                     SequenceIndex = ao.OrderIndex
                                 })
                                 .OrderBy(ao => ao.SequenceIndex)
                                 .ToListAsync();

                             selectedQuestions.Add(new ExamQuestionDto
                             {
                                 QuestionId = question.QuestionId,
                                 Content = question.Content,
                                 QuestionType = question.QuestionType,
                                 Difficulty = question.Difficulty,
                                 Marks = distribution.MarksPerQuestion,
                                 Options = answerOptions
                             });

                            totalMarks += distribution.MarksPerQuestion;
                        }
                    }

                    variants.Add(new ExamVariant
                    {
                        VariantCode = variantCode,
                        Questions = selectedQuestions.OrderBy(x => Guid.NewGuid()).ToList(), // Shuffle questions
                        TotalMarks = totalMarks
                    });
                }

                var response = new MixQuestionsResponse
                {
                    ExamId = id,
                    Variants = variants,
                    Message = $"Đã tạo thành công {request.NumberOfVariants} đề thi với {request.TotalQuestions} câu hỏi mỗi đề"
                };

                return Ok(ApiResponse.SuccessResponse(response));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi trộn câu hỏi", 500));
            }
        }

        /// <summary>
        /// Bắt đầu làm bài thi
        /// </summary>
        [HttpPost("{id}/start")]
        [Authorize]
        public async Task<IActionResult> StartExam(int id, [FromBody] StartExamRequest request)
        {
            try
            {
                var userId = HttpContext.GetSyncedUserId();
                if (userId == null)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                // Validate exam exists
                var exam = await _context.Exams
                    .Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.ExamId == id && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Bài thi không tồn tại", 404));
                }

                // Check if exam is active
                if (exam.Status != "Active")
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bài thi chưa được kích hoạt", 400));
                }

                // Check exam time constraints
                var now = DateTime.UtcNow;
                if (exam.StartAt.HasValue && now < exam.StartAt.Value)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bài thi chưa đến thời gian bắt đầu", 400));
                }

                if (exam.EndAt.HasValue && now > exam.EndAt.Value)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bài thi đã kết thúc", 400));
                }

                // Require successful purchase/enrollment for paid exams
                var price = exam.Course?.Price ?? 0m;
                var isFree = (exam.Course?.IsFree == true) || price <= 0m;
                
                if (!isFree)
                {
                    // Bài thi có phí: kiểm tra enrollment
                    var hasActiveEnrollment = await _context.ExamEnrollments
                        .AnyAsync(en => en.ExamId == id && en.UserId == userId.Value && en.Status == "Active" && !en.HasDelete);

                    if (!hasActiveEnrollment)
                    {
                        return StatusCode(403, ApiResponse.ErrorResponse("Bạn chưa thanh toán để làm bài thi này", 403));
                    }
                }
                else
                {
                    // Bài thi miễn phí: tự động tạo enrollment nếu chưa có
                    var existingEnrollment = await _context.ExamEnrollments
                        .FirstOrDefaultAsync(en => en.ExamId == id && en.UserId == userId.Value && !en.HasDelete);

                    if (existingEnrollment == null)
                    {
                        // Tạo enrollment mới cho bài thi miễn phí
                        var enrollment = new ExamEnrollment
                        {
                            ExamId = id,
                            UserId = userId.Value,
                            Status = "Active",
                            CreatedAt = DateTime.UtcNow,
                            HasDelete = false
                        };
                        _context.ExamEnrollments.Add(enrollment);
                        await _context.SaveChangesAsync();
                    }
                    else if (existingEnrollment.Status != "Active")
                    {
                        // Kích hoạt enrollment nếu đã tồn tại nhưng chưa active
                        existingEnrollment.Status = "Active";
                        await _context.SaveChangesAsync();
                    }
                }

                // Check if user already has an active attempt
                var existingAttempt = await _context.ExamAttempts
                    .FirstOrDefaultAsync(ea => ea.ExamId == id && ea.UserId == userId.Value && 
                                             ea.Status == "InProgress" && !ea.HasDelete);

                if (existingAttempt != null)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bạn đã có một lần thi đang diễn ra", 400));
                }

                // Check multiple attempts policy
                if (!exam.AllowMultipleAttempts)
                {
                    var previousAttempts = await _context.ExamAttempts
                        .CountAsync(ea => ea.ExamId == id && ea.UserId == userId.Value && !ea.HasDelete);

                    if (previousAttempts > 0)
                    {
                        return BadRequest(ApiResponse.ErrorResponse("Bài thi này chỉ cho phép làm một lần", 400));
                    }
                }

                // Get exam questions
                var examQuestions = await _context.ExamQuestions
                    .Include(eq => eq.Question)
                    .ThenInclude(q => q.Bank)
                    .Where(eq => eq.ExamId == id && !eq.HasDelete && !eq.Question.HasDelete)
                    .OrderBy(eq => eq.SequenceIndex)
                    .ToListAsync();

                if (!examQuestions.Any())
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bài thi chưa có câu hỏi", 400));
                }

                // Create exam attempt
                var examAttempt = new ExamAttempt
                {
                    ExamId = id,
                    UserId = userId.Value,
                    VariantCode = request.VariantCode,
                    StartTime = now,
                    EndTime = exam.DurationMinutes.HasValue ? now.AddMinutes(exam.DurationMinutes.Value) : null,
                    Status = "InProgress",
                    IsSubmitted = false,
                    CreatedAt = now,
                    HasDelete = false
                };

                _context.ExamAttempts.Add(examAttempt);
                await _context.SaveChangesAsync();

                // Prepare questions for response
                var questions = new List<ExamQuestionDto>();
                foreach (var examQuestion in examQuestions)
                {
                    var answerOptions = await _context.AnswerOptions
                         .Where(ao => ao.QuestionId == examQuestion.QuestionId && !ao.HasDelete)
                         .Select(ao => new AnswerOptionDto
                         {
                             OptionId = ao.OptionId,
                             Content = ao.Content,
                             IsCorrect = false, // Don't reveal correct answers
                             SequenceIndex = ao.OrderIndex
                         })
                         .OrderBy(ao => ao.SequenceIndex)
                         .ToListAsync();

                     questions.Add(new ExamQuestionDto
                     {
                         QuestionId = examQuestion.QuestionId,
                         Content = examQuestion.Question.Content,
                         QuestionType = examQuestion.Question.QuestionType,
                         Difficulty = examQuestion.Question.Difficulty,
                         Marks = examQuestion.Marks,
                         Options = answerOptions
                     });
                }

                // Randomize questions if enabled
                if (exam.RandomizeQuestions)
                {
                    questions = questions.OrderBy(x => Guid.NewGuid()).ToList();
                }

                var response = new StartExamResponse
                {
                    ExamAttemptId = examAttempt.ExamAttemptId,
                    ExamId = id,
                    ExamTitle = exam.Title,
                    VariantCode = request.VariantCode,
                    StartTime = examAttempt.StartTime,
                    EndTime = examAttempt.EndTime,
                    DurationMinutes = exam.DurationMinutes ?? 0,
                    Questions = questions,
                    TotalMarks = exam.TotalMarks ?? 0,
                    PassingMark = exam.PassingMark ?? 0,
                    Instructions = exam.Description ?? ""
                };

                return Ok(ApiResponse.SuccessResponse(response));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi bắt đầu bài thi", 500));
            }
        }

        [HttpPost("{id}/submit")]
        public async Task<IActionResult> SubmitExam(int id, [FromBody] SubmitExamRequest request)
        {
            try
            {
                // Get current user ID from UserSyncMiddleware
                var userId = HttpContext.GetSyncedUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                // Find the active exam attempt
                var examAttempt = await _context.ExamAttempts
                    .Include(ea => ea.Exam)
                    .FirstOrDefaultAsync(ea => ea.ExamId == id && ea.UserId == userId.Value && ea.Status == "InProgress");

                if (examAttempt == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy phiên thi đang diễn ra", 404));
                }

                // Check if exam time has expired
                if (examAttempt.EndTime.HasValue && DateTime.UtcNow > examAttempt.EndTime.Value)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Thời gian làm bài đã hết", 400));
                }

                var exam = examAttempt.Exam;
                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy bài thi", 404));
                }

                // ✅ Calculate time spent - Priority: Use TimeSpentSeconds from frontend (most accurate)
                var submitTime = DateTime.UtcNow;
                int timeSpent = 0;
                
                // Debug: Log request data
                _logger.LogInformation("📥 SubmitExam Request - TimeSpentSeconds: {TimeSpentSeconds}, HasValue: {HasValue}", 
                    request.TimeSpentSeconds, request.TimeSpentSeconds.HasValue);
                
                if (request.TimeSpentSeconds.HasValue && request.TimeSpentSeconds.Value > 0)
                {
                    // Use actual time spent from frontend (convert seconds to minutes)
                    // Round to nearest minute (e.g., 3 seconds = 0 minutes, 30 seconds = 1 minute, 90 seconds = 2 minutes)
                    // For very short times (< 1 minute), round up to 1 minute minimum
                    if (request.TimeSpentSeconds.Value < 60)
                    {
                        timeSpent = 1; // At least 1 minute for any attempt
                    }
                    else
                    {
                        timeSpent = (int)Math.Round(request.TimeSpentSeconds.Value / 60.0);
                    }
                    _logger.LogInformation("✅ Using TimeSpentSeconds from frontend: {Seconds}s = {Minutes}m", 
                        request.TimeSpentSeconds.Value, timeSpent);
                }
                else
                {
                    // Fallback: Calculate from StartTime to SubmittedAt (may be inaccurate if exam was left open)
                    timeSpent = (int)(submitTime - examAttempt.StartTime).TotalMinutes;
                    if (timeSpent < 0) timeSpent = 0;
                    _logger.LogWarning("⚠️ TimeSpentSeconds not provided or invalid. Using StartTime-SubmittedAt calculation (may be inaccurate): {Minutes}m. StartTime: {StartTime}, SubmitTime: {SubmitTime}", 
                        timeSpent, examAttempt.StartTime, submitTime);
                }

                // Prefer answers from Redis manual save if available
                var cached = await _progressCache.GetAllAsync(examAttempt.ExamAttemptId);
                var answersToGrade = (cached.Count > 0)
                    ? cached.Values.Select(v => new SubmittedAnswerDto
                    {
                        QuestionId = v.QuestionId,
                        SelectedOptionIds = v.SelectedOptionIds ?? new List<int>(),
                        TextAnswer = v.TextAnswer
                    }).ToList()
                    : (request.Answers ?? new List<SubmittedAnswerDto>());

                if (answersToGrade.Count == 0)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Không có dữ liệu trả lời để nộp", 400));
                }

                decimal totalScore = 0;
                var questionResults = new List<QuestionResultDto>();

                // Process each submitted answer
                foreach (var submittedAnswer in answersToGrade)
                {
                    // Get question details
                    var question = await _context.Questions
                        .Include(q => q.AnswerOptions)
                        .FirstOrDefaultAsync(q => q.QuestionId == submittedAnswer.QuestionId);

                    if (question == null) continue;

                    // Get question marks from exam
                    var examQuestion = await _context.ExamQuestions
                        .FirstOrDefaultAsync(eq => eq.ExamId == id && eq.QuestionId == submittedAnswer.QuestionId);

                    var questionMarks = examQuestion?.Marks ?? 0;
                    var earnedMarks = 0m;
                    var isCorrect = false;

                    // Create submitted answer record
                    var submittedAnswerEntity = new SubmittedAnswer
                    {
                        ExamAttemptId = examAttempt.ExamAttemptId,
                        QuestionId = submittedAnswer.QuestionId,
                        TextAnswer = submittedAnswer.TextAnswer,
                        EarnedMarks = 0, // Will be calculated below
                        IsCorrect = false, // Will be calculated below
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.SubmittedAnswers.Add(submittedAnswerEntity);
                    await _context.SaveChangesAsync(); // Save to get ID

                    // Process multiple choice answers
                    if (submittedAnswer.SelectedOptionIds?.Any() == true)
                    {
                        var correctOptions = question.AnswerOptions.Where(ao => ao.IsCorrect).ToList();
                        var selectedOptions = question.AnswerOptions.Where(ao => submittedAnswer.SelectedOptionIds.Contains(ao.OptionId)).ToList();

                        // Save selected options
                        foreach (var optionId in submittedAnswer.SelectedOptionIds)
                        {
                            var submittedOption = new SubmittedAnswerOption
                            {
                                SubmittedAnswerId = submittedAnswerEntity.SubmittedAnswerId,
                                AnswerOptionId = optionId,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.SubmittedAnswerOptions.Add(submittedOption);
                        }

                        // Check if answer is correct (all correct options selected, no incorrect options selected)
                        var correctOptionIds = correctOptions.Select(co => co.OptionId).ToHashSet();
                        var selectedOptionIds = submittedAnswer.SelectedOptionIds.ToHashSet();

                        isCorrect = correctOptionIds.SetEquals(selectedOptionIds);
                        earnedMarks = isCorrect ? questionMarks : 0;
                    }
                    // Process text answers (basic exact match for now)
                    else if (!string.IsNullOrEmpty(submittedAnswer.TextAnswer))
                    {
                        // For text questions, you might want to implement more sophisticated checking
                        // For now, we'll mark as correct if there's an answer (manual grading might be needed)
                        isCorrect = true;
                        earnedMarks = questionMarks; // Or implement your grading logic
                    }

                    // Update submitted answer with results
                    submittedAnswerEntity.IsCorrect = isCorrect;
                    submittedAnswerEntity.EarnedMarks = earnedMarks;
                    totalScore += earnedMarks;

                    // Add to results
                    questionResults.Add(new QuestionResultDto
                    {
                        QuestionId = submittedAnswer.QuestionId,
                        Content = question.Content,
                        Marks = questionMarks,
                        EarnedMarks = earnedMarks,
                        IsCorrect = isCorrect,
                        CorrectOptionIds = question.AnswerOptions.Where(ao => ao.IsCorrect).Select(ao => ao.OptionId).ToList(),
                        SelectedOptionIds = submittedAnswer.SelectedOptionIds ?? new List<int>(),
                        TextAnswer = submittedAnswer.TextAnswer
                    });
                }

                // Update exam attempt
                examAttempt.Score = totalScore;
                examAttempt.MaxScore = exam.TotalMarks ?? 0;
                examAttempt.Status = "Completed";
                examAttempt.IsSubmitted = true;
                examAttempt.SubmittedAt = submitTime;
                // ✅ Use calculated timeSpent (from TimeSpentSeconds if available, otherwise from StartTime-SubmittedAt)
                examAttempt.TimeSpentMinutes = timeSpent;

                await _context.SaveChangesAsync();

                // Cleanup cached progress
                await _progressCache.DeleteAsync(examAttempt.ExamAttemptId);

                // Calculate percentage and pass status
                var maxScore = examAttempt.MaxScore ?? 0;
                var percentage = maxScore > 0 ? (totalScore / maxScore) * 100 : 0;
                var isPassed = totalScore >= (exam.PassingMark ?? 0);

                // ✅ Use actual TimeSpentMinutes from database (recalculated above)
                var actualTimeSpent = examAttempt.TimeSpentMinutes ?? timeSpent;
                
                // ✅ Calculate TimeSpentSeconds for accurate display
                int timeSpentSeconds = 0;
                if (request.TimeSpentSeconds.HasValue && request.TimeSpentSeconds.Value > 0)
                {
                    // Use exact seconds from frontend (most accurate)
                    timeSpentSeconds = request.TimeSpentSeconds.Value;
                }
                else
                {
                    // Fallback: Calculate from StartTime to SubmittedAt
                    timeSpentSeconds = (int)(submitTime - examAttempt.StartTime).TotalSeconds;
                    if (timeSpentSeconds < 0) timeSpentSeconds = 0;
                }

                var response = new SubmitExamResponse
                {
                    ExamAttemptId = examAttempt.ExamAttemptId,
                    ExamId = id,
                    ExamTitle = exam.Title,
                    Score = totalScore,
                    MaxScore = maxScore,
                    Percentage = percentage,
                    IsPassed = isPassed,
                    StartTime = examAttempt.StartTime, // ✅ Include StartTime for accurate calculation
                    SubmittedAt = examAttempt.SubmittedAt ?? submitTime,
                    TimeSpentMinutes = actualTimeSpent,
                    TimeSpentSeconds = timeSpentSeconds, // ✅ Include TimeSpentSeconds for accurate display
                    Status = examAttempt.Status,
                    QuestionResults = questionResults
                };

                return Ok(ApiResponse.SuccessResponse(response));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi nộp bài thi", 500));
            }
        }

        [HttpGet("results/{userId}")]
        public async Task<IActionResult> GetUserExamResults(int userId)
        {
            try
            {
                // Get current user ID from UserSyncMiddleware
                var currentUserId = HttpContext.GetSyncedUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                // Check if user can access these results (either own results or admin/teacher)
                var userRole = HttpContext.GetSyncedUserRole();
                if (currentUserId.Value != userId && userRole != "Admin" && userRole != "Teacher")
                {
                    return StatusCode(403, ApiResponse.ErrorResponse("Không có quyền truy cập kết quả này", 403));
                }

                // Get user info
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy người dùng", 404));
                }

                // Get exam attempts with results
                var examAttempts = await _context.ExamAttempts
                    .Include(ea => ea.Exam)
                        .ThenInclude(e => e.Course)
                            .ThenInclude(c => c.Subject)
                    .Where(ea => ea.UserId == userId && ea.IsSubmitted)
                    .OrderByDescending(ea => ea.SubmittedAt)
                    .ToListAsync();

                var results = new List<ExamResultDto>();
                var attemptCounts = new Dictionary<int, int>();

                foreach (var attempt in examAttempts)
                {
                    // Calculate attempt number for this exam
                    if (!attemptCounts.ContainsKey(attempt.ExamId))
                    {
                        attemptCounts[attempt.ExamId] = 0;
                    }
                    attemptCounts[attempt.ExamId]++;

                    var percentage = attempt.MaxScore > 0 ? (attempt.Score / attempt.MaxScore) * 100 : 0;
                    var isPassed = attempt.Score >= (attempt.Exam?.PassingMark ?? 0);

                    // ✅ Recalculate TimeSpentMinutes from StartTime to SubmittedAt if available
                    int calculatedTimeSpent = 0;
                    if (attempt.SubmittedAt.HasValue)
                    {
                        calculatedTimeSpent = (int)(attempt.SubmittedAt.Value - attempt.StartTime).TotalMinutes;
                        if (calculatedTimeSpent < 0) calculatedTimeSpent = 0;
                    }
                    else if (attempt.TimeSpentMinutes.HasValue)
                    {
                        calculatedTimeSpent = attempt.TimeSpentMinutes.Value;
                    }

                    results.Add(new ExamResultDto
                    {
                        ExamAttemptId = attempt.ExamAttemptId,
                        ExamId = attempt.ExamId,
                        ExamTitle = attempt.Exam?.Title ?? "",
                        CourseName = attempt.Exam?.Course?.Title,
                        SubjectName = attempt.Exam?.Course?.Subject?.Name,
                        Score = attempt.Score ?? 0,
                        MaxScore = attempt.MaxScore ?? 0,
                        Percentage = percentage ?? 0,
                        IsPassed = isPassed,
                        StartTime = attempt.StartTime,
                        SubmittedAt = attempt.SubmittedAt,
                        TimeSpentMinutes = calculatedTimeSpent, // ✅ Use recalculated time
                        Status = attempt.Status,
                        AttemptNumber = attemptCounts[attempt.ExamId]
                    });
                }

                // Calculate statistics
                var statistics = new ExamResultsStatistics
                {
                    TotalExams = results.Count,
                    PassedExams = results.Count(r => r.IsPassed),
                    FailedExams = results.Count(r => !r.IsPassed),
                    AverageScore = results.Any() ? results.Average(r => r.Percentage) : 0,
                    HighestScore = results.Any() ? results.Max(r => r.Percentage) : 0,
                    LowestScore = results.Any() ? results.Min(r => r.Percentage) : 0,
                    PassRate = results.Any() ? (double)results.Count(r => r.IsPassed) / results.Count * 100 : 0
                };

                var response = new UserExamResultsResponse
                {
                    UserId = userId,
                    UserName = user.FullName ?? user.Email,
                    Results = results,
                    Statistics = statistics
                };

                return Ok(ApiResponse.SuccessResponse(response));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy kết quả thi", 500));
            }
        }

        [HttpGet("{id}/ranking")]
        public async Task<IActionResult> GetExamRanking(int id)
        {
            try
            {
                // Get exam info
                var exam = await _context.Exams
                    .Include(e => e.Course)
                        .ThenInclude(c => c.Subject)
                    .FirstOrDefaultAsync(e => e.ExamId == id);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy bài thi", 404));
                }

                // Get all completed exam attempts for this exam
                var examAttempts = await _context.ExamAttempts
                    .Include(ea => ea.User)
                    .Where(ea => ea.ExamId == id && ea.IsSubmitted)
                    .OrderByDescending(ea => ea.Score)
                    .ThenBy(ea => ea.TimeSpentMinutes)
                    .ThenBy(ea => ea.SubmittedAt)
                    .ToListAsync();

                // Group by user to get best attempt for each user
                var bestAttempts = examAttempts
                    .GroupBy(ea => ea.UserId)
                    .Select(g => g.OrderByDescending(ea => ea.Score)
                                  .ThenBy(ea => ea.TimeSpentMinutes)
                                  .ThenBy(ea => ea.SubmittedAt)
                                  .First())
                    .OrderByDescending(ea => ea.Score)
                    .ThenBy(ea => ea.TimeSpentMinutes)
                    .ThenBy(ea => ea.SubmittedAt)
                    .ToList();

                var rankings = new List<RankingEntryDto>();
                var rank = 1;

                foreach (var attempt in bestAttempts)
                {
                    var percentage = attempt.MaxScore > 0 ? (attempt.Score / attempt.MaxScore) * 100 : 0;
                    
                    // Count attempts for this user
                    var userAttemptCount = examAttempts.Count(ea => ea.UserId == attempt.UserId);

                    rankings.Add(new RankingEntryDto
                    {
                        Rank = rank++,
                        UserId = attempt.UserId,
                        UserName = attempt.User?.FullName ?? attempt.User?.Email ?? "Unknown",
                        UserEmail = attempt.User?.Email,
                        Score = attempt.Score ?? 0,
                        MaxScore = attempt.MaxScore ?? 0,
                        Percentage = percentage ?? 0,
                        SubmittedAt = attempt.SubmittedAt ?? DateTime.UtcNow,
                        TimeSpentMinutes = attempt.TimeSpentMinutes ?? 0,
                        AttemptNumber = userAttemptCount
                    });
                }

                // Calculate statistics
                var scores = rankings.Select(r => r.Percentage).ToList();
                var statistics = new RankingStatistics
                {
                    TotalParticipants = rankings.Count,
                    AverageScore = scores.Any() ? scores.Average() : 0,
                    HighestScore = scores.Any() ? scores.Max() : 0,
                    LowestScore = scores.Any() ? scores.Min() : 0,
                    PassRate = rankings.Any() ? (double)rankings.Count(r => r.Score >= (exam.PassingMark ?? 0)) / rankings.Count * 100 : 0,
                    MedianScore = scores.Any() ? CalculateMedian(scores) : 0
                };

                var response = new ExamRankingResponse
                {
                    ExamId = id,
                    ExamTitle = exam.Title,
                    CourseName = exam.Course?.Title,
                    SubjectName = exam.Course?.Subject?.Name,
                    Rankings = rankings,
                    Statistics = statistics
                };

                return Ok(ApiResponse.SuccessResponse(response));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy bảng xếp hạng", 500));
            }
        }

        private decimal CalculateMedian(List<decimal> values)
        {
            if (!values.Any()) return 0;

            var sorted = values.OrderBy(x => x).ToList();
            var count = sorted.Count;

            if (count % 2 == 0)
            {
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
            }
            else
            {
                return sorted[count / 2];
            }
        }

        private decimal CalculateStandardDeviation(List<decimal> values)
        {
            if (!values.Any()) return 0;
            
            var average = values.Average();
            var sumOfSquares = values.Sum(x => (double)Math.Pow((double)(x - average), 2));
            var variance = sumOfSquares / values.Count;
            
            return (decimal)Math.Sqrt(variance);
        }

        /// <summary>
        /// Demo User Sync - Lấy thông tin user hiện tại từ middleware
        /// </summary>
        [HttpGet("user-sync-demo")]
        [Authorize]
        public IActionResult GetUserSyncDemo()
        {
            try
            {
                // Sử dụng HttpContext Extension từ middleware
                var syncedUser = HttpContext.GetSyncedUser();
                var userId = HttpContext.GetSyncedUserId();
                var userRole = HttpContext.GetSyncedUserRole();

                if (syncedUser == null)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("User not found or invalid token", 401));
                }

                _logger.LogInformation($"User {syncedUser.FullName} ({syncedUser.Email}) is accessing user sync demo");

                return Ok(ApiResponse.SuccessResponse(new
                {
                    Message = "User sync demo - Thông tin user được đồng bộ từ AuthService",
                    ServiceName = "ExamsService (Port 5002)",
                    SyncedUser = syncedUser,
                    Permissions = new
                    {
                        IsAdmin = HttpContext.IsAdmin(),
                        IsTeacher = HttpContext.IsTeacher(),
                        IsStudent = HttpContext.IsStudent()
                    },
                    AccessTime = DateTime.UtcNow
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in user sync demo");
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống", 500));
            }
        }

        /// <summary>
        /// Demo User Sync - Kiểm tra quyền truy cập theo role
        /// </summary>
        [HttpGet("role-check-demo")]
        [Authorize]
        public async Task<IActionResult> GetRoleCheckDemo()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Missing or invalid authorization header", 401));
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();
                var user = await _userSyncService.GetUserFromTokenAsync(token);

                if (user == null)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Invalid token or user not found", 401));
                }

                // Kiểm tra quyền cho từng role
                var adminPermission = await _userSyncService.ValidateUserPermissionAsync(user.UserId, "Admin");
                var teacherPermission = await _userSyncService.ValidateUserPermissionAsync(user.UserId, "Teacher");
                var studentPermission = await _userSyncService.ValidateUserPermissionAsync(user.UserId, "Student");

                _logger.LogInformation($"Role check for user {user.FullName}: Admin={adminPermission}, Teacher={teacherPermission}, Student={studentPermission}");

                return Ok(ApiResponse.SuccessResponse(new
                {
                    Message = "Role check demo - Kiểm tra quyền từ AuthService",
                    ServiceName = "ExamsService (Port 5002)",
                    User = user,
                    RolePermissions = new
                    {
                        HasAdminPermission = adminPermission,
                        HasTeacherPermission = teacherPermission,
                        HasStudentPermission = studentPermission
                    },
                    CurrentRole = user.RoleName,
                    CheckTime = DateTime.UtcNow
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in role check demo");
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống", 500));
            }
        }

        /// <summary>
        /// Lấy danh sách điểm thi của học viên cho một bài thi cụ thể
        /// </summary>
        [HttpGet("exam-results/{examId}")]
        public async Task<IActionResult> GetExamResults(int examId)
        {
            try
            {
                // Check if exam exists
                var exam = await _context.Exams
                    .Include(e => e.Course)
                        .ThenInclude(c => c!.Subject)
                    .FirstOrDefaultAsync(e => e.ExamId == examId && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy bài thi", 404));
                }

                // Get all completed exam attempts for this exam
                var examAttempts = await _context.ExamAttempts
                    .Include(ea => ea.User)
                    .Where(ea => ea.ExamId == examId && ea.IsSubmitted && !ea.HasDelete)
                    .ToListAsync();

                // Group by user to get best attempt for each user
                var bestAttempts = examAttempts
                    .GroupBy(ea => ea.UserId)
                    .Select(g => g.OrderByDescending(ea => ea.Score)
                                  .ThenBy(ea => ea.TimeSpentMinutes)
                                  .ThenBy(ea => ea.SubmittedAt)
                                  .First())
                    .OrderByDescending(ea => ea.Score)
                    .ToList();

                // Create student scores list
                var studentScores = bestAttempts.Select((attempt, index) => new StudentScoreDto
                {
                    UserId = attempt.UserId,
                    UserName = attempt.User?.FullName ?? attempt.User?.Email ?? "Unknown",
                    UserEmail = attempt.User?.Email,
                    Score = attempt.Score ?? 0,
                    MaxScore = exam.TotalMarks ?? 0,
                    Percentage = exam.TotalMarks > 0 ? (decimal)((attempt.Score ?? 0) / exam.TotalMarks * 100) : 0,
                    IsPassed = (attempt.Score ?? 0) >= (exam.PassingMark ?? 0),
                    SubmittedAt = attempt.SubmittedAt ?? DateTime.UtcNow,
                    TimeSpentMinutes = attempt.TimeSpentMinutes ?? 0,
                    AttemptNumber = examAttempts.Count(ea => ea.UserId == attempt.UserId)
                }).ToList();

                // Calculate statistics
                var scores = studentScores.Select(s => s.Score).ToList();
                var percentages = studentScores.Select(s => s.Percentage).ToList();

                var statistics = new ExamScoreStatistics
                {
                    TotalStudents = studentScores.Count,
                    PassedStudents = studentScores.Count(s => s.IsPassed),
                    FailedStudents = studentScores.Count(s => !s.IsPassed),
                    AverageScore = scores.Any() ? scores.Average() : 0,
                    HighestScore = scores.Any() ? scores.Max() : 0,
                    LowestScore = scores.Any() ? scores.Min() : 0,
                    PassRate = studentScores.Any() ? (double)studentScores.Count(s => s.IsPassed) / studentScores.Count * 100 : 0,
                    MedianScore = CalculateMedian(scores),
                    StandardDeviation = CalculateStandardDeviation(scores)
                };

                var response = new ExamResultsSummaryDto
                {
                    ExamId = examId,
                    ExamTitle = exam.Title,
                    CourseName = exam.Course?.Title,
                    SubjectName = exam.Course?.Subject?.Name,
                    PassingMark = exam.PassingMark,
                    StudentScores = studentScores,
                    Statistics = statistics
                };

                return Ok(ApiResponse.SuccessResponse(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exam results for exam {ExamId}", examId);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy kết quả thi", 500));
            }
        }

        /// <summary>
        /// Lấy danh sách lịch thi cá nhân mà học sinh đã đăng ký/đã tham gia
        /// (Tạm thời dựa trên các ExamAttempts của người dùng trong ExamsService)
        /// </summary>
        [HttpGet("my-schedule")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMySchedule()
        {
            try
            {
                var userId = HttpContext.GetSyncedUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                var now = DateTime.UtcNow;

                var attempts = await _context.ExamAttempts
                    .Include(ea => ea.Exam)
                        .ThenInclude(e => e.Course)
                            .ThenInclude(c => c.Subject)
                    .Where(ea => ea.UserId == userId.Value && !ea.HasDelete)
                    .OrderByDescending(ea => ea.StartTime)
                    .ToListAsync();

                var items = new List<MyScheduleItemDto>();

                foreach (var ea in attempts)
                {
                    var exam = ea.Exam;
                    if (exam == null)
                    {
                        continue;
                    }

                    var attemptStatus = ea.IsSubmitted ? "Completed"
                                      : (string.Equals(ea.Status, "InProgress", StringComparison.OrdinalIgnoreCase) ? "InProgress" : "NotStarted");

                    string scheduleStatus;
                    if (attemptStatus == "Completed")
                    {
                        scheduleStatus = "Completed";
                    }
                    else if (exam.StartAt.HasValue && now < exam.StartAt.Value)
                    {
                        scheduleStatus = "Upcoming";
                    }
                    else if (exam.EndAt.HasValue && now > exam.EndAt.Value)
                    {
                        scheduleStatus = "Expired";
                    }
                    else
                    {
                        scheduleStatus = "Ongoing";
                    }

                    items.Add(new MyScheduleItemDto
                    {
                        ExamId = exam.ExamId,
                        ExamTitle = exam.Title,
                        CourseId = exam.CourseId,
                        CourseName = exam.Course?.Title,
                        SubjectId = exam.Course?.SubjectId,
                        SubjectName = exam.Course?.Subject?.Name,
                        StartAt = exam.StartAt,
                        EndAt = exam.EndAt,
                        Status = scheduleStatus,
                        AttemptStatus = attemptStatus,
                        Score = ea.Score,
                        AttemptStart = ea.StartTime,
                        AttemptEnd = ea.EndTime,
                        CompletedAt = ea.SubmittedAt
                    });
                }

                var response = new MyScheduleResponse
                {
                    UserId = userId.Value,
                    Items = items
                };

                return Ok(ApiResponse.SuccessResponse(response, "Lấy lịch thi cá nhân thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting personal exam schedule");
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy lịch thi", 500));
            }
        }

        /// <summary>
        /// Phân tích kết quả thi - tính toán tỉ lệ đúng/sai, câu hỏi khó
        /// </summary>
        [HttpGet("exam-results/{examId}/analysis")]
        public async Task<IActionResult> GetExamAnalysis(int examId)
        {
            try
            {
                // Check if exam exists
                var exam = await _context.Exams
                    .FirstOrDefaultAsync(e => e.ExamId == examId && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy bài thi", 404));
                }

                // Get exam questions
                var examQuestions = await _context.ExamQuestions
                    .Include(eq => eq.Question)
                        .ThenInclude(q => q.AnswerOptions)
                    .Where(eq => eq.ExamId == examId && !eq.HasDelete && !eq.Question.HasDelete)
                    .ToListAsync();

                // Get all exam attempts for this exam that are submitted
                var submittedExamAttempts = await _context.ExamAttempts
                    .Where(ea => ea.ExamId == examId)
                    .Where(ea => ea.IsSubmitted == true)
                    .ToListAsync();
                
                var examAttemptIds = submittedExamAttempts.Select(ea => ea.ExamAttemptId).ToList();

                Console.WriteLine($"Found {examAttemptIds.Count} exam attempts for exam {examId}");

                // Check if we have any exam attempts
                if (!examAttemptIds.Any())
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "No submitted exam attempts found for this exam",
                        Data = new
                        {
                            ExamId = examId,
                            TotalAttempts = 0,
                            Questions = new List<object>()
                        }
                    });
                }

                // Get all submitted answers for these exam attempts
                var submittedAnswers = new List<SubmittedAnswer>();
                if (examAttemptIds.Any())
                {
                    // Get submitted answers by querying each exam attempt individually to avoid SQL syntax issues
                    foreach (var attemptId in examAttemptIds)
                    {
                        var answersForAttempt = await _context.SubmittedAnswers
                            .Where(sa => sa.ExamAttemptId == attemptId && sa.HasDelete == false)
                            .ToListAsync();
                        submittedAnswers.AddRange(answersForAttempt);
                    }
                }

                // Get submitted answer options by querying each submitted answer individually
                Console.WriteLine($"Found {submittedAnswers.Count} submitted answers");
                
                var submittedAnswerOptions = new List<SubmittedAnswerOption>();
                
                if (submittedAnswers.Any())
                {
                    Console.WriteLine("Querying SubmittedAnswerOptions individually...");
                    foreach (var submittedAnswer in submittedAnswers)
                    {
                        var optionsForAnswer = await _context.SubmittedAnswerOptions
                            .Where(sao => sao.SubmittedAnswerId == submittedAnswer.SubmittedAnswerId)
                            .ToListAsync();
                        submittedAnswerOptions.AddRange(optionsForAnswer);
                    }
                    Console.WriteLine($"Found {submittedAnswerOptions.Count} submitted answer options");
                }
                else
                {
                    Console.WriteLine("No submitted answers found, skipping SubmittedAnswerOptions query");
                }

                // Analyze each question
                var questionAnalysis = new List<QuestionAnalysisDto>();
                
                foreach (var examQuestion in examQuestions)
                {
                    var question = examQuestion.Question;
                    var questionAnswers = submittedAnswers.Where(sa => sa.QuestionId == question.QuestionId).ToList();
                    
                    var totalAttempts = questionAnswers.Count;
                    var correctAnswers = questionAnswers.Count(sa => sa.IsCorrect);
                    var incorrectAnswers = totalAttempts - correctAnswers;
                    
                    var correctPercentage = totalAttempts > 0 ? (double)correctAnswers / totalAttempts * 100 : 0;
                    
                    // Determine difficulty level based on correct percentage
                    string difficultyLevel = correctPercentage >= 70 ? "Easy" : 
                                           correctPercentage >= 40 ? "Medium" : "Hard";

                    // Analyze answer options
                    var optionAnalysis = question.AnswerOptions.Select(option => {
                        // Get submitted answer IDs for this question
                        var questionSubmittedAnswerIds = questionAnswers.Select(qa => qa.SubmittedAnswerId).ToList();
                        
                        // Count how many times this option was selected
                        var optionSelections = submittedAnswerOptions
                            .Where(sao => questionSubmittedAnswerIds.Contains(sao.SubmittedAnswerId) && sao.AnswerOptionId == option.OptionId)
                            .Count();
                            
                        var selectionPercentage = totalAttempts > 0 ? (double)optionSelections / totalAttempts * 100 : 0;
                        
                        return new OptionAnalysisDto
                        {
                            OptionId = option.OptionId,
                            OptionContent = option.Content,
                            IsCorrect = option.IsCorrect,
                            SelectionCount = optionSelections,
                            SelectionPercentage = selectionPercentage
                        };
                    }).ToList();

                    questionAnalysis.Add(new QuestionAnalysisDto
                    {
                        QuestionId = question.QuestionId,
                        QuestionContent = question.Content,
                        Difficulty = question.Difficulty,
                        Marks = examQuestion.Marks ?? question.Marks ?? 0,
                        TotalAttempts = totalAttempts,
                        CorrectAnswers = correctAnswers,
                        IncorrectAnswers = incorrectAnswers,
                        CorrectPercentage = correctPercentage,
                        IncorrectPercentage = 100 - correctPercentage,
                        DifficultyLevel = difficultyLevel,
                        OptionAnalysis = optionAnalysis
                    });
                }

                // Calculate difficulty analysis
                var difficultyAnalysis = new ExamDifficultyAnalysis
                {
                    EasyQuestions = questionAnalysis.Count(q => q.DifficultyLevel == "Easy"),
                    MediumQuestions = questionAnalysis.Count(q => q.DifficultyLevel == "Medium"),
                    HardQuestions = questionAnalysis.Count(q => q.DifficultyLevel == "Hard"),
                    AverageCorrectPercentage = questionAnalysis.Any() ? questionAnalysis.Average(q => q.CorrectPercentage) : 0,
                    MostDifficultQuestions = questionAnalysis
                        .OrderBy(q => q.CorrectPercentage)
                        .Take(5)
                        .Select(q => q.QuestionContent.Length > 100 ? q.QuestionContent.Substring(0, 100) + "..." : q.QuestionContent)
                        .ToList(),
                    EasiestQuestions = questionAnalysis
                        .OrderByDescending(q => q.CorrectPercentage)
                        .Take(5)
                        .Select(q => q.QuestionContent.Length > 100 ? q.QuestionContent.Substring(0, 100) + "..." : q.QuestionContent)
                        .ToList()
                };

                // Create score distribution chart data
                var examAttempts = await _context.ExamAttempts
                    .Where(ea => ea.ExamId == examId && ea.IsSubmitted && !ea.HasDelete)
                    .ToListAsync();

                var scoreRanges = new[] { "0-20%", "21-40%", "41-60%", "61-80%", "81-100%" };
                var scoreDistributionChart = scoreRanges.Select((string range) => {
                    var (min, max) = range switch
                    {
                        "0-20%" => (0, 20),
                        "21-40%" => (21, 40),
                        "41-60%" => (41, 60),
                        "61-80%" => (61, 80),
                        "81-100%" => (81, 100),
                        _ => (0, 0)
                    };
                    
                    var count = examAttempts.Count((ExamAttempt ea) => {
                        var percentage = exam.TotalMarks > 0 ? (ea.Score ?? 0) / exam.TotalMarks * 100 : 0;
                        return percentage >= min && percentage <= max;
                    });
                    
                    return new ChartDataDto
                    {
                        Label = range,
                        Value = count,
                        Color = range switch
                        {
                            "0-20%" => "#ff4444",
                            "21-40%" => "#ff8800",
                            "41-60%" => "#ffbb33",
                            "61-80%" => "#00C851",
                            "81-100%" => "#007E33",
                            _ => "#cccccc"
                        }
                    };
                }).ToList();

                // Create question difficulty chart data
                var questionDifficultyChart = new List<ChartDataDto>
                {
                    new ChartDataDto { Label = "Dễ", Value = difficultyAnalysis.EasyQuestions, Color = "#00C851" },
                    new ChartDataDto { Label = "Trung bình", Value = difficultyAnalysis.MediumQuestions, Color = "#ffbb33" },
                    new ChartDataDto { Label = "Khó", Value = difficultyAnalysis.HardQuestions, Color = "#ff4444" }
                };

                var response = new ExamAnalysisDto
                {
                    ExamId = examId,
                    ExamTitle = exam.Title,
                    QuestionAnalysis = questionAnalysis,
                    DifficultyAnalysis = difficultyAnalysis,
                    ScoreDistributionChart = scoreDistributionChart,
                    QuestionDifficultyChart = questionDifficultyChart
                };

                return Ok(ApiResponse.SuccessResponse(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exam analysis for exam {ExamId}", examId);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi phân tích kết quả thi", 500));
            }
        }

        /// <summary>
        /// Tải chứng chỉ hoàn thành bài thi
        /// </summary>
        [HttpGet("certificates/{userId}/{examId}")]
        public async Task<IActionResult> GetCertificate(int userId, int examId)
        {
            try
            {
                // Get current user for authorization
                var currentUserId = HttpContext.GetSyncedUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                var userRole = HttpContext.GetSyncedUserRole();
                
                // Check if user can access this certificate (own certificate or admin/teacher)
                if (currentUserId.Value != userId && userRole != "Admin" && userRole != "Teacher")
                {
                    return StatusCode(403, ApiResponse.ErrorResponse("Không có quyền truy cập chứng chỉ này", 403));
                }

                // Check if exam exists
                var exam = await _context.Exams
                    .Include(e => e.Course)
                        .ThenInclude(c => c!.Subject)
                    .FirstOrDefaultAsync(e => e.ExamId == examId && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy bài thi", 404));
                }

                // Check if user exists
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy người dùng", 404));
                }

                // Get best exam attempt for this user
                var bestAttempt = await _context.ExamAttempts
                    .Where(ea => ea.ExamId == examId && ea.UserId == userId && ea.IsSubmitted && !ea.HasDelete)
                    .OrderByDescending(ea => ea.Score)
                    .ThenBy(ea => ea.TimeSpentMinutes)
                    .ThenBy(ea => ea.SubmittedAt)
                    .FirstOrDefaultAsync();

                if (bestAttempt == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Người dùng chưa hoàn thành bài thi này", 404));
                }

                // Check if user passed the exam
                var score = bestAttempt.Score ?? 0;
                var maxScore = exam.TotalMarks ?? 0;
                var percentage = maxScore > 0 ? score / maxScore * 100 : 0;
                var passingMark = exam.PassingMark ?? 0;
                var isPassed = score >= passingMark;

                if (!isPassed)
                {
                    var failureResponse = new CertificateGenerationResponse
                    {
                        IsEligible = false,
                        Message = $"Điểm số ({score}/{maxScore}) chưa đạt chuẩn đầu ra ({passingMark}). Không thể tạo chứng chỉ.",
                        Certificate = null
                    };
                    return Ok(ApiResponse.SuccessResponse(failureResponse));
                }

                // Generate certificate ID
                var certificateId = $"CERT-{examId}-{userId}-{bestAttempt.SubmittedAt:yyyyMMdd}";
                
                // In a real implementation, you would generate a PDF here
                // For now, we'll return a mock download URL
                var downloadUrl = $"/api/certificates/download/{certificateId}.pdf";

                var certificate = new CertificateDto
                {
                    UserId = userId,
                    UserName = user.FullName ?? user.Email,
                    UserEmail = user.Email,
                    ExamId = examId,
                    ExamTitle = exam.Title,
                    CourseName = exam.Course?.Title,
                    SubjectName = exam.Course?.Subject?.Name,
                    Score = score,
                    MaxScore = maxScore,
                    Percentage = percentage,
                    PassingMark = passingMark,
                    IsPassed = isPassed,
                    CompletedAt = bestAttempt.SubmittedAt ?? DateTime.UtcNow,
                    CertificateId = certificateId,
                    DownloadUrl = downloadUrl,
                    IssuedAt = DateTime.UtcNow
                };

                var response = new CertificateGenerationResponse
                {
                    IsEligible = true,
                    Message = "Chứng chỉ đã được tạo thành công",
                    Certificate = certificate
                };

                return Ok(ApiResponse.SuccessResponse(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating certificate for user {UserId} and exam {ExamId}", userId, examId);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi tạo chứng chỉ", 500));
            }
        }


    }
}