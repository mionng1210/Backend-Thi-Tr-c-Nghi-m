using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatService.Data;
using ChatService.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using ChatService.Hubs;

namespace ChatService.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<NotificationsController> _logger;
        private readonly IHubContext<ChatHub> _hubContext;

        public NotificationsController(ChatDbContext context, ILogger<NotificationsController> logger, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
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

        [HttpPost("send-to-admins")]
        public async Task<IActionResult> SendToAdmins([FromBody] CreateAdminNotificationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                var adminIds = await _context.Users
                    .Include(u => u.Role)
                    .Where(u => !u.HasDelete && u.Role != null && u.Role.RoleName.ToLower() == "admin")
                    .Select(u => u.UserId)
                    .ToListAsync();

                var notifications = adminIds.Select(adminId => new Notification
                {
                    UserId = adminId,
                    Title = request.Title,
                    Message = request.Message,
                    Type = request.Type,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                if (notifications.Count > 0)
                {
                    _context.Notifications.AddRange(notifications);
                    await _context.SaveChangesAsync();
                    var payload = new NotificationResponse { NotificationId = notifications.Last().NotificationId, Title = request.Title, Message = request.Message, Type = request.Type, CreatedAt = notifications.Last().CreatedAt, IsRead = false };
                    await _hubContext.Clients.Group("Admins").SendAsync("NotificationReceived", payload);
                }

                return Ok(new { success = true, count = notifications.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notifications to admins");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi tạo thông báo" });
            }
        }

        [HttpPost("send-to-user/{userId}")]
        public async Task<IActionResult> SendToUser(int userId, [FromBody] CreateUserNotificationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                var noti = new Notification
                {
                    UserId = userId,
                    Title = request.Title,
                    Message = request.Message,
                    Type = request.Type,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(noti);
                await _context.SaveChangesAsync();

                var payload = new NotificationResponse { NotificationId = noti.NotificationId, Title = noti.Title, Message = noti.Message, Type = noti.Type, CreatedAt = noti.CreatedAt, IsRead = noti.IsRead };
                await _hubContext.Clients.Group($"User_{userId}").SendAsync("NotificationReceived", payload);

                return Ok(new { success = true, data = payload });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}", userId);
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi tạo thông báo" });
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
