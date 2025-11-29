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
    [Route("api/feedback")]
    [Authorize]
    public class FeedbackController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<FeedbackController> _logger;
        private readonly IUserSyncService _userSyncService;

        public FeedbackController(ChatDbContext context, ILogger<FeedbackController> logger, IUserSyncService userSyncService)
        {
            _context = context;
            _logger = logger;
            _userSyncService = userSyncService;
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
                    ExamId = request.ExamId,
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

                return Ok(new { success = true, data = new { feedback.FeedbackId, feedback.ExamId, feedback.Stars, feedback.Comment, feedback.CreatedAt } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting feedback");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi gửi feedback" });
            }
        }

        /// <summary>
        /// Lấy tất cả feedback của một exam
        /// </summary>
        [HttpGet("exam/{examId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeedbackByExam(int examId)
        {
            try
            {
                // Lấy feedback từ database (không join với Users để tránh lấy tên sai)
                var feedbacks = await _context.Feedbacks
                    .Where(f => f.ExamId == examId && !f.HasDelete)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new
                    {
                        f.FeedbackId,
                        f.UserId,
                        f.ExamId,
                        f.Stars,
                        f.Comment,
                        f.CreatedAt
                    })
                    .ToListAsync();

                // Ưu tiên lấy tên từ AuthService (nguồn chính xác nhất)
                var feedbacksWithUserNames = new List<object>();
                foreach (var fb in feedbacks)
                {
                    string userName = "Người dùng";
                    
                    // Luôn ưu tiên lấy từ AuthService trước
                    try
                    {
                        var userFromAuth = await _userSyncService.GetUserByIdAsync(fb.UserId);
                        if (userFromAuth != null)
                        {
                            userName = !string.IsNullOrEmpty(userFromAuth.FullName) 
                                ? userFromAuth.FullName 
                                : !string.IsNullOrEmpty(userFromAuth.Email) 
                                    ? userFromAuth.Email 
                                    : "Người dùng";
                        }
                        else
                        {
                            // Fallback: Nếu AuthService không có, thử lấy từ ChatService DB
                            var userFromChat = await _context.Users
                                .FirstOrDefaultAsync(u => u.UserId == fb.UserId);
                            
                            if (userFromChat != null)
                            {
                                userName = !string.IsNullOrEmpty(userFromChat.FullName) 
                                    ? userFromChat.FullName 
                                    : !string.IsNullOrEmpty(userFromChat.Email) 
                                        ? userFromChat.Email 
                                        : "Người dùng";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not fetch user {UserId} from AuthService, trying ChatService DB", fb.UserId);
                        
                        // Fallback: Nếu AuthService lỗi, thử lấy từ ChatService DB
                        try
                        {
                            var userFromChat = await _context.Users
                                .FirstOrDefaultAsync(u => u.UserId == fb.UserId);
                            
                            if (userFromChat != null)
                            {
                                userName = !string.IsNullOrEmpty(userFromChat.FullName) 
                                    ? userFromChat.FullName 
                                    : !string.IsNullOrEmpty(userFromChat.Email) 
                                        ? userFromChat.Email 
                                        : "Người dùng";
                            }
                        }
                        catch (Exception ex2)
                        {
                            _logger.LogError(ex2, "Could not fetch user {UserId} from ChatService DB either", fb.UserId);
                        }
                    }
                    
                    feedbacksWithUserNames.Add(new
                    {
                        fb.FeedbackId,
                        fb.UserId,
                        fb.ExamId,
                        fb.Stars,
                        fb.Comment,
                        fb.CreatedAt,
                        UserName = userName
                    });
                }

                return Ok(new { success = true, data = feedbacksWithUserNames });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feedback by exam {ExamId}", examId);
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi lấy feedback" });
            }
        }

        /// <summary>
        /// Lấy feedback của user hiện tại cho một exam
        /// </summary>
        [HttpGet("my/exam/{examId}")]
        public async Task<IActionResult> GetMyFeedbackForExam(int examId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không thể xác thực người dùng" });
                }

                var feedback = await _context.Feedbacks
                    .Where(f => f.ExamId == examId && f.UserId == userId && !f.HasDelete)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new
                    {
                        f.FeedbackId,
                        f.UserId,
                        f.ExamId,
                        f.Stars,
                        f.Comment,
                        f.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (feedback == null)
                {
                    return Ok(new { success = true, data = (object?)null });
                }

                return Ok(new { success = true, data = feedback });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting my feedback for exam {ExamId}", examId);
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi lấy feedback" });
            }
        }
    }
}