using ChatService.Services;
using API_ThiTracNghiem.Shared.Contracts;

namespace ChatService.Middleware
{
    /// <summary>
    /// Middleware để tự động đồng bộ thông tin user từ AuthService
    /// </summary>
    public class UserSyncMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserSyncMiddleware> _logger;

        public UserSyncMiddleware(RequestDelegate next, ILogger<UserSyncMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IUserSyncService userSyncService, ChatService.Data.ChatDbContext dbContext)
        {
            _logger.LogInformation($"UserSyncMiddleware: Processing request {context.Request.Method} {context.Request.Path}");
            
            // Chỉ xử lý các request có Authorization header
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            _logger.LogInformation($"UserSyncMiddleware: Authorization header = {authHeader}");
            
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                try
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    _logger.LogInformation($"UserSyncMiddleware: Calling GetUserFromTokenAsync");
                    var user = await userSyncService.GetUserFromTokenAsync(token);
                    _logger.LogInformation($"UserSyncMiddleware: GetUserFromTokenAsync returned: {user?.Email}");

                    if (user != null)
                    {
                        // Kiểm tra và sync user vào database nếu chưa tồn tại
                        var existingUser = await dbContext.Users.FindAsync(user.UserId);
                        if (existingUser == null)
                        {
                            var newUser = new ChatService.Models.User
                            {
                                UserId = user.UserId,
                                Email = user.Email ?? "",
                                FullName = user.FullName ?? "",
                                RoleId = user.RoleId ?? 3, // Default Student role
                                Status = user.Status ?? "Active",
                                IsEmailVerified = user.IsEmailVerified,
                                CreatedAt = user.CreatedAt,
                                UpdatedAt = user.UpdatedAt ?? DateTime.UtcNow,
                                HasDelete = user.HasDelete
                            };
                            dbContext.Users.Add(newUser);
                            await dbContext.SaveChangesAsync();
                            
                            _logger.LogInformation($"User {user.UserId} ({user.Email}) synced to database");
                        }

                        // Lưu thông tin user vào HttpContext để sử dụng trong controller
                        context.Items["SyncedUser"] = user;
                        context.Items["SyncedUserId"] = user.UserId;
                        context.Items["SyncedUserRole"] = user.RoleName;
                        
                        _logger.LogDebug($"User {user.FullName} ({user.Email}) synced successfully");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to sync user from token");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in UserSyncMiddleware");
                    // Không return ở đây, vẫn tiếp tục với request
                }
            }
            else
            {
                _logger.LogInformation("UserSyncMiddleware: No Authorization header or not Bearer token");
            }

            _logger.LogInformation("UserSyncMiddleware: Calling next middleware");
            await _next(context);
            _logger.LogInformation("UserSyncMiddleware: Completed processing");
        }
    }

    /// <summary>
    /// Extension methods cho HttpContext để dễ dàng truy cập thông tin user đã sync
    /// </summary>
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Lấy thông tin user đã được sync
        /// </summary>
        public static UserSyncDto? GetSyncedUser(this HttpContext context)
        {
            return context.Items["SyncedUser"] as UserSyncDto;
        }

        /// <summary>
        /// Lấy UserId đã được sync
        /// </summary>
        public static int? GetSyncedUserId(this HttpContext context)
        {
            return context.Items["SyncedUserId"] as int?;
        }

        /// <summary>
        /// Lấy Role name đã được sync
        /// </summary>
        public static string? GetSyncedUserRole(this HttpContext context)
        {
            return context.Items["SyncedUserRole"] as string;
        }

        /// <summary>
        /// Kiểm tra user có phải Admin không
        /// </summary>
        public static bool IsAdmin(this HttpContext context)
        {
            var role = context.GetSyncedUserRole();
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kiểm tra user có phải Teacher không (bao gồm cả Admin)
        /// </summary>
        public static bool IsTeacher(this HttpContext context)
        {
            var role = context.GetSyncedUserRole();
            return string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kiểm tra user có phải Student không
        /// </summary>
        public static bool IsStudent(this HttpContext context)
        {
            var role = context.GetSyncedUserRole();
            return string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Extension method để đăng ký middleware
    /// </summary>
    public static class UserSyncMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserSync(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserSyncMiddleware>();
        }
    }
}