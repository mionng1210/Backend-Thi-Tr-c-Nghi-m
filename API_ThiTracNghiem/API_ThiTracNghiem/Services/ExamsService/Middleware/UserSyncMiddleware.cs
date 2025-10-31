using API_ThiTracNghiem.Services;
using API_ThiTracNghiem.Shared.Contracts;

namespace API_ThiTracNghiem.Middleware
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

        public async Task InvokeAsync(HttpContext context, IUserSyncService userSyncService)
        {
            // Chỉ xử lý các request có Authorization header
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                try
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    var user = await userSyncService.GetUserFromTokenAsync(token);

                    if (user != null)
                    {
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
                }
            }

            await _next(context);
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