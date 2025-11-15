using API_ThiTracNghiem.Services;
using API_ThiTracNghiem.Contracts;
using System.Security.Claims;

namespace API_ThiTracNghiem.Middleware
{
    /// <summary>
    /// Middleware để tự động đồng bộ thông tin User từ AuthService
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
            if (context.Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    
                    try
                    {
                        // Lấy thông tin user từ AuthService
                        var user = await userSyncService.GetUserFromTokenAsync(token);
                        
                        if (user != null)
                        {
                            // Thêm thông tin user vào HttpContext để các controller khác sử dụng
                            context.Items["SyncedUser"] = user;
                            context.Items["UserId"] = user.UserId;
                            context.Items["UserRole"] = user.RoleName;
                            context.Items["UserEmail"] = user.Email;
                            context.Items["UserFullName"] = user.FullName;
                            
                            _logger.LogInformation($"User {user.UserId} ({user.Email}) synced successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to sync user from token: {ex.Message}");
                    }
                }
            }

            await _next(context);
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

    /// <summary>
    /// Helper class để lấy thông tin user đã sync trong controller
    /// </summary>
    public static class HttpContextUserExtensions
    {
        public static UserSyncDto? GetSyncedUser(this HttpContext context)
        {
            return context.Items["SyncedUser"] as UserSyncDto;
        }

        public static int? GetSyncedUserId(this HttpContext context)
        {
            return context.Items["UserId"] as int?;
        }

        public static string? GetSyncedUserRole(this HttpContext context)
        {
            return context.Items["UserRole"] as string;
        }

        public static string? GetSyncedUserEmail(this HttpContext context)
        {
            return context.Items["UserEmail"] as string;
        }

        public static string? GetSyncedUserFullName(this HttpContext context)
        {
            return context.Items["UserFullName"] as string;
        }

        public static bool IsAdmin(this HttpContext context)
        {
            var role = context.GetSyncedUserRole();
            return role?.ToLower() == "admin";
        }

        public static bool IsTeacher(this HttpContext context)
        {
            var role = context.GetSyncedUserRole();
            return role?.ToLower() == "teacher" || role?.ToLower() == "admin";
        }

        public static bool IsStudent(this HttpContext context)
        {
            var role = context.GetSyncedUserRole();
            return role?.ToLower() == "student";
        }
    }
}