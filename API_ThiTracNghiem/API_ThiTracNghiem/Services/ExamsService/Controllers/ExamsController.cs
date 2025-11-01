using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamsService.Data;
using ExamsService.DTOs;
using ExamsService.Models;
using System.Security.Claims;
using API_ThiTracNghiem.Services;
using API_ThiTracNghiem.Middleware;

namespace ExamsService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExamsController : ControllerBase
    {
        private readonly ExamsDbContext _context;
        private readonly IUserSyncService _userSyncService;
        private readonly ILogger<ExamsController> _logger;

        public ExamsController(ExamsDbContext context, IUserSyncService userSyncService, ILogger<ExamsController> logger)
        {
            _context = context;
            _userSyncService = userSyncService;
            _logger = logger;
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
                        CreatedByName = e.Creator != null ? e.Creator.FullName : null
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
                    Questions = questions
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
                var teacherId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                // Validate course exists if provided
                if (request.CourseId.HasValue && !await _context.Courses.AnyAsync(c => c.CourseId == request.CourseId.Value && !c.HasDelete))
                {
                    return BadRequest(ApiResponse.ErrorResponse("Course không tồn tại", 400));
                }

                var exam = new Exam
                {
                    Title = request.Title,
                    Description = request.Description,
                    CourseId = request.CourseId,
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
                    CreatedBy = teacherId,
                    CreatedAt = DateTime.UtcNow,
                    HasDelete = false
                };

                _context.Exams.Add(exam);
                await _context.SaveChangesAsync();

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
                    Questions = new List<ExamQuestionDto>()
                };

                return CreatedAtAction(nameof(GetExam), new { id = exam.ExamId }, ApiResponse.SuccessResponse(createdExamDetail));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message, 400));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi tạo bài thi", 500));
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
                    .FirstOrDefaultAsync(e => e.ExamId == id && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Bài thi không tồn tại", 404));
                }

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

                await _context.SaveChangesAsync();

                return Ok(ApiResponse.SuccessResponse(null, "Cập nhật bài thi thành công"));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi cập nhật bài thi", 500));
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
                var exam = await _context.Exams
                    .FirstOrDefaultAsync(e => e.ExamId == id && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Bài thi không tồn tại", 404));
                }

                // Soft delete exam
                exam.HasDelete = true;

                // Delete related exam questions (hard delete since ExamQuestion doesn't have HasDelete)
                var examQuestions = await _context.ExamQuestions
                    .Where(eq => eq.ExamId == id)
                    .ToListAsync();
                
                _context.ExamQuestions.RemoveRange(examQuestions);

                await _context.SaveChangesAsync();

                return Ok(ApiResponse.SuccessResponse(null, "Xóa bài thi thành công"));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi xóa bài thi", 500));
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
                var exam = await _context.Exams
                    .FirstOrDefaultAsync(e => e.ExamId == id && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Bài thi không tồn tại", 404));
                }

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
                // Check if exam exists
                var exam = await _context.Exams.FirstOrDefaultAsync(e => e.ExamId == examId && !e.HasDelete);
                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy bài thi", 404));
                }

                // Validate question IDs exist in question bank
                var existingQuestions = new List<Question>();
                foreach (var questionId in request.QuestionIds)
                {
                    var question = await _context.Questions
                        .FirstOrDefaultAsync(q => q.QuestionId == questionId && !q.HasDelete);
                    if (question != null)
                    {
                        existingQuestions.Add(question);
                    }
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
                            SequenceIndex = examQuestion.SequenceIndex
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

                    exam.TotalQuestions = totalQuestions;
                    exam.TotalMarks = totalMarks;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(ApiResponse.SuccessResponse(new 
                    { 
                        AddedQuestions = addedQuestions,
                        ExamSummary = new
                        {
                            ExamId = examId,
                            TotalQuestions = totalQuestions,
                            TotalMarks = totalMarks
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

                // Calculate time spent
                var timeSpent = (int)(DateTime.UtcNow - examAttempt.StartTime).TotalMinutes;

                decimal totalScore = 0;
                var questionResults = new List<QuestionResultDto>();

                // Process each submitted answer
                foreach (var submittedAnswer in request.Answers)
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
                examAttempt.SubmittedAt = DateTime.UtcNow;
                examAttempt.TimeSpentMinutes = timeSpent;

                await _context.SaveChangesAsync();

                // Calculate percentage and pass status
                var maxScore = examAttempt.MaxScore ?? 0;
                var percentage = maxScore > 0 ? (totalScore / maxScore) * 100 : 0;
                var isPassed = totalScore >= (exam.PassingMark ?? 0);

                var response = new SubmitExamResponse
                {
                    ExamAttemptId = examAttempt.ExamAttemptId,
                    ExamId = id,
                    ExamTitle = exam.Title,
                    Score = totalScore,
                    MaxScore = maxScore,
                    Percentage = percentage,
                    IsPassed = isPassed,
                    SubmittedAt = examAttempt.SubmittedAt ?? DateTime.UtcNow,
                    TimeSpentMinutes = timeSpent,
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
                        TimeSpentMinutes = attempt.TimeSpentMinutes ?? 0,
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
    }
}