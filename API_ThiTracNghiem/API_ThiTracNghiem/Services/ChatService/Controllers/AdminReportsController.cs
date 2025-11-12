using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatService.Data;
using ChatService.Models;
using ChatService.Services;
using System.Security.Claims;

namespace ChatService.Controllers
{
    [ApiController]
    [Route("api/admin/reports")]
    [Authorize]
    public class AdminReportsController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<AdminReportsController> _logger;
        private readonly IUserSyncService _userSyncService;

        public AdminReportsController(ChatDbContext context, ILogger<AdminReportsController> logger, IUserSyncService userSyncService)
        {
            _context = context;
            _logger = logger;
            _userSyncService = userSyncService;
        }

        private async Task<bool> IsAdminAsync()
        {
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(sub) || !int.TryParse(sub, out var currentUserId))
            {
                return false;
            }

            var user = await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == currentUserId && !u.HasDelete);

            return user?.Role?.RoleName?.ToLower() == "admin";
        }

        /// <summary>
        /// Lấy danh sách tất cả báo cáo người dùng (Admin)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllReports([FromQuery] string? status = null)
        {
            if (!await IsAdminAsync())
            {
                return Forbid("Chỉ admin mới có thể truy cập endpoint này");
            }

            try
            {
                var reportsQuery = _context.Reports
                    .AsNoTracking()
                    .Where(r => !r.HasDelete);

                if (!string.IsNullOrWhiteSpace(status))
                {
                    reportsQuery = reportsQuery.Where(r => r.Status == status);
                }

                // Lấy danh sách report trước, sau đó lấy thông tin user từ AuthService để đảm bảo chính xác
                var reports = await reportsQuery
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                var results = new List<AdminReportResponse>(reports.Count);
                foreach (var r in reports)
                {
                    // Ưu tiên lấy từ AuthService để đảm bảo dữ liệu mới nhất
                    var user = await _userSyncService.GetUserByIdAsync(r.UserId);
                    results.Add(new AdminReportResponse
                    {
                        ReportId = r.ReportId,
                        UserId = r.UserId,
                        UserEmail = user?.Email,
                        UserFullName = user?.FullName,
                        Description = r.Description,
                        Status = r.Status,
                        AttachmentPath = r.AttachmentPath,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt
                    });
                }

                return Ok(new { success = true, data = results, count = results.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reports for admin");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi lấy danh sách báo cáo" });
            }
        }

        /// <summary>
        /// Cập nhật trạng thái báo cáo: "Đang xử lý" hoặc "Đã xử lý" (Admin)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReportStatus(int id, [FromBody] UpdateReportStatusRequest request)
        {
            if (!await IsAdminAsync())
            {
                return Forbid("Chỉ admin mới có thể truy cập endpoint này");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
            }

            var allowedStatuses = new[] { "Đang xử lý", "Đã xử lý" };
            if (!allowedStatuses.Contains(request.Status))
            {
                return BadRequest(new { success = false, message = "Trạng thái không hợp lệ. Chỉ cho phép: Đang xử lý, Đã xử lý" });
            }

            try
            {
                var report = await _context.Reports.FirstOrDefaultAsync(r => r.ReportId == id && !r.HasDelete);
                if (report == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy báo cáo" });
                }

                report.Status = request.Status;
                report.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Tạo thông báo cho người gửi báo cáo về trạng thái mới
                try
                {
                    var statusNoti = new Notification
                    {
                        UserId = report.UserId,
                        Title = "Cập nhật báo cáo",
                        Message = $"Báo cáo #{report.ReportId} đã được cập nhật trạng thái: {report.Status}.",
                        Type = "report_update",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(statusNoti);
                    await _context.SaveChangesAsync();
                }
                catch (Exception exNoti)
                {
                    _logger.LogWarning(exNoti, "Không thể tạo thông báo khi cập nhật trạng thái báo cáo");
                }

                // Lấy thông tin user từ AuthService để đảm bảo dữ liệu chính xác
                var user = await _userSyncService.GetUserByIdAsync(report.UserId);

                var response = new AdminReportResponse
                {
                    ReportId = report.ReportId,
                    UserId = report.UserId,
                    UserEmail = user?.Email,
                    UserFullName = user?.FullName,
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
                _logger.LogError(ex, "Error updating report status");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi cập nhật trạng thái báo cáo" });
            }
        }
    }
}