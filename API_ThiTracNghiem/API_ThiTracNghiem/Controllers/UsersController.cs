using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API_ThiTracNghiem.Data;
using API_ThiTracNghiem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_ThiTracNghiem.Contracts;
using System.Globalization;

namespace API_ThiTracNghiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ICloudStorage _cloud;

        public UsersController(ApplicationDbContext db, ICloudStorage cloud)
        {
            _db = db;
            _cloud = cloud;
        }

        [Authorize]
        [HttpPost("upload-avatar")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20_000_000)] // 20MB
        public async Task<IActionResult> UploadAvatar([FromForm] UploadAvatarRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var file = request.File;
            if (file == null || file.Length == 0) return BadRequest(new { message = "File rỗng" });
            if (!file.ContentType.StartsWith("image/", System.StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Chỉ chấp nhận định dạng ảnh" });

            var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(sub) || !int.TryParse(sub, out var userId)) return Unauthorized();

            var url = await _cloud.UploadImageAsync(file, "avatars");
            if (string.IsNullOrWhiteSpace(url)) return StatusCode(500, new { message = "Upload thất bại" });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound();

            user.AvatarUrl = url;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { avatarUrl = url });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .Where(u => u.UserId == id && !u.HasDelete)
                .Select(u => new GetUserResponse
                {
                    UserId = u.UserId,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    FullName = u.FullName,
                    RoleId = u.RoleId,
                    RoleName = u.Role != null ? u.Role.RoleName : null,
                    Gender = u.Gender,
                    DateOfBirth = u.DateOfBirth,
                    AvatarUrl = u.AvatarUrl,
                    Status = u.Status,
                    IsEmailVerified = u.IsEmailVerified,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt,
                    LastLoginAt = u.LastLoginAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng" });
            }

            return Ok(user);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Lấy thông tin user từ token
            var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(sub) || !int.TryParse(sub, out var currentUserId))
            {
                return Unauthorized(new { message = "Token không hợp lệ" });
            }

            // Kiểm tra quyền: chỉ được sửa thông tin của chính mình
            if (currentUserId != id)
            {
                return Forbid("Bạn chỉ có thể cập nhật thông tin của chính mình");
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id && !u.HasDelete);
            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng" });
            }

            // Cập nhật thông tin nếu có
            if (!string.IsNullOrWhiteSpace(request.FullName))
            {
                user.FullName = request.FullName;
            }

            if (!string.IsNullOrWhiteSpace(request.Gender))
            {
                user.Gender = request.Gender;
            }

            if (!string.IsNullOrWhiteSpace(request.DateOfBirth))
            {
                if (DateTime.TryParseExact(request.DateOfBirth, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    user.DateOfBirth = parsedDate;
                }
                else
                {
                    return BadRequest(new { message = "Ngày sinh không đúng định dạng dd/MM/yyyy" });
                }
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Trả về thông tin user đã cập nhật (không bao gồm password)
            var updatedUser = await _db.Users
                .Include(u => u.Role)
                .Where(u => u.UserId == id)
                .Select(u => new GetUserResponse
                {
                    UserId = u.UserId,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    FullName = u.FullName,
                    RoleId = u.RoleId,
                    RoleName = u.Role != null ? u.Role.RoleName : null,
                    Gender = u.Gender,
                    DateOfBirth = u.DateOfBirth,
                    AvatarUrl = u.AvatarUrl,
                    Status = u.Status,
                    IsEmailVerified = u.IsEmailVerified,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt,
                    LastLoginAt = u.LastLoginAt
                })
                .FirstOrDefaultAsync();

            return Ok(new { message = "Cập nhật thông tin thành công", user = updatedUser });
        }
    }
}


