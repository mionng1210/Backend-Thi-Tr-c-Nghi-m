using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using ExamsService.Data;
using ExamsService.Models;
using ExamsService.DTOs;
using System.Security.Claims;

namespace ExamsService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LessonsController : ControllerBase
    {
        private readonly ExamsDbContext _context;
        private readonly ILogger<LessonsController> _logger;

        public LessonsController(ExamsDbContext context, ILogger<LessonsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách bài học theo courseId
        /// </summary>
        [HttpGet("by-course/{courseId}")]
        [Authorize]
        public async Task<IActionResult> GetLessonsByCourseId(int courseId)
        {
            try
            {
                var lessons = await _context.Lessons
                    .Where(l => l.CourseId == courseId && !l.HasDelete)
                    .OrderBy(l => l.OrderIndex ?? int.MaxValue)
                    .ThenBy(l => l.CreatedAt)
                    .Select(l => new LessonListItemDto
                    {
                        LessonId = l.LessonId,
                        CourseId = l.CourseId,
                        Title = l.Title,
                        Description = l.Description,
                        Content = l.Content,
                        Type = l.Type,
                        VideoUrl = l.VideoUrl,
                        ContentUrl = l.ContentUrl,
                        DurationSeconds = l.DurationSeconds,
                        OrderIndex = l.OrderIndex,
                        IsFree = l.IsFree,
                        CreatedAt = l.CreatedAt,
                        UpdatedAt = l.UpdatedAt
                    })
                    .ToListAsync();

                // Load questions for each lesson
                foreach (var lesson in lessons)
                {
                    var lessonQuestions = await _context.LessonQuestions
                        .Where(lq => lq.LessonId == lesson.LessonId && !lq.HasDelete)
                        .OrderBy(lq => lq.SequenceIndex ?? lq.LessonQuestionId)
                        .ToListAsync();

                    var questionDtos = new List<LessonQuestionDto>();
                    foreach (var lq in lessonQuestions)
                    {
                        var question = await _context.Questions
                            .Where(q => q.QuestionId == lq.QuestionId && !q.HasDelete)
                            .FirstOrDefaultAsync();

                        if (question != null)
                        {
                            var answerOptions = await _context.AnswerOptions
                                .Where(ao => ao.QuestionId == question.QuestionId && !ao.HasDelete)
                                .OrderBy(ao => ao.OrderIndex ?? ao.OptionId)
                                .Select(ao => new DTOs.AnswerOptionDto
                                {
                                    OptionId = ao.OptionId,
                                    Content = ao.Content,
                                    IsCorrect = ao.IsCorrect,
                                    SequenceIndex = ao.OrderIndex
                                })
                                .ToListAsync();

                            questionDtos.Add(new LessonQuestionDto
                            {
                                QuestionId = question.QuestionId,
                                Content = question.Content,
                                QuestionType = question.QuestionType,
                                Difficulty = question.Difficulty,
                                Marks = question.Marks,
                                SequenceIndex = lq.SequenceIndex,
                                Options = answerOptions
                            });
                        }
                    }

                    lesson.Questions = questionDtos;
                }

                return Ok(new { message = "Lấy danh sách bài học thành công", data = lessons });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting lessons for course {CourseId}", courseId);
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy chi tiết bài học theo ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetLessonById(int id)
        {
            try
            {
                var lesson = await _context.Lessons
                    .Where(l => l.LessonId == id && !l.HasDelete)
                    .Select(l => new LessonListItemDto
                    {
                        LessonId = l.LessonId,
                        CourseId = l.CourseId,
                        Title = l.Title,
                        Description = l.Description,
                        Content = l.Content,
                        Type = l.Type,
                        VideoUrl = l.VideoUrl,
                        ContentUrl = l.ContentUrl,
                        DurationSeconds = l.DurationSeconds,
                        OrderIndex = l.OrderIndex,
                        IsFree = l.IsFree,
                        CreatedAt = l.CreatedAt,
                        UpdatedAt = l.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (lesson == null)
                {
                    return NotFound(new { message = "Không tìm thấy bài học" });
                }

                // Load questions for this lesson
                var lessonQuestions = await _context.LessonQuestions
                    .Where(lq => lq.LessonId == lesson.LessonId && !lq.HasDelete)
                    .OrderBy(lq => lq.SequenceIndex ?? lq.LessonQuestionId)
                    .ToListAsync();

                var questionDtos = new List<LessonQuestionDto>();
                foreach (var lq in lessonQuestions)
                {
                    var question = await _context.Questions
                        .Where(q => q.QuestionId == lq.QuestionId && !q.HasDelete)
                        .FirstOrDefaultAsync();

                    if (question != null)
                    {
                        var answerOptions = await _context.AnswerOptions
                            .Where(ao => ao.QuestionId == question.QuestionId && !ao.HasDelete)
                            .OrderBy(ao => ao.OrderIndex ?? ao.OptionId)
                            .Select(ao => new AnswerOptionDto
                            {
                                OptionId = ao.OptionId,
                                Content = ao.Content,
                                IsCorrect = ao.IsCorrect,
                                SequenceIndex = ao.OrderIndex
                            })
                            .ToListAsync();

                        questionDtos.Add(new LessonQuestionDto
                        {
                            QuestionId = question.QuestionId,
                            Content = question.Content,
                            QuestionType = question.QuestionType,
                            Difficulty = question.Difficulty,
                            Marks = question.Marks,
                            SequenceIndex = lq.SequenceIndex,
                            Options = answerOptions
                        });
                    }
                }

                lesson.Questions = questionDtos;

                return Ok(new { message = "Lấy thông tin bài học thành công", data = lesson });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting lesson {LessonId}", id);
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }

        /// <summary>
        /// Tạo bài học mới
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> CreateLesson([FromBody] CreateLessonRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });
            }

            try
            {
                // Kiểm tra course tồn tại
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.CourseId == request.CourseId && !c.HasDelete);

                if (course == null)
                {
                    return NotFound(new { message = "Khóa học không tồn tại" });
                }

                // Kiểm tra quyền: Teacher chỉ có thể thêm bài học vào khóa học của mình
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { message = "Không tìm thấy thông tin người dùng" });
                }

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != "Admin" && course.TeacherId != userId)
                {
                    return Forbid("Bạn không có quyền thêm bài học vào khóa học này");
                }

                var lesson = new Lesson
                {
                    CourseId = request.CourseId,
                    Title = request.Title,
                    Description = request.Description,
                    Content = request.Content,
                    Type = request.Type ?? "video",
                    VideoUrl = request.VideoUrl,
                    ContentUrl = request.ContentUrl,
                    DurationSeconds = request.DurationSeconds,
                    OrderIndex = request.OrderIndex,
                    IsFree = request.IsFree,
                    CreatedAt = DateTime.UtcNow,
                    HasDelete = false
                };

                _context.Lessons.Add(lesson);
                await _context.SaveChangesAsync();

                // Lưu relationship với questions nếu có
                if (request.QuestionIds != null && request.QuestionIds.Count > 0)
                {
                    var lessonQuestions = new List<LessonQuestion>();
                    for (int i = 0; i < request.QuestionIds.Count; i++)
                    {
                        var questionId = request.QuestionIds[i];
                        // Kiểm tra question tồn tại
                        var questionExists = await _context.Questions
                            .AnyAsync(q => q.QuestionId == questionId && !q.HasDelete);
                        
                        if (questionExists)
                        {
                            lessonQuestions.Add(new LessonQuestion
                            {
                                LessonId = lesson.LessonId,
                                QuestionId = questionId,
                                SequenceIndex = i + 1,
                                CreatedAt = DateTime.UtcNow,
                                HasDelete = false
                            });
                        }
                    }
                    
                    if (lessonQuestions.Count > 0)
                    {
                        _context.LessonQuestions.AddRange(lessonQuestions);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("✅ Added {Count} questions to lesson {LessonId}", lessonQuestions.Count, lesson.LessonId);
                    }
                }

                _logger.LogInformation("✅ Created lesson {LessonId} for course {CourseId}", lesson.LessonId, request.CourseId);

                return Ok(new { message = "Tạo bài học thành công", data = new LessonListItemDto
                {
                    LessonId = lesson.LessonId,
                    CourseId = lesson.CourseId,
                    Title = lesson.Title,
                    Description = lesson.Description,
                    Content = lesson.Content,
                    Type = lesson.Type,
                    VideoUrl = lesson.VideoUrl,
                    ContentUrl = lesson.ContentUrl,
                    DurationSeconds = lesson.DurationSeconds,
                    OrderIndex = lesson.OrderIndex,
                    IsFree = lesson.IsFree,
                    CreatedAt = lesson.CreatedAt,
                    UpdatedAt = lesson.UpdatedAt
                }});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating lesson: {Message} | InnerException: {InnerException} | StackTrace: {StackTrace}", 
                    ex.Message, ex.InnerException?.Message, ex.StackTrace);
                
                var errorMessage = $"Lỗi hệ thống: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" | Chi tiết: {ex.InnerException.Message}";
                }
                
                return StatusCode(500, new { message = "Lỗi server", error = errorMessage });
            }
        }

        /// <summary>
        /// Cập nhật bài học
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> UpdateLesson(int id, [FromBody] UpdateLessonRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });
            }

            try
            {
                var lesson = await _context.Lessons
                    .Include(l => l.Course)
                    .FirstOrDefaultAsync(l => l.LessonId == id && !l.HasDelete);

                if (lesson == null)
                {
                    return NotFound(new { message = "Không tìm thấy bài học" });
                }

                // Kiểm tra quyền
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { message = "Không tìm thấy thông tin người dùng" });
                }

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != "Admin" && lesson.Course?.TeacherId != userId)
                {
                    return Forbid("Bạn không có quyền sửa bài học này");
                }

                // Cập nhật các trường
                if (!string.IsNullOrWhiteSpace(request.Title))
                    lesson.Title = request.Title;
                if (request.Description != null)
                    lesson.Description = request.Description;
                if (request.Content != null)
                    lesson.Content = request.Content;
                if (!string.IsNullOrWhiteSpace(request.Type))
                    lesson.Type = request.Type;
                if (request.VideoUrl != null)
                    lesson.VideoUrl = request.VideoUrl;
                if (request.ContentUrl != null)
                    lesson.ContentUrl = request.ContentUrl;
                if (request.DurationSeconds.HasValue)
                    lesson.DurationSeconds = request.DurationSeconds;
                if (request.OrderIndex.HasValue)
                    lesson.OrderIndex = request.OrderIndex;
                if (request.IsFree.HasValue)
                    lesson.IsFree = request.IsFree.Value;

                lesson.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Updated lesson {LessonId}", id);

                return Ok(new { message = "Cập nhật bài học thành công", data = new LessonListItemDto
                {
                    LessonId = lesson.LessonId,
                    CourseId = lesson.CourseId,
                    Title = lesson.Title,
                    Description = lesson.Description,
                    Type = lesson.Type,
                    VideoUrl = lesson.VideoUrl,
                    ContentUrl = lesson.ContentUrl,
                    DurationSeconds = lesson.DurationSeconds,
                    OrderIndex = lesson.OrderIndex,
                    IsFree = lesson.IsFree,
                    CreatedAt = lesson.CreatedAt,
                    UpdatedAt = lesson.UpdatedAt
                }});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating lesson {LessonId}: {Message}", id, ex.Message);
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }

        /// <summary>
        /// Xóa bài học (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> DeleteLesson(int id)
        {
            try
            {
                var lesson = await _context.Lessons
                    .Include(l => l.Course)
                    .FirstOrDefaultAsync(l => l.LessonId == id && !l.HasDelete);

                if (lesson == null)
                {
                    return NotFound(new { message = "Không tìm thấy bài học" });
                }

                // Kiểm tra quyền
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { message = "Không tìm thấy thông tin người dùng" });
                }

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != "Admin" && lesson.Course?.TeacherId != userId)
                {
                    return Forbid("Bạn không có quyền xóa bài học này");
                }

                lesson.HasDelete = true;
                lesson.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Deleted lesson {LessonId}", id);

                return Ok(new { message = "Xóa bài học thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting lesson {LessonId}: {Message}", id, ex.Message);
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }
    }

    // DTOs
    public class LessonQuestionDto
    {
        public int QuestionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? QuestionType { get; set; }
        public string? Difficulty { get; set; }
        public decimal? Marks { get; set; }
        public int? SequenceIndex { get; set; }
        public List<DTOs.AnswerOptionDto> Options { get; set; } = new List<DTOs.AnswerOptionDto>();
    }

    public class LessonListItemDto
    {
        public int LessonId { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Content { get; set; }
        public string? Type { get; set; }
        public string? VideoUrl { get; set; }
        public string? ContentUrl { get; set; }
        public int? DurationSeconds { get; set; }
        public int? OrderIndex { get; set; }
        public bool IsFree { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<LessonQuestionDto>? Questions { get; set; }
    }

    public class CreateLessonRequest
    {
        [Required]
        public int CourseId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public string? Content { get; set; } // Nội dung bài học (có thể chứa HTML)

        [MaxLength(50)]
        public string? Type { get; set; } // video, document, quiz, assignment

        [MaxLength(500)]
        public string? VideoUrl { get; set; }

        [MaxLength(500)]
        public string? ContentUrl { get; set; }

        public int? DurationSeconds { get; set; }

        public int? OrderIndex { get; set; }

        public bool IsFree { get; set; } = true;

        public List<int>? QuestionIds { get; set; } // IDs của câu hỏi từ ngân hàng
    }

    public class UpdateLessonRequest
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public string? Content { get; set; } // Nội dung bài học (có thể chứa HTML)

        [MaxLength(50)]
        public string? Type { get; set; }

        [MaxLength(500)]
        public string? VideoUrl { get; set; }

        [MaxLength(500)]
        public string? ContentUrl { get; set; }

        public int? DurationSeconds { get; set; }

        public int? OrderIndex { get; set; }

        public bool? IsFree { get; set; }
    }
}

