using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Globalization;
using API_ThiTracNghiem.Services.AuthService.Data;
using API_ThiTracNghiem.Services.AuthService.DTOs;
using API_ThiTracNghiem.Services.AuthService.Models;

namespace API_ThiTracNghiem.Services.AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly Cloudinary _cloudinary;
    private readonly AuthDbContext _db;
    
    public UsersController(IConfiguration config, AuthDbContext db)
    {
        var cloud = config["Cloudinary:CloudName"];
        var key = config["Cloudinary:ApiKey"];
        var secret = config["Cloudinary:ApiSecret"];
        _cloudinary = new Cloudinary(new Account(cloud, key, secret)) { Api = { Secure = true } };
        _db = db;
    }

    [HttpPost("upload-avatar")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)] // 20MB
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "File rỗng" });

        await using var stream = file.OpenReadStream();
        var upload = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "users/avatars",
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        };
        var result = await _cloudinary.UploadAsync(upload);
        if (result.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Created)
        {
            var url = result.SecureUrl?.ToString();
            var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(sub, out var userId))
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    user = new Models.User { UserId = userId, AvatarUrl = url };
                    _db.Users.Add(user);
                }
                else
                {
                    user.AvatarUrl = url;
                }
                await _db.SaveChangesAsync();
            }
            return Ok(new { url });
        }
        return StatusCode(500, new { message = "Upload thất bại" });
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
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Cập nhật thông tin thành công" });
    }

    /// <summary>
    /// Người dùng gửi yêu cầu trở thành giáo viên (Teacher)
    /// </summary>
    [Authorize]
    [HttpPost("request-teacher-role")]
    public async Task<IActionResult> CreatePermissionRequest([FromBody] RequestTeacherRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(sub) || !int.TryParse(sub, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ" });
        }

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId && !u.HasDelete);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }

        // Không cho phép yêu cầu nếu đã là Teacher/Admin
        var currentRole = user.Role?.RoleName?.ToLower();
        if (currentRole == "teacher")
        {
            return BadRequest(new { message = "Bạn đã là giáo viên" });
        }
        if (currentRole == "admin")
        {
            return BadRequest(new { message = "Admin không cần gửi yêu cầu" });
        }

        // Kiểm tra có yêu cầu pending trước đó hay chưa
        var hasPending = await _db.PermissionRequests
            .AnyAsync(r => r.UserId == userId && r.Status == "pending");
        if (hasPending)
        {
            return BadRequest(new { message = "Bạn đã có một yêu cầu đang chờ duyệt" });
        }

        var teacherRoleId = await _db.Roles
            .Where(r => r.RoleName == "Teacher")
            .Select(r => r.RoleId)
            .FirstOrDefaultAsync();
        if (teacherRoleId <= 0)
        {
            return StatusCode(500, new { message = "Role 'Teacher' không tồn tại" });
        }

        // Cập nhật nhanh hồ sơ nếu có truyền vào
        if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName;
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber)) user.PhoneNumber = request.PhoneNumber;
        if (!string.IsNullOrWhiteSpace(request.Gender)) user.Gender = request.Gender;
        if (!string.IsNullOrWhiteSpace(request.DateOfBirth))
        {
            if (DateTime.TryParseExact(request.DateOfBirth, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
            {
                user.DateOfBirth = dob;
            }
        }
        user.UpdatedAt = DateTime.UtcNow;

        var req = new PermissionRequest
        {
            UserId = userId,
            RequestedRoleId = teacherRoleId,
            Status = "pending",
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            BankName = request.BankName,
            BankAccountName = request.BankAccountName,
            BankAccountNumber = request.BankAccountNumber,
            PaymentMethod = request.PaymentMethod,
            PaymentReference = request.PaymentReference,
            PaymentStatus = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "none" : (request.PaymentStatus ?? "pending"),
            PaymentAmount = request.PaymentAmount
        };
        _db.PermissionRequests.Add(req);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã gửi yêu cầu trở thành giáo viên", requestId = req.PermissionRequestId });
    }

    /// <summary>
    /// Người dùng xem danh sách yêu cầu phân quyền của chính mình
    /// </summary>
    [Authorize]
    [HttpGet("request-teacher-role")]
    public async Task<IActionResult> GetMyPermissionRequests()
    {
        var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(sub) || !int.TryParse(sub, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ" });
        }

        var list = await _db.PermissionRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new PermissionRequestItem
            {
                Id = r.PermissionRequestId,
                UserId = r.UserId,
                Email = null,
                FullName = null,
                RequestedRoleId = r.RequestedRoleId,
                Status = r.Status,
                SubmittedAt = r.SubmittedAt,
                ReviewedAt = r.ReviewedAt,
                ReviewedById = r.ReviewedById,
                RejectReason = r.RejectReason
            })
            .ToListAsync();

        return Ok(new { requests = list, count = list.Count });
    }
}