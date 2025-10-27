using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamsService.Data;
using ExamsService.Models;
using System.Text.Json;

namespace ExamsService.Controllers
{
    [ApiController]
    [Route("api/question-bank")]
    public class QuestionBankController : ControllerBase
    {
        private readonly ExamsDbContext _context;

        public QuestionBankController(ExamsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Thêm câu hỏi mới vào ngân hàng câu hỏi trung tâm
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> CreateQuestion([FromBody] CreateQuestionBankRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });
            }

            // Validate answer options
            if (request.AnswerOptions == null || !request.AnswerOptions.Any())
            {
                return BadRequest(new { message = "Câu hỏi phải có ít nhất một đáp án" });
            }

            var correctAnswers = request.AnswerOptions.Where(a => a.IsCorrect).ToList();
            if (!correctAnswers.Any())
            {
                return BadRequest(new { message = "Câu hỏi phải có ít nhất một đáp án đúng" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Get or create default question bank
                var questionBank = await _context.QuestionBanks.FirstOrDefaultAsync(qb => !qb.HasDelete);
                if (questionBank == null)
                {
                    // Create default question bank if none exists
                    questionBank = new QuestionBank
                    {
                        Name = "Ngân hàng câu hỏi mặc định",
                        Description = "Ngân hàng câu hỏi chung cho hệ thống",
                        CreatedBy = 1, // Default user
                        CreatedAt = DateTime.UtcNow,
                        HasDelete = false
                    };
                    _context.QuestionBanks.Add(questionBank);
                    await _context.SaveChangesAsync();
                }

                // Create question
                var question = new Question
                {
                    BankId = questionBank.BankId,
                    Content = request.Content,
                    QuestionType = request.QuestionType,
                    Difficulty = request.Difficulty,
                    Marks = request.Marks,
                    TagsJson = !string.IsNullOrEmpty(request.Tags) ? JsonSerializer.Serialize(request.Tags.Split(',').Select(t => t.Trim()).ToArray()) : null,
                    CreatedBy = 1, // Default user for now
                    CreatedAt = DateTime.UtcNow,
                    HasDelete = false
                };

                _context.Questions.Add(question);
                await _context.SaveChangesAsync();

                // Create answer options
                var answerOptions = new List<AnswerOption>();
                foreach (var option in request.AnswerOptions)
                {
                    var answerOption = new AnswerOption
                    {
                        QuestionId = question.QuestionId,
                        Content = option.Content,
                        IsCorrect = option.IsCorrect,
                        OrderIndex = option.OrderIndex,
                        CreatedAt = DateTime.UtcNow,
                        HasDelete = false
                    };
                    answerOptions.Add(answerOption);
                }

                _context.AnswerOptions.AddRange(answerOptions);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // Return created question with answer options
                var response = new QuestionBankResponse
                {
                    QuestionId = question.QuestionId,
                    Content = question.Content,
                    QuestionType = question.QuestionType,
                    Difficulty = question.Difficulty,
                    Marks = question.Marks,
                    Tags = question.TagsJson,
                    CreatedAt = question.CreatedAt,
                    AnswerOptions = answerOptions.Select(ao => new AnswerOptionResponse
                    {
                        OptionId = ao.OptionId,
                        Content = ao.Content,
                        IsCorrect = ao.IsCorrect,
                        OrderIndex = ao.OrderIndex
                    }).ToList()
                };

                return Ok(new { message = "Thêm câu hỏi vào ngân hàng thành công", data = response });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi hệ thống khi thêm câu hỏi", error = ex.Message });
            }
        }

        /// <summary>
        /// Xem, tìm kiếm, lọc câu hỏi trong ngân hàng
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> GetQuestions([FromQuery] QuestionBankFilterRequest filter)
        {
            try
            {
                // Simple query to test
                var questions = await _context.Questions
                    .Where(q => !q.HasDelete)
                    .Take(10)
                    .ToListAsync();

                // Simple response for testing
                var questionResponses = questions.Select(q => new QuestionBankResponse
                {
                    QuestionId = q.QuestionId,
                    Content = q.Content,
                    QuestionType = q.QuestionType,
                    Difficulty = q.Difficulty,
                    Marks = q.Marks,
                    Tags = q.TagsJson,
                    CreatedAt = q.CreatedAt,
                    AnswerOptions = new List<AnswerOptionResponse>()
                }).ToList();

                return Ok(new { message = "Lấy danh sách câu hỏi thành công", data = questionResponses });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống khi lấy danh sách câu hỏi", error = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật câu hỏi trong ngân hàng câu hỏi
        /// </summary>
        [HttpPut("{questionId}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> UpdateQuestion(int questionId, [FromBody] UpdateQuestionBankRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });
            }

            // Validate answer options
            if (request.AnswerOptions == null || !request.AnswerOptions.Any())
            {
                return BadRequest(new { message = "Câu hỏi phải có ít nhất một đáp án" });
            }

            var correctAnswers = request.AnswerOptions.Where(a => a.IsCorrect).ToList();
            if (!correctAnswers.Any())
            {
                return BadRequest(new { message = "Câu hỏi phải có ít nhất một đáp án đúng" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Find existing question
                var existingQuestion = await _context.Questions
                    .FirstOrDefaultAsync(q => q.QuestionId == questionId && !q.HasDelete);

                if (existingQuestion == null)
                {
                    return NotFound(new { message = "Không tìm thấy câu hỏi" });
                }

                // Update question
                existingQuestion.Content = request.Content;
                existingQuestion.QuestionType = request.QuestionType;
                existingQuestion.Difficulty = request.Difficulty;
                existingQuestion.Marks = request.Marks;
                existingQuestion.TagsJson = !string.IsNullOrEmpty(request.Tags) ? 
                    JsonSerializer.Serialize(request.Tags.Split(',').Select(t => t.Trim()).ToArray()) : null;

                // Delete existing answer options (soft delete)
                var existingOptions = await _context.AnswerOptions
                    .Where(ao => ao.QuestionId == questionId && !ao.HasDelete)
                    .ToListAsync();

                foreach (var option in existingOptions)
                {
                    option.HasDelete = true;
                }

                // Create new answer options
                var newAnswerOptions = new List<AnswerOption>();
                foreach (var option in request.AnswerOptions)
                {
                    var answerOption = new AnswerOption
                    {
                        QuestionId = questionId,
                        Content = option.Content,
                        IsCorrect = option.IsCorrect,
                        OrderIndex = option.OrderIndex,
                        CreatedAt = DateTime.UtcNow,
                        HasDelete = false
                    };
                    newAnswerOptions.Add(answerOption);
                }

                _context.AnswerOptions.AddRange(newAnswerOptions);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Return updated question
                var updatedAnswerOptions = await _context.AnswerOptions
                    .Where(ao => ao.QuestionId == questionId && !ao.HasDelete)
                    .OrderBy(ao => ao.OrderIndex)
                    .ToListAsync();

                var response = new QuestionBankResponse
                {
                    QuestionId = existingQuestion.QuestionId,
                    Content = existingQuestion.Content,
                    QuestionType = existingQuestion.QuestionType,
                    Difficulty = existingQuestion.Difficulty,
                    Marks = existingQuestion.Marks,
                    Tags = existingQuestion.TagsJson,
                    CreatedAt = existingQuestion.CreatedAt,
                    AnswerOptions = updatedAnswerOptions.Select(ao => new AnswerOptionResponse
                    {
                        OptionId = ao.OptionId,
                        Content = ao.Content,
                        IsCorrect = ao.IsCorrect,
                        OrderIndex = ao.OrderIndex
                    }).ToList()
                };

                return Ok(new { message = "Cập nhật câu hỏi thành công", data = response });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }

        /// <summary>
        /// Xóa câu hỏi khỏi ngân hàng câu hỏi
        /// </summary>
        [HttpDelete("{questionId}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> DeleteQuestion(int questionId)
        {
            try
            {
                // Find existing question
                var existingQuestion = await _context.Questions
                    .FirstOrDefaultAsync(q => q.QuestionId == questionId && !q.HasDelete);

                if (existingQuestion == null)
                {
                    return NotFound(new { message = "Không tìm thấy câu hỏi" });
                }

                // Check if question is used in any exams
                var isUsedInExams = await _context.ExamQuestions
                    .AnyAsync(eq => eq.QuestionId == questionId && !eq.HasDelete);

                if (isUsedInExams)
                {
                    return BadRequest(new { message = "Không thể xóa câu hỏi đang được sử dụng trong bài thi" });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                // Soft delete question
                existingQuestion.HasDelete = true;

                // Soft delete associated answer options
                var answerOptions = await _context.AnswerOptions
                    .Where(ao => ao.QuestionId == questionId && !ao.HasDelete)
                    .ToListAsync();

                foreach (var option in answerOptions)
                {
                    option.HasDelete = true;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Xóa câu hỏi thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }
    }
}