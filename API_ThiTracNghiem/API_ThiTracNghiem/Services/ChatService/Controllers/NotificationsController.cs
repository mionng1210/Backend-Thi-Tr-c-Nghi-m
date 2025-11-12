using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatService.Data;
using ChatService.Models;
using System.Security.Claims;

namespace ChatService.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(ChatDbContext context, ILogger<NotificationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy thông báo mới (chưa đọc) của user hiện tại
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNewNotifications([FromQuery] int limit = 50)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không thể xác thực người dùng" });
                }

                if (limit <= 0 || limit > 200) limit = 50;

                var notifications = await _context.Notifications
                    .AsNoTracking()
                    .Where(n => n.UserId == userId && !n.HasDelete && !n.IsRead)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(limit)
                    .Select(n => new NotificationResponse
                    {
                        NotificationId = n.NotificationId,
                        Title = n.Title,
                        Message = n.Message,
                        Type = n.Type,
                        CreatedAt = n.CreatedAt,
                        IsRead = n.IsRead
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = notifications });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi lấy thông báo" });
            }
        }

        /// <summary>
        /// Cập nhật cài đặt thông báo (email/popup) của user hiện tại
        /// </summary>
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateNotificationSettingsRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không thể xác thực người dùng" });
                }

                var setting = await _context.NotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId);
                if (setting == null)
                {
                    setting = new NotificationSetting
                    {
                        UserId = userId,
                        EmailEnabled = request.EmailEnabled,
                        PopupEnabled = request.PopupEnabled,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.NotificationSettings.Add(setting);
                }
                else
                {
                    setting.EmailEnabled = request.EmailEnabled;
                    setting.PopupEnabled = request.PopupEnabled;
                    setting.UpdatedAt = DateTime.UtcNow;
                    _context.NotificationSettings.Update(setting);
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, data = new { setting.UserId, setting.EmailEnabled, setting.PopupEnabled, setting.UpdatedAt } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification settings");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi cập nhật cài đặt thông báo" });
            }
        }

        /// <summary>
        /// Đánh dấu 1 thông báo là đã đọc của user hiện tại
        /// </summary>
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không thể xác thực người dùng" });
                }

                var noti = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId && !n.HasDelete);
                if (noti == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thông báo" });
                }

                if (!noti.IsRead)
                {
                    noti.IsRead = true;
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi cập nhật thông báo" });
            }
        }
    }
}