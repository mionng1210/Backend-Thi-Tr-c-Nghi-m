using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using API_ThiTracNghiem.Data;
using API_ThiTracNghiem.Models;

namespace API_ThiTracNghiem.Middleware
{
    public class RoleAuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RoleAuthorizationMiddleware> _logger;

        public RoleAuthorizationMiddleware(RequestDelegate next, ILogger<RoleAuthorizationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
        {
            await _next(context);

            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized ||
                context.Response.StatusCode == StatusCodes.Status403Forbidden)
            {
                try
                {
                    var userId = context.Items.ContainsKey("UserId") ? context.Items["UserId"] as int? : null;
                    var role = context.Items.ContainsKey("UserRole") ? context.Items["UserRole"] as string : null;
                    var log = new AccessLog
                    {
                        UserId = userId,
                        Role = role,
                        IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = context.Request.Headers["User-Agent"].FirstOrDefault(),
                        Endpoint = context.Request.Path.ToString(),
                        Method = context.Request.Method,
                        StatusCode = context.Response.StatusCode,
                        Reason = context.Response.StatusCode == 401 ? "Unauthorized" : "Forbidden",
                        CreatedAt = DateTime.UtcNow
                    };
                    db.AccessLogs.Add(log);
                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to log access attempt: {Message}", ex.Message);
                }
            }
        }
    }

    public static class RoleAuthorizationMiddlewareExtensions
    {
        public static IApplicationBuilder UseRoleAuthorizationLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RoleAuthorizationMiddleware>();
        }
    }
}

