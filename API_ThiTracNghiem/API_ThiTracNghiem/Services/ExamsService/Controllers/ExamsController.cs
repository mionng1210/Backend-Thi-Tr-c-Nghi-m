using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamsService.Data;
using ExamsService.DTOs;
using ExamsService.Models;
using System.Security.Claims;

namespace ExamsService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExamsController : ControllerBase
    {
        private readonly ExamsDbContext _context;

        public ExamsController(ExamsDbContext context)
        {
            _context = context;
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
                        CreatedAt = e.CreatedAt
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
    }
}