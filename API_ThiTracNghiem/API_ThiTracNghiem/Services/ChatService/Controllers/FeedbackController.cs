using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatService.Data;
using ChatService.Models;
using System.Security.Claims;

namespace ChatService.Controllers
{
    [ApiController]
    [Route("api/feedback")]
    [Authorize]
    public class FeedbackController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<FeedbackController> _logger;

        public FeedbackController(ChatDbContext context, ILogger<FeedbackController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Gửi sao + nhận xét và lưu vào DB
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SubmitFeedback([FromBody] SubmitFeedbackRequest request)
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

                if (request.Stars < 1 || request.Stars > 5)
                {
                    return BadRequest(new { success = false, message = "Số sao phải từ 1 đến 5" });
                }

                var feedback = new Feedback
                {
                    UserId = userId,
                    Stars = request.Stars,
                    Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();

                // Tạo thông báo tự động
                try
                {
                    // Thông báo cho chính người gửi feedback
                    var selfNoti = new Notification
                    {
                        UserId = userId,
                        Title = "Cảm ơn bạn đã đánh giá",
                        Message = $"Bạn vừa gửi đánh giá {feedback.Stars} sao.",
                        Type = "feedback",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    var notifications = new List<Notification> { selfNoti };

                    // Thông báo cho admin (nếu có)
                    var adminIds = await _context.Users
                        .Include(u => u.Role)
                        .Where(u => !u.HasDelete && u.Role != null && u.Role.RoleName.ToLower() == "admin")
                        .Select(u => u.UserId)
                        .ToListAsync();

                    foreach (var adminId in adminIds)
                    {
                        notifications.Add(new Notification
                        {
                            UserId = adminId,
                            Title = "Feedback mới",
                            Message = $"User {userId} gửi feedback {feedback.Stars} sao.",
                            Type = "feedback",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    _context.Notifications.AddRange(notifications);
                    await _context.SaveChangesAsync();
                }
                catch (Exception exNoti)
                {
                    _logger.LogWarning(exNoti, "Không thể tạo thông báo sau khi gửi feedback");
                }

                return Ok(new { success = true, data = new { feedback.FeedbackId, feedback.Stars, feedback.Comment, feedback.CreatedAt } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting feedback");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi gửi feedback" });
            }
        }
    }
}