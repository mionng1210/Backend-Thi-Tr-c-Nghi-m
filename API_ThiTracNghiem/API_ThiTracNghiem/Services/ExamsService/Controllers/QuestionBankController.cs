using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamsService.Data;
using ExamsService.Models;
using System.Text.Json;
using System.Text;
using System.Data;
using System.Data.Common;

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
        [HttpPost("debug/create-subject")]
    public async Task<IActionResult> CreateDebugSubject()
    {
        var subject = new Subject
        {
            Name = "Test Subject",
            Description = "Subject for testing",
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();
        
        return Ok(new { message = "Subject created", subjectId = subject.SubjectId, name = subject.Name });
    }

    [HttpGet("debug/subjects")]
    public async Task<IActionResult> GetDebugSubjects()
    {
        var subjects = await _context.Subjects.ToListAsync();
        return Ok(new { 
            message = "Debug subjects", 
            count = subjects.Count,
            subjects = subjects.Select(s => new { s.SubjectId, s.Name, s.Description }).ToList()
        });
    }

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

            // Validate subject exists
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId);
            if (subject == null)
            {
                // Debug: Log available subjects
                var allSubjects = await _context.Subjects.ToListAsync();
                Console.WriteLine($"DEBUG: Available subjects: {string.Join(", ", allSubjects.Select(s => $"ID:{s.SubjectId} Name:{s.Name}"))}");
                Console.WriteLine($"DEBUG: Requested SubjectId: {request.SubjectId}");
                return BadRequest(new { message = "Môn học không tồn tại" });
            }

            Console.WriteLine($"DEBUG: Found subject - ID: {subject.SubjectId}, Name: {subject.Name}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Get or create question bank for the subject
                var questionBank = await _context.QuestionBanks
                    .FirstOrDefaultAsync(qb => qb.SubjectId == request.SubjectId && !qb.HasDelete);
                
                if (questionBank == null)
                {
                    // Create question bank for this subject if none exists
                    questionBank = new QuestionBank
                    {
                        Name = $"Ngân hàng câu hỏi {subject.Name}",
                        Description = $"Ngân hàng câu hỏi cho môn {subject.Name}",
                        SubjectId = request.SubjectId,
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
                    SubjectId = questionBank.SubjectId,
                    SubjectName = subject.Name,
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
                var page = filter.Page <= 0 ? 1 : filter.Page;
                var pageSize = filter.PageSize <= 0 ? 10 : Math.Min(filter.PageSize, 100);
                var offset = (page - 1) * pageSize;
                int? subjectId = filter.SubjectId;
                var questionType = string.IsNullOrWhiteSpace(filter.QuestionType) ? null : filter.QuestionType;
                var difficulty = string.IsNullOrWhiteSpace(filter.Difficulty) ? null : filter.Difficulty;
                var tags = string.IsNullOrWhiteSpace(filter.Tags) ? null : filter.Tags;
                var searchContent = string.IsNullOrWhiteSpace(filter.SearchContent) ? null : filter.SearchContent;
                static void AddParam(DbCommand c, string n, object? v)
                {
                    var p = c.CreateParameter();
                    p.ParameterName = n;
                    p.Value = v ?? DBNull.Value;
                    c.Parameters.Add(p);
                }

                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();

                using var cmdCount = conn.CreateCommand();
                cmdCount.CommandText = "SELECT COUNT(*) FROM [Questions] q LEFT JOIN [QuestionBanks] qb ON qb.[BankId]=q.[BankId] WHERE q.[HasDelete]=0 AND (@SubjectId IS NULL OR qb.[SubjectId]=@SubjectId) AND (@QuestionType IS NULL OR q.[QuestionType]=@QuestionType) AND (@Difficulty IS NULL OR q.[Difficulty]=@Difficulty) AND (@SearchContent IS NULL OR q.[Content] LIKE '%' + @SearchContent + '%') AND (@Tags IS NULL OR q.[TagsJson] LIKE '%' + @Tags + '%');";
                AddParam(cmdCount, "@SubjectId", subjectId);
                AddParam(cmdCount, "@QuestionType", questionType);
                AddParam(cmdCount, "@Difficulty", difficulty);
                AddParam(cmdCount, "@SearchContent", searchContent);
                AddParam(cmdCount, "@Tags", tags);
                var totalCount = Convert.ToInt32(await cmdCount.ExecuteScalarAsync());

                using var cmdList = conn.CreateCommand();
                cmdList.CommandText = "SELECT q.[QuestionId],q.[Content],q.[QuestionType],q.[Difficulty],q.[Marks],q.[TagsJson],qb.[SubjectId],s.[Name] AS [SubjectName],q.[CreatedAt] FROM [Questions] q LEFT JOIN [QuestionBanks] qb ON qb.[BankId]=q.[BankId] LEFT JOIN [Subjects] s ON s.[SubjectId]=qb.[SubjectId] WHERE q.[HasDelete]=0 AND (@SubjectId IS NULL OR qb.[SubjectId]=@SubjectId) AND (@QuestionType IS NULL OR q.[QuestionType]=@QuestionType) AND (@Difficulty IS NULL OR q.[Difficulty]=@Difficulty) AND (@SearchContent IS NULL OR q.[Content] LIKE '%' + @SearchContent + '%') AND (@Tags IS NULL OR q.[TagsJson] LIKE '%' + @Tags + '%') ORDER BY q.[CreatedAt] DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
                AddParam(cmdList, "@SubjectId", subjectId);
                AddParam(cmdList, "@QuestionType", questionType);
                AddParam(cmdList, "@Difficulty", difficulty);
                AddParam(cmdList, "@SearchContent", searchContent);
                AddParam(cmdList, "@Tags", tags);
                AddParam(cmdList, "@Offset", offset);
                AddParam(cmdList, "@PageSize", pageSize);

                var questions = new List<QuestionBankResponse>();
                var questionIds = new List<int>();
                using (var r = await cmdList.ExecuteReaderAsync())
                {
                    while (await r.ReadAsync())
                    {
                        var qid = r.GetInt32(0);
                        questionIds.Add(qid);
                        questions.Add(new QuestionBankResponse
                        {
                            QuestionId = qid,
                            Content = r.GetString(1),
                            QuestionType = r.IsDBNull(2) ? null : r.GetString(2),
                            Difficulty = r.IsDBNull(3) ? null : r.GetString(3),
                            Marks = r.IsDBNull(4) ? (decimal?)null : r.GetDecimal(4),
                            Tags = r.IsDBNull(5) ? null : r.GetString(5),
                            SubjectId = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
                            SubjectName = r.IsDBNull(7) ? null : r.GetString(7),
                            CreatedAt = r.GetDateTime(8),
                            AnswerOptions = new List<AnswerOptionResponse>()
                        });
                    }
                }

                var optionsByQuestion = new Dictionary<int, List<AnswerOptionResponse>>();
                if (questionIds.Count > 0)
                {
                    using var cmdAns = conn.CreateCommand();
                    var sb = new StringBuilder();
                    for (int i = 0; i < questionIds.Count; i++)
                    {
                        var pn = "@qid" + i;
                        if (i > 0) sb.Append(", ");
                        sb.Append(pn);
                        var p = cmdAns.CreateParameter();
                        p.ParameterName = pn;
                        p.Value = questionIds[i];
                        cmdAns.Parameters.Add(p);
                    }
                    cmdAns.CommandText = "SELECT [OptionId],[QuestionId],[Content],[IsCorrect],[OrderIndex] FROM [AnswerOptions] WHERE [HasDelete]=0 AND [QuestionId] IN (" + sb.ToString() + ");";
                    using var r2 = await cmdAns.ExecuteReaderAsync();
                    while (await r2.ReadAsync())
                    {
                        var qid = r2.GetInt32(1);
                        if (!optionsByQuestion.TryGetValue(qid, out var list))
                        {
                            list = new List<AnswerOptionResponse>();
                            optionsByQuestion[qid] = list;
                        }
                        list.Add(new AnswerOptionResponse
                        {
                            OptionId = r2.GetInt32(0),
                            Content = r2.GetString(2),
                            IsCorrect = r2.GetBoolean(3),
                            OrderIndex = r2.IsDBNull(4) ? (int?)null : r2.GetInt32(4)
                        });
                    }
                }

                foreach (var q in questions)
                {
                    if (optionsByQuestion.TryGetValue(q.QuestionId, out var list)) q.AnswerOptions = list;
                }

                var response = new QuestionBankListResponse
                {
                    Questions = questions,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };

                return Ok(new { message = "Lấy danh sách câu hỏi thành công", data = response });
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