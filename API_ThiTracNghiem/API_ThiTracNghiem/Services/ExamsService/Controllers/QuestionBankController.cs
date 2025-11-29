using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamsService.Data;
using ExamsService.Models;
using System.Text.Json;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;

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
                int? createdByUserId = null;
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out var uid))
                {
                    var existingUser = await _context.Users.FindAsync(uid);
                    if (existingUser != null) createdByUserId = uid;
                }
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
                        CreatedBy = createdByUserId,
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
                    CreatedBy = createdByUserId,
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
                return StatusCode(500, new { message = "Lỗi hệ thống khi thêm câu hỏi", error = ex.ToString() });
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

        [HttpPost("generate-ai")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> GenerateAIQuestion([FromBody] GenerateAIQuestionRequest request, [FromServices] IConfiguration config, [FromServices] IHostEnvironment envHost)
        {
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new { message = "Mô tả câu hỏi là bắt buộc" });
            }

            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId);
            if (subject == null)
            {
                return BadRequest(new { message = "Môn học không tồn tại" });
            }

            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? config["Gemini:ApiKey"] ?? string.Empty;
            var headerApiKey = Request.Headers["X-Gemini-Api-Key"].ToString();
            if (!string.IsNullOrWhiteSpace(headerApiKey)) apiKey = headerApiKey;
            if (envHost.IsDevelopment() && string.IsNullOrWhiteSpace(apiKey))
            {
                var fType = string.IsNullOrWhiteSpace(request.QuestionType) ? "MultipleChoice" : request.QuestionType;
                var fDifficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty;
                var fMarks = request.Marks ?? 1m;
                var fOptionsCount = request.OptionsCount ?? 4;
                var fCorrectCount = Math.Max(1, request.CorrectCount ?? 1);
                var opts = new List<CreateAnswerOptionRequest>();
                for (int i = 0; i < fOptionsCount; i++)
                {
                    opts.Add(new CreateAnswerOptionRequest
                    {
                        Content = $"Đáp án {i + 1}",
                        IsCorrect = i < fCorrectCount,
                        OrderIndex = i + 1
                    });
                }
                var suggestionDev = new GeneratedAIQuestionResponse
                {
                    Content = string.IsNullOrWhiteSpace(request.Description) ? "Câu hỏi trắc nghiệm mẫu" : request.Description,
                    QuestionType = fType,
                    Difficulty = fDifficulty,
                    Marks = fMarks,
                    Tags = "ai,dev,fallback",
                    SubjectId = request.SubjectId,
                    AnswerOptions = opts
                };
                return Ok(new { message = "Gợi ý câu hỏi từ AI (fallback dev)", data = suggestionDev });
            }

            var desiredType = string.IsNullOrWhiteSpace(request.QuestionType) ? "MultipleChoice" : request.QuestionType;
            var desiredDifficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty;
            var desiredMarks = request.Marks ?? 1m;
            var optionsCount = request.OptionsCount ?? 4;
            var correctCount = request.CorrectCount ?? 1;

            var prompt = $@"Hãy tạo câu hỏi trắc nghiệm về lập trình theo mô tả sau và trả về đúng duy nhất một JSON hợp lệ, không kèm chữ, không kèm markdown, không kèm giải thích.
