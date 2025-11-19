using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API_ThiTracNghiem.Infrastructure
{
    public class ApiResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }

        public static ApiResponse Success(object data, string message = "OK", int status = 200)
            => new ApiResponse { StatusCode = status, Message = message, Data = data };

        public static ApiResponse Fail(string message, int status = 400)
            => new ApiResponse { StatusCode = status, Message = message };
    }

    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception: {ExceptionType} - {Message}. StackTrace: {StackTrace}", 
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                
                // Kiểm tra xem response đã được gửi chưa
                if (context.Response.HasStarted)
                {
                    _logger.LogWarning("Response has already started, cannot modify status code");
                    return;
                }

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                
                // Trả về thông báo lỗi chi tiết hơn
                var environment = context.RequestServices.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
                var errorMessage = environment?.IsDevelopment() == true
                    ? $"Internal Server Error: {ex.GetType().Name} - {ex.Message}"
                    : "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại sau.";
                
                var response = new { 
                    message = errorMessage,
                    statusCode = 500
                };
                
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        }
    }
}


