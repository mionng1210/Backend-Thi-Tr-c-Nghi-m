using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatService.Data;
using ChatService.Models;
using System.Security.Claims;

namespace ChatService.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(ChatDbContext context, ILogger<ReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Gửi báo cáo vấn đề (mô tả + optional file), lưu DB trạng thái "Chưa xử lý"
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitReport([FromForm] CreateReportRequest request, [FromForm] IFormFile? attachment)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không thể xác thực người dùng" });
                }

                string? savedPath = null;
                if (attachment != null && attachment.Length > 0)
                {
                    var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "Services", "ChatService", "uploads", "reports");
                    Directory.CreateDirectory(uploadsDir);

                    var safeFileName = Path.GetFileName(attachment.FileName);
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(safeFileName)}";
                    var fullPath = Path.Combine(uploadsDir, fileName);
                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await attachment.CopyToAsync(stream);
                    }
                    // Lưu đường dẫn tương đối để tham chiếu
                    savedPath = Path.Combine("uploads", "reports", fileName).Replace("\\", "/");
                }

                var report = new Report
                {
                    UserId = userId,
                    Description = request.Description.Trim(),
                    AttachmentPath = savedPath,
                    Status = "Chưa xử lý",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Reports.Add(report);
                await _context.SaveChangesAsync();

                var response = new ReportResponse
                {
                    ReportId = report.ReportId,
                    Description = report.Description,
                    Status = report.Status,
                    AttachmentPath = report.AttachmentPath,
                    CreatedAt = report.CreatedAt,
                    UpdatedAt = report.UpdatedAt
                };

                return Ok(new { success = true, data = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting report");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi gửi báo cáo" });
            }
        }

        /// <summary>
        /// Lấy danh sách báo cáo do chính user hiện tại gửi
        /// </summary>
        [HttpGet("my-reports")]
        public async Task<IActionResult> GetMyReports()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không thể xác thực người dùng" });
                }

                var reports = await _context.Reports
                    .AsNoTracking()
                    .Where(r => r.UserId == userId && !r.HasDelete)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new ReportResponse
                    {
                        ReportId = r.ReportId,
                        Description = r.Description,
                        Status = r.Status,
                        AttachmentPath = r.AttachmentPath,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = reports });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting my reports");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi lấy danh sách báo cáo" });
            }
        }
    }
}