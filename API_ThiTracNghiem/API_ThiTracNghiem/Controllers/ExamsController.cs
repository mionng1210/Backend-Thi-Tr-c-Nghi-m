using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using API_ThiTracNghiem.Contracts;
using API_ThiTracNghiem.Models;
using API_ThiTracNghiem.Infrastructure;
using API_ThiTracNghiem.Services;
using System.Security.Claims;

namespace API_ThiTracNghiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExamsController : ControllerBase
    {
        private readonly IExamsService _examsService;
        private readonly ICloudStorage _cloudStorage;
        private readonly ILogger<ExamsController> _logger;

        public ExamsController(IExamsService examsService, ICloudStorage cloudStorage, ILogger<ExamsController> logger)
        {
            _examsService = examsService;
            _cloudStorage = cloudStorage;
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
                var result = await _examsService.GetExamsAsync(pageIndex, pageSize, courseId, teacherId, subjectId);
                return Ok(ApiResponse.Success(result, "Lấy danh sách bài thi thành công"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("Có lỗi xảy ra khi lấy danh sách bài thi", 500));
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
                var exam = await _examsService.GetExamByIdAsync(id);
                if (exam == null)
                {
                    return NotFound(ApiResponse.Fail("Không tìm thấy bài thi", 404));
                }
                return Ok(ApiResponse.Success(exam));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi lấy thông tin bài thi", 500));
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
                var exam = await _examsService.CreateExamAsync(request, teacherId);
                return CreatedAtAction(nameof(GetExam), new { id = exam.Id }, ApiResponse.Success(exam));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message, 400));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi tạo bài thi", 500));
            }
        }

        /// <summary>
        /// Cập nhật bài thi (chỉ dành cho giáo viên sở hữu)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> UpdateExam(int id, [FromBody] UpdateExamRequest request)
        {
            try
            {
                var teacherId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var exam = await _examsService.UpdateExamAsync(id, request, teacherId);
                if (exam == null)
                {
                    return NotFound(ApiResponse.Fail("Không tìm thấy bài thi", 404));
                }
                return Ok(ApiResponse.Success(exam, "Cập nhật bài thi thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message, 400));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi cập nhật bài thi", 500));
            }
        }

        /// <summary>
        /// Xóa bài thi (chỉ dành cho giáo viên sở hữu)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> DeleteExam(int id)
        {
            try
            {
                var teacherId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _examsService.DeleteExamAsync(id, teacherId);
                if (!result)
                {
                    return NotFound(ApiResponse.Fail("Không tìm thấy bài thi", 404));
                }
                return Ok(ApiResponse.Success(null, "Xóa bài thi thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi xóa bài thi", 500));
            }
        }

        /// <summary>
        /// Thêm câu hỏi vào bài thi
        /// </summary>
        [HttpPost("{examId}/questions")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> AddQuestionToExam(int examId, [FromBody] CreateExamQuestionRequest request)
        {
            try
            {
                var teacherId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _examsService.AddQuestionToExamAsync(examId, request, teacherId);
                return Ok(ApiResponse.Success(null, "Thêm câu hỏi vào bài thi thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message, 400));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi thêm câu hỏi vào bài thi", 500));
            }
        }

        /// <summary>
        /// Xóa câu hỏi khỏi bài thi
        /// </summary>
        [HttpDelete("{examId}/questions/{questionId}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> RemoveQuestionFromExam(int examId, int questionId)
        {
            try
            {
                var teacherId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _examsService.RemoveQuestionFromExamAsync(examId, questionId, teacherId);
                if (!result)
                {
                    return NotFound(ApiResponse.Fail("Không tìm thấy câu hỏi trong bài thi", 404));
                }
                return Ok(ApiResponse.Success(null, "Xóa câu hỏi khỏi bài thi thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message, 400));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi xóa câu hỏi khỏi bài thi", 500));
            }
        }

        /// <summary>
        /// Tạo nhiều mã đề tự động dựa trên độ khó - Trộn câu hỏi
        /// </summary>
        [HttpPost("{id}/mix-questions")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> MixQuestions(int id, [FromBody] MixQuestionsRequest request)
        {
            try
            {
                var teacherId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _examsService.MixQuestionsAsync(id, request, teacherId);
                return Ok(ApiResponse.Success(result, "Tạo mã đề thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message, 400));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi tạo mã đề", 500));
            }
        }

        /// <summary>
        /// Sinh đề cho thí sinh và lưu thời gian bắt đầu - Bắt đầu thi
        /// </summary>
        [HttpPost("{id}/start")]
        [Authorize]
        public async Task<IActionResult> StartExam(int id, [FromBody] StartExamRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _examsService.StartExamAsync(id, request, userId);
                return Ok(ApiResponse.Success(result, "Bắt đầu làm bài thi thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message, 400));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message, 400));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi bắt đầu làm bài thi", 500));
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
                _logger.LogInformation("📤 Upload exam image request received. File: {FileName}, Size: {FileSize}, ContentType: {ContentType}", 
                    file?.FileName, file?.Length, file?.ContentType);

                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("❌ File is null or empty");
                    return BadRequest(ApiResponse.Fail("File rỗng", 400));
                }

                // Validate file type (only images)
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                var contentType = file.ContentType?.ToLower() ?? "";
                if (!allowedTypes.Contains(contentType))
                {
                    _logger.LogWarning("❌ Invalid file type: {ContentType}", contentType);
                    return BadRequest(ApiResponse.Fail($"Chỉ chấp nhận file ảnh (jpg, png, gif, webp). File type: {contentType}", 400));
                }

                // Check if cloudStorage is injected
                if (_cloudStorage == null)
                {
                    _logger.LogError("❌ CloudStorage service is null - DI failed");
                    return StatusCode(500, ApiResponse.Fail("CloudStorage service chưa được cấu hình", 500));
                }

                _logger.LogInformation("☁️ Starting Cloudinary upload to folder: exams/covers");
                
                // Upload to Cloudinary
                var url = await _cloudStorage.UploadImageAsync(file, "exams/covers");
                
                _logger.LogInformation("☁️ Cloudinary upload result: {Url}", url ?? "NULL");
                
                if (string.IsNullOrEmpty(url))
                {
                    _logger.LogError("❌ Upload failed - Empty URL returned from Cloudinary");
                    return StatusCode(500, ApiResponse.Fail("Upload thất bại - Không nhận được URL từ Cloudinary", 500));
                }

                _logger.LogInformation("✅ Upload successful: {Url}", url);
                return Ok(ApiResponse.Success(new { url }, "Upload ảnh thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception during image upload: {Message} | StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                
                // Log full exception details
                var errorMessage = $"Lỗi hệ thống: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" | Inner: {ex.InnerException.Message}";
                    _logger.LogError("Inner exception: {InnerMessage}", ex.InnerException.Message);
                }
                return StatusCode(500, ApiResponse.Fail(errorMessage, 500));
            }
        }
    }
}