using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API_ThiTracNghiem.Contracts;
using API_ThiTracNghiem.Data;
using API_ThiTracNghiem.Models;
using API_ThiTracNghiem.Services;
using API_ThiTracNghiem.Utils;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace API_ThiTracNghiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _email;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthController> _logger;
        private readonly IHostEnvironment _environment;

        public AuthController(ApplicationDbContext db, IEmailService email, ITokenService tokenService, ILogger<AuthController> logger, IHostEnvironment environment)
        {
            _db = db;
            _email = email;
            _tokenService = tokenService;
            _logger = logger;
            _environment = environment;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Kiểm tra tồn tại user theo Email/Phone
            var existed = await _db.Users.AnyAsync(u => u.Email == request.Email || u.PhoneNumber == request.PhoneNumber);
            if (existed) return Conflict(new { message = "Email hoặc SĐT đã tồn tại" });

            // Parse ngày sinh dd/MM/yyyy
            DateTime? dob = null;
            if (!string.IsNullOrWhiteSpace(request.DateOfBirth))
            {
                if (DateTime.TryParseExact(request.DateOfBirth, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    dob = d;
                }
                else
                {
                    return BadRequest(new { message = "Ngày sinh không đúng định dạng dd/MM/yyyy" });
                }
            }

            // Tạo OTP và lưu DB
            var otpCode = _tokenService.GenerateOtp();

            var user = new User
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Gender = request.Gender,
                DateOfBirth = dob,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = await _db.Roles.Where(r => r.RoleName == "Student").Select(r => r.RoleId).FirstOrDefaultAsync(),
                IsEmailVerified = false,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };

            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();

            var otp = new OTP
            {
                UserId = user.UserId,
                OtpCode = otpCode,
                Purpose = "register",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            await _db.OTPs.AddAsync(otp);
            await _db.SaveChangesAsync();

            // Gửi email OTP - bắt lỗi để tránh 500 nếu email service fail
            var subject = "Mã xác thực đăng ký";
            var body = EmailTemplates.BuildOtpCard("Xin chào " + request.FullName + ",", "Mã OTP đăng ký của bạn", otpCode, 5);
            try
            {
                await _email.SendAsync(request.Email, subject, body);
            }
            catch (Exception ex)
            {
                // Log lỗi nhưng vẫn trả về success vì OTP đã được lưu vào DB
                _logger.LogError(ex, "Failed to send registration OTP email to {Email}. OTP code: {OtpCode}", request.Email, otpCode);
            }

            return Ok(new { message = "Đăng ký thành công. Vui lòng kiểm tra email để nhập OTP xác thực." });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            var now = DateTime.UtcNow;
            var otp = await _db.OTPs
                .Where(o => o.UserId == user.UserId && o.OtpCode == request.Otp && !o.IsUsed && o.ExpiresAt > now)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                return BadRequest(new { message = "OTP không hợp lệ hoặc đã hết hạn" });
            }

            otp.IsUsed = true;
            user.IsEmailVerified = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Xác thực thành công" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation("=== Login attempt started ===");
                _logger.LogInformation("Login attempt for email: {Email}", request?.Email ?? "null");
                
                if (request == null)
                {
                    _logger.LogWarning("Login request is null");
                    return BadRequest(new { message = "Request không hợp lệ" });
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState is invalid for email: {Email}. Errors: {Errors}", 
                        request.Email, string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });
                }

                _logger.LogInformation("Looking up user with email: {Email}", request.Email);
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (user == null)
                {
                    _logger.LogWarning("User not found for email: {Email}", request.Email);
                    return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });
                }

                _logger.LogInformation("User found: {UserId}, verifying password", user.UserId);
                
                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    _logger.LogError("User {UserId} has no password hash", user.UserId);
                    return StatusCode(500, new { message = "Lỗi hệ thống. Vui lòng liên hệ quản trị viên." });
                }

                var ok = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                if (!ok)
                {
                    _logger.LogWarning("Password verification failed for user: {UserId}", user.UserId);
                    return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });
                }

                _logger.LogInformation("Password verified successfully for user: {UserId}", user.UserId);

                // Phát sinh OTP login và gửi email
                string otpCode;
                try
                {
                    otpCode = _tokenService.GenerateOtp();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate OTP for user {Email}", user.Email);
                    return StatusCode(500, new { message = "Không thể tạo mã OTP. Vui lòng thử lại sau." });
                }

                var otp = new OTP
                {
                    UserId = user.UserId,
                    OtpCode = otpCode,
                    Purpose = "login",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow
                };

                try
                {
                    await _db.OTPs.AddAsync(otp);
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save OTP to database for user {Email}", user.Email);
                    return StatusCode(500, new { message = "Không thể lưu mã OTP. Vui lòng thử lại sau." });
                }

                // Gửi email OTP - bắt lỗi để tránh 500 nếu email service fail
                var subject = "Mã OTP đăng nhập";
                string body;
                try
                {
                    body = EmailTemplates.BuildOtpCard(null, "Mã OTP đăng nhập của bạn", otpCode, 5);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to build email template for user {Email}", user.Email);
                    // Vẫn tiếp tục vì email template không quan trọng bằng OTP
                    body = $"Mã OTP đăng nhập của bạn: {otpCode}. Mã này có hiệu lực trong 5 phút.";
                }

                bool emailSent = false;
                if (!string.IsNullOrEmpty(user.Email))
                {
                    try
                    {
                        await _email.SendAsync(user.Email, subject, body);
                        emailSent = true;
                    }
                    catch (Exception ex)
                    {
                        // Log lỗi nhưng vẫn trả về success vì OTP đã được lưu vào DB
                        _logger.LogError(ex, "Failed to send login OTP email to {Email}. OTP code: {OtpCode}. Error: {ErrorMessage}", 
                            user.Email, otpCode, ex.Message);
                        emailSent = false;
                        // Vẫn tiếp tục vì OTP đã được lưu vào DB, user có thể yêu cầu gửi lại OTP
                    }
                }
                else
                {
                    _logger.LogWarning("User {UserId} has no email address configured", user.UserId);
                }

                _logger.LogInformation("OTP created and saved successfully for user: {UserId}", user.UserId);
                
                // Trả về success kèm thông tin OTP (trong môi trường dev có thể hiển thị OTP)
                var response = new { 
                    message = emailSent 
                        ? "Đã gửi OTP tới email. Vui lòng xác minh." 
                        : "Đã tạo mã OTP nhưng không thể gửi email. Vui lòng liên hệ quản trị viên hoặc thử lại sau.",
                    otpCode = _environment?.IsDevelopment() == true ? otpCode : null // Chỉ hiển thị OTP trong dev mode
                };
                
                _logger.LogInformation("=== Login attempt completed successfully for user: {UserId} ===", user.UserId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== UNEXPECTED ERROR in Login endpoint === Email: {Email}, Exception Type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                    request?.Email ?? "null", 
                    ex.GetType().Name, 
                    ex.Message, 
                    ex.StackTrace);
                
                // Trả về thông báo lỗi chi tiết hơn trong dev mode
                var errorMessage = _environment?.IsDevelopment() == true
                    ? $"Lỗi: {ex.GetType().Name} - {ex.Message}"
                    : "Đã xảy ra lỗi khi xử lý yêu cầu đăng nhập. Vui lòng thử lại sau.";
                
                return StatusCode(500, new { message = errorMessage });
            }
        }

        [HttpPost("verify-login-otp")]
        public async Task<IActionResult> VerifyLoginOtp([FromBody] VerifyOtpRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            var now = DateTime.UtcNow;
            var otp = await _db.OTPs
                .Where(o => o.UserId == user.UserId && o.OtpCode == request.Otp && !o.IsUsed && o.ExpiresAt > now && o.Purpose == "login")
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                return BadRequest(new { message = "OTP không hợp lệ hoặc đã hết hạn" });
            }

            otp.IsUsed = true;
            await _db.SaveChangesAsync();

            var roleName = (await _db.Roles.Where(r => r.RoleId == user.RoleId).Select(r => r.RoleName).FirstOrDefaultAsync()) ?? "Student";
            var (token, expiresAt) = _tokenService.Generate(user, roleName);

            // Tạo phiên đăng nhập
            var session = new AuthSession
            {
                UserId = user.UserId,
                DeviceInfo = Request.Headers["User-Agent"].ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                LoginAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.AuthSessions.Add(session);
            await _db.SaveChangesAsync();

            return Ok(new { token, expiresAt });
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // Tự đọc token từ header và validate để tránh 401 trước khi vào action
            var auth = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(auth)) return Unauthorized();

            var token = auth.Trim();
            var prefix = "Bearer ";
            while (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                token = token.Substring(prefix.Length).TrimStart();
            }
            token = token.Trim('"');

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var parameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"],
                ValidAudience = config["Jwt:Audience"],
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(config["Jwt:Key"]))
            };

            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, parameters, out var _);
                var sub = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                          ?? principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(sub, out var userId)) return Unauthorized();

                var session = await _db.AuthSessions
                    .Where(s => s.UserId == userId && s.IsActive)
                    .OrderByDescending(s => s.LoginAt)
                    .FirstOrDefaultAsync();

                if (session != null)
                {
                    session.IsActive = false;
                    session.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }

                return Ok(new { message = "logged out" });
            }
            catch
            {
                return Unauthorized();
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                // Không tiết lộ thông tin user có tồn tại hay không
                return Ok(new { message = "Nếu email tồn tại, mã OTP đã được gửi đến email của bạn." });
            }

            // Sinh OTP và lưu DB
            var otpCode = _tokenService.GenerateOtp();
            var otp = new OTP
            {
                UserId = user.UserId,
                OtpCode = otpCode,
                Purpose = "forgot_password",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5), // OTP hết hạn sau 5 phút
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            await _db.OTPs.AddAsync(otp);
            await _db.SaveChangesAsync();

            // Gửi email OTP - bắt lỗi để tránh 500 nếu email service fail
            var subject = "Đặt lại mật khẩu - Mã OTP xác thực";
            var body = EmailTemplates.BuildForgotPasswordCard(user.FullName ?? "Người dùng", otpCode, 5);
            try
            {
                await _email.SendAsync(request.Email, subject, body);
            }
            catch (Exception ex)
            {
                // Log lỗi nhưng vẫn trả về success vì OTP đã được lưu vào DB
                _logger.LogError(ex, "Failed to send forgot password OTP email to {Email}. OTP code: {OtpCode}", request.Email, otpCode);
            }

            return Ok(new { message = "Nếu email tồn tại, mã OTP đã được gửi đến email của bạn." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return BadRequest(new { message = "Email không tồn tại trong hệ thống" });

            // Kiểm tra OTP
            var now = DateTime.UtcNow;
            var otp = await _db.OTPs
                .Where(o => o.UserId == user.UserId && o.OtpCode == request.Otp && !o.IsUsed && o.ExpiresAt > now && o.Purpose == "forgot_password")
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                return BadRequest(new { message = "Mã OTP không hợp lệ hoặc đã hết hạn" });
            }

            // Cập nhật mật khẩu mới
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            // Đánh dấu OTP đã sử dụng
            otp.IsUsed = true;

            // Vô hiệu hóa tất cả phiên đăng nhập hiện tại
            var activeSessions = await _db.AuthSessions
                .Where(s => s.UserId == user.UserId && s.IsActive)
                .ToListAsync();
            
            foreach (var session in activeSessions)
            {
                session.IsActive = false;
                session.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại." });
        }

        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Lấy thông tin user từ token
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                              ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Token không hợp lệ" });
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            // Xác thực mật khẩu cũ
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { message = "Mật khẩu cũ không đúng" });
            }

            // Cập nhật mật khẩu mới
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            // Vô hiệu hóa tất cả phiên đăng nhập khác (trừ phiên hiện tại)
            var currentSessionToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "").Trim();
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(currentSessionToken);
            var currentSessionTime = jsonToken.IssuedAt;

            var otherSessions = await _db.AuthSessions
                .Where(s => s.UserId == user.UserId && s.IsActive && s.LoginAt != currentSessionTime)
                .ToListAsync();
            
            foreach (var session in otherSessions)
            {
                session.IsActive = false;
                session.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công. Các phiên đăng nhập khác đã bị vô hiệu hóa." });
        }
    }
}