Yêu cầu bắt buộc:
- ""questionType"" = ""MultipleChoice""
- ""difficulty"" = ""Easy"" hoặc ""Medium"" hoặc ""Hard"" (chỉ một giá trị)
- ""marks"" = {desiredMarks}
- ""tags"" = chuỗi tags, phân cách bằng dấu phẩy
- ""subjectId"" = {request.SubjectId}
- ""answerOptions"" phải có đúng 4 phần tử, mỗi phần tử có đầy đủ 3 trường: ""content"" (string, không rỗng), ""isCorrect"" (boolean, bắt buộc), ""orderIndex"" (số thứ tự 1..4).
- Có đúng 1 đáp án đúng (""isCorrect"" = true), 3 đáp án sai.
Mô tả: {request.Description}
Trả về đúng JSON, ví dụ cấu trúc:
{{
  ""content"": ""..."",
  ""questionType"": ""MultipleChoice"",
  ""difficulty"": ""Easy|Medium|Hard"",
  ""marks"": {desiredMarks},
  ""tags"": ""tag1,tag2"",
  ""subjectId"": {request.SubjectId},
  ""answerOptions"": [
    {{ ""content"": ""..."", ""isCorrect"": true,  ""orderIndex"": 1 }},
    {{ ""content"": ""..."", ""isCorrect"": false, ""orderIndex"": 2 }},
    {{ ""content"": ""..."", ""isCorrect"": false, ""orderIndex"": 3 }},
    {{ ""content"": ""..."", ""isCorrect"": false, ""orderIndex"": 4 }}
  ]
}}";

            using var http = new HttpClient();
            var models = new[] { "gemini-2.5-flash", "gemini-1.5-flash", "gemini-pro" };
            string respText = string.Empty;
            HttpResponseMessage? lastResp = null;
            foreach (var model in models)
            {
                var url = "https://generativelanguage.googleapis.com/v1beta/models/" + model + ":generateContent?key=" + apiKey;
                var body = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new { response_mime_type = "application/json" }
                };
                var reqContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                var resp = await http.PostAsync(url, reqContent);
                lastResp = resp;
                if (resp.IsSuccessStatusCode)
                {
                    respText = await resp.Content.ReadAsStringAsync();
                    break;
                }
            }
            if (string.IsNullOrEmpty(respText))
            {
                if (envHost.IsDevelopment())
                {
                    var fType = string.IsNullOrWhiteSpace(request.QuestionType) ? "MultipleChoice" : request.QuestionType;
                    var fDifficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty;
                    var fMarks = request.Marks ?? 1m;
                    var fOptionsCount = request.OptionsCount ?? 4;
                    var fCorrectCount = Math.Max(1, request.CorrectCount ?? 1);
                    var opts = new List<CreateAnswerOptionRequest>();
                    for (int i = 0; i < fOptionsCount; i++)
                    {
                        opts.Add(new CreateAnswerOptionRequest
                        {
                            Content = "Đáp án " + (i + 1),
                            IsCorrect = i < fCorrectCount,
                            OrderIndex = i + 1
                        });
                    }
                    var suggestionDev = new GeneratedAIQuestionResponse
                    {
                        Content = string.IsNullOrWhiteSpace(request.Description) ? "Câu hỏi trắc nghiệm mẫu" : request.Description,
                        QuestionType = fType,
                        Difficulty = fDifficulty,
                        Marks = fMarks,
                        Tags = "ai,dev,fallback",
                        SubjectId = request.SubjectId,
                        AnswerOptions = opts
                    };
                    return Ok(new { message = "Gợi ý câu hỏi từ AI (fallback dev)", data = suggestionDev });
                }
                var statusCode = lastResp != null ? (int)lastResp.StatusCode : 500;
                return StatusCode(statusCode, new { message = "Lỗi gọi Gemini", error = "Không thể gọi bất kỳ model Gemini" });
            }

            using var doc = JsonDocument.Parse(respText);
            string aiText = string.Empty;
            try
            {
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var parts = candidates[0].GetProperty("content").GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        aiText = parts[0].GetProperty("text").GetString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                aiText = respText;
            }

            if (string.IsNullOrWhiteSpace(aiText))
            {
                return BadRequest(new { message = "Phản hồi AI rỗng" });
            }

            aiText = aiText.Trim();
            aiText = aiText.Replace("```json", string.Empty).Replace("```", string.Empty);
            if (!aiText.TrimStart().StartsWith("{"))
            {
                var idx = aiText.IndexOf('{');
                var lastIdx = aiText.LastIndexOf('}');
                if (idx >= 0 && lastIdx > idx)
                {
                    aiText = aiText.Substring(idx, lastIdx - idx + 1);
                }
            }

            try
            {
                using var preDoc = JsonDocument.Parse(aiText);
                var rootObj = preDoc.RootElement.ValueKind == JsonValueKind.Array && preDoc.RootElement.GetArrayLength() > 0 ? preDoc.RootElement[0] : preDoc.RootElement;
                if (rootObj.ValueKind != JsonValueKind.Object)
                {
                    return BadRequest(new { message = "Không thể phân tích JSON từ AI", raw = aiText });
                }
                if (!rootObj.TryGetProperty("content", out var pContent) || pContent.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(pContent.GetString()))
                {
                    return BadRequest(new { message = "Thiếu hoặc rỗng trường content", raw = aiText });
                }
                if (!rootObj.TryGetProperty("questionType", out var pType) || pType.ValueKind != JsonValueKind.String || (pType.GetString() ?? string.Empty) != "MultipleChoice")
                {
                    return BadRequest(new { message = "questionType phải là MultipleChoice", raw = aiText });
                }
                if (!rootObj.TryGetProperty("difficulty", out var pDiff) || pDiff.ValueKind != JsonValueKind.String)
                {
                    return BadRequest(new { message = "Thiếu trường difficulty", raw = aiText });
                }
                var diffVal = pDiff.GetString() ?? string.Empty;
                if (diffVal != "Easy" && diffVal != "Medium" && diffVal != "Hard")
                {
                    return BadRequest(new { message = "difficulty phải là Easy|Medium|Hard", raw = aiText });
                }
                if (!rootObj.TryGetProperty("marks", out var pMarks) || (pMarks.ValueKind != JsonValueKind.Number && pMarks.ValueKind != JsonValueKind.String))
                {
                    return BadRequest(new { message = "Thiếu trường marks", raw = aiText });
                }
                if (!rootObj.TryGetProperty("tags", out var pTags) || pTags.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(pTags.GetString()))
                {
                    return BadRequest(new { message = "Thiếu hoặc rỗng trường tags", raw = aiText });
                }
                if (!rootObj.TryGetProperty("subjectId", out var pSubject) || (pSubject.ValueKind != JsonValueKind.Number && pSubject.ValueKind != JsonValueKind.String))
                {
                    return BadRequest(new { message = "Thiếu trường subjectId", raw = aiText });
                }
                if (!rootObj.TryGetProperty("answerOptions", out var pOptions) || pOptions.ValueKind != JsonValueKind.Array)
                {
                    return BadRequest(new { message = "Thiếu trường answerOptions dạng mảng", raw = aiText });
                }
                if (pOptions.GetArrayLength() != 4)
                {
                    return BadRequest(new { message = "answerOptions phải có đúng 4 phần tử", raw = aiText });
                }
                int correctCountCheck = 0;
                int idxOpt = 0;
                foreach (var opt in pOptions.EnumerateArray())
                {
                    idxOpt++;
                    if (opt.ValueKind != JsonValueKind.Object)
                    {
                        return BadRequest(new { message = "Phần tử answerOptions không phải object", raw = aiText });
                    }
                    if (!opt.TryGetProperty("content", out var c) || c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString()))
                    {
                        return BadRequest(new { message = "Option content is missing", raw = aiText });
                    }
                    if (!opt.TryGetProperty("isCorrect", out var ic) || (ic.ValueKind != JsonValueKind.True && ic.ValueKind != JsonValueKind.False))
                    {
                        return BadRequest(new { message = "Option isCorrect is missing", raw = aiText });
                    }
                    if (!opt.TryGetProperty("orderIndex", out var oi) || (oi.ValueKind != JsonValueKind.Number && oi.ValueKind != JsonValueKind.String))
                    {
                        return BadRequest(new { message = "Option orderIndex is missing", raw = aiText });
                    }
                    if ((ic.ValueKind == JsonValueKind.True)) correctCountCheck++;
                }
                if (correctCountCheck != 1)
                {
                    return BadRequest(new { message = "Phải có đúng 1 đáp án đúng", raw = aiText });
                }
            }
            catch
            {
                return BadRequest(new { message = "Không thể phân tích JSON từ AI", raw = aiText });
            }

            GeneratedAIQuestionResponse? suggestion = null;
            try
            {
                suggestion = System.Text.Json.JsonSerializer.Deserialize<GeneratedAIQuestionResponse>(aiText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                var sanitized = Regex.Replace(aiText, @",\s*}\s*", "}");
                sanitized = Regex.Replace(sanitized, @",\s*]\s*", "]");
                try
                {
                    suggestion = System.Text.Json.JsonSerializer.Deserialize<GeneratedAIQuestionResponse>(sanitized, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    try
                    {
                        using var parsed = JsonDocument.Parse(sanitized);
                        JsonElement root = parsed.RootElement;
                        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                        {
                            root = root[0];
                        }
                        if (root.ValueKind != JsonValueKind.Object)
                        {
                            return BadRequest(new { message = "Không thể phân tích JSON từ AI", raw = aiText });
                        }
                        string getString(params string[] keys)
                        {
                            foreach (var k in keys)
                            {
                                if (root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                                {
                                    return v.GetString() ?? string.Empty;
                                }
                            }
                            return string.Empty;
                        }
                        decimal getDecimal(params string[] keys)
                        {
                            foreach (var k in keys)
                            {
                                if (root.TryGetProperty(k, out var v))
                                {
                                    if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
                                    if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var ds)) return ds;
                                }
                            }
                            return 0m;
                        }
                        var content = getString("content", "question", "questionText", "prompt");
                        var qType = getString("questionType", "type");
                        var diff = getString("difficulty", "level");
                        var marksVal = getDecimal("marks", "score", "point", "points");
                        var opts = new List<CreateAnswerOptionRequest>();
                        int correctIdx = -1;
                        bool hasCorrectIdx = false;
                        if (root.TryGetProperty("correctIndex", out var ci) && ci.ValueKind == JsonValueKind.Number)
                        {
                            correctIdx = ci.GetInt32();
                            hasCorrectIdx = true;
                        }
                        if (root.TryGetProperty("correctAnswerIndex", out var cai) && cai.ValueKind == JsonValueKind.Number)
                        {
                            correctIdx = cai.GetInt32();
                            hasCorrectIdx = true;
                        }
                        JsonElement optionsElem;
                        if (root.TryGetProperty("answerOptions", out optionsElem) || root.TryGetProperty("options", out optionsElem) || root.TryGetProperty("choices", out optionsElem))
                        {
                            if (optionsElem.ValueKind == JsonValueKind.Array)
                            {
                                int i = 0;
                                foreach (var opt in optionsElem.EnumerateArray())
                                {
                                    string oc = string.Empty;
                                    bool ic = false;
                                    if (opt.ValueKind == JsonValueKind.Object)
                                    {
                                        if (opt.TryGetProperty("content", out var ocp) && ocp.ValueKind == JsonValueKind.String) oc = ocp.GetString() ?? string.Empty;
                                        else if (opt.TryGetProperty("text", out var otp) && otp.ValueKind == JsonValueKind.String) oc = otp.GetString() ?? string.Empty;
                                        else if (opt.TryGetProperty("option", out var oop) && oop.ValueKind == JsonValueKind.String) oc = oop.GetString() ?? string.Empty;
                                        if (opt.TryGetProperty("isCorrect", out var icp) && icp.ValueKind == JsonValueKind.True || (icp.ValueKind == JsonValueKind.String && bool.TryParse(icp.GetString(), out var b) && b)) ic = true;
                                    }
                                    else if (opt.ValueKind == JsonValueKind.String)
                                    {
                                        oc = opt.GetString() ?? string.Empty;
                                    }
                                    if (string.IsNullOrWhiteSpace(oc)) oc = "Đáp án " + (i + 1);
                                    var createOpt = new CreateAnswerOptionRequest { Content = oc, IsCorrect = ic, OrderIndex = i + 1 };
                                    if (hasCorrectIdx && i == correctIdx) createOpt.IsCorrect = true;
                                    opts.Add(createOpt);
                                    i++;
                                }
                            }
                        }
                        if (opts.Count == 0)
                        {
                            for (int i = 0; i < (optionsCount > 0 ? optionsCount : 4); i++)
                            {
                                opts.Add(new CreateAnswerOptionRequest { Content = "Đáp án " + (i + 1), IsCorrect = i == 0, OrderIndex = i + 1 });
                            }
                        }
                        suggestion = new GeneratedAIQuestionResponse
                        {
                            Content = string.IsNullOrWhiteSpace(content) ? request.Description : content,
                            QuestionType = string.IsNullOrWhiteSpace(qType) ? desiredType : qType,
                            Difficulty = string.IsNullOrWhiteSpace(diff) ? desiredDifficulty : diff,
                            Marks = marksVal > 0 ? marksVal : desiredMarks,
                            Tags = request.Tags,
                            SubjectId = request.SubjectId,
                            AnswerOptions = opts
                        };
                    }
                    catch
                    {
                        return BadRequest(new { message = "Không thể phân tích JSON từ AI", raw = aiText });
                    }
                }
            }

            if (suggestion == null)
            {
                return BadRequest(new { message = "AI không trả về dữ liệu hợp lệ" });
            }

            suggestion.SubjectId = request.SubjectId;
            if (string.IsNullOrWhiteSpace(suggestion.QuestionType)) suggestion.QuestionType = desiredType;
            if (string.IsNullOrWhiteSpace(suggestion.Difficulty)) suggestion.Difficulty = desiredDifficulty;
            if (suggestion.Marks <= 0) suggestion.Marks = desiredMarks;
            if (suggestion.AnswerOptions == null) suggestion.AnswerOptions = new List<CreateAnswerOptionRequest>();
            if (suggestion.AnswerOptions.Count == 0)
            {
                return BadRequest(new { message = "AI không tạo đáp án" });
            }

            for (int i = 0; i < suggestion.AnswerOptions.Count; i++)
            {
                var opt = suggestion.AnswerOptions[i];
                opt.OrderIndex = i + 1;
            }

            var hasCorrect = suggestion.AnswerOptions.Any(o => o.IsCorrect);
            if (!hasCorrect)
            {
                suggestion.AnswerOptions[0].IsCorrect = true;
            }

            return Ok(new { message = "Gợi ý câu hỏi từ AI", data = suggestion });
        }
    }
}
