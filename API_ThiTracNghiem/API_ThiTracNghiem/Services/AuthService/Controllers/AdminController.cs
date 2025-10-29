using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Globalization;
using AuthService.Data;
using AuthService.DTOs;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all admin endpoints
public class AdminController : ControllerBase
{
    private readonly AuthDbContext _db;
    
    public AdminController(AuthDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ người dùng (Admin only)
    /// </summary>
    /// <param name="page">Trang hiện tại (mặc định: 1)</param>
    /// <param name="pageSize">Số lượng user mỗi trang (mặc định: 10, tối đa: 100)</param>
    /// <param name="search">Tìm kiếm theo email hoặc tên</param>
    /// <param name="status">Lọc theo trạng thái</param>
    /// <param name="roleId">Lọc theo role</param>
    /// <returns>Danh sách người dùng với phân trang</returns>
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int? roleId = null)
    {
        // Kiểm tra quyền admin
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Users
            .Include(u => u.Role)
            .Where(u => !u.HasDelete);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(u => 
                (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                (u.FullName != null && u.FullName.ToLower().Contains(searchLower)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(u => u.Status == status);
        }

        // Apply role filter
        if (roleId.HasValue)
        {
            query = query.Where(u => u.RoleId == roleId.Value);
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        // Apply pagination and get results
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync();

        var response = new GetAllUsersResponse
        {
            Users = users,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };

        return Ok(response);
    }

    /// <summary>
    /// Cập nhật thông tin user (Admin only)
    /// </summary>
    /// <param name="id">ID của user cần cập nhật</param>
    /// <param name="request">Thông tin cập nhật</param>
    /// <returns>Kết quả cập nhật</returns>
    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] AdminUpdateUserRequest request)
    {
        // Kiểm tra quyền admin
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        if (!ModelState.IsValid) 
            return BadRequest(ModelState);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id && !u.HasDelete);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }

        // Kiểm tra email trùng lặp nếu có thay đổi email
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var emailExists = await _db.Users.AnyAsync(u => 
                u.Email == request.Email && 
                u.UserId != id && 
                !u.HasDelete);
            
            if (emailExists)
            {
                return BadRequest(new { message = "Email đã được sử dụng bởi người dùng khác" });
            }
        }

        // Kiểm tra role tồn tại nếu có thay đổi role
        if (request.RoleId.HasValue)
        {
            var roleExists = await _db.Roles.AnyAsync(r => r.RoleId == request.RoleId.Value);
            if (!roleExists)
            {
                return BadRequest(new { message = "Role không tồn tại" });
            }
        }

        // Cập nhật thông tin
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            user.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            user.PhoneNumber = request.PhoneNumber;
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName;
        }

        if (request.RoleId.HasValue)
        {
            user.RoleId = request.RoleId.Value;
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

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            user.Status = request.Status;
        }

        if (request.IsEmailVerified.HasValue)
        {
            user.IsEmailVerified = request.IsEmailVerified.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            return Ok(new { message = "Cập nhật thông tin người dùng thành công" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Có lỗi xảy ra khi cập nhật thông tin", error = ex.Message });
        }
    }

    /// <summary>
    /// Khóa user (Admin only)
    /// </summary>
    /// <param name="id">ID của user cần khóa</param>
    /// <returns>Kết quả khóa user</returns>
    [HttpPut("users/{id}/lock")]
    public async Task<IActionResult> LockUser(int id)
    {
        // Kiểm tra quyền admin
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id && !u.HasDelete);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }

        // Kiểm tra xem user có phải admin không (không được khóa admin)
        var targetUser = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == id && !u.HasDelete);

        if (targetUser?.Role?.RoleName?.ToLower() == "admin")
        {
            return BadRequest(new { message = "Không thể khóa tài khoản admin" });
        }

        // Cập nhật trạng thái thành "locked"
        user.Status = "locked";
        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã khóa người dùng thành công" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Có lỗi xảy ra khi khóa người dùng", error = ex.Message });
        }
    }

    /// <summary>
    /// Mở khóa user (Admin only)
    /// </summary>
    /// <param name="id">ID của user cần mở khóa</param>
    /// <returns>Kết quả mở khóa user</returns>
    [HttpPut("users/{id}/unlock")]
    public async Task<IActionResult> UnlockUser(int id)
    {
        // Kiểm tra quyền admin
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id && !u.HasDelete);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }

        // Cập nhật trạng thái thành "active"
        user.Status = "active";
        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã mở khóa người dùng thành công" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Có lỗi xảy ra khi mở khóa người dùng", error = ex.Message });
        }
    }

    /// <summary>
    /// Xóa user (soft delete)
    /// </summary>
    /// <param name="id">ID của user cần xóa</param>
    /// <returns>Kết quả xóa user</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        // Kiểm tra quyền admin
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể xóa người dùng");
        }

        // Kiểm tra user tồn tại
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == id && !u.HasDelete);
        
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }

        // Không cho phép xóa admin khác
        if (user.Role?.RoleName?.ToLower() == "admin")
        {
            return BadRequest(new { message = "Không thể xóa tài khoản admin" });
        }

        // Lấy thông tin admin hiện tại để ghi log
        var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var currentAdminId = int.Parse(sub!);
        var currentAdmin = await _db.Users.FirstOrDefaultAsync(u => u.UserId == currentAdminId);

        try
        {
            // Soft delete user
            user.HasDelete = true;
            user.UpdatedAt = DateTime.UtcNow;

            // Ghi log hoạt động xóa user
            var logMessage = $"Admin {currentAdmin?.FullName} (ID: {currentAdminId}) đã xóa user {user.FullName} (ID: {user.UserId}, Email: {user.Email}) vào lúc {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            
            // Log vào console và có thể mở rộng để ghi vào database hoặc file log
            Console.WriteLine($"[USER_DELETE_LOG] {logMessage}");
            
            // Có thể thêm ghi log vào database nếu có bảng logs
            // await LogUserDeletionAsync(currentAdminId, user.UserId, logMessage);

            await _db.SaveChangesAsync();
            
            return Ok(new { 
                message = "Đã xóa người dùng thành công",
                deletedUser = new {
                    userId = user.UserId,
                    email = user.Email,
                    fullName = user.FullName,
                    deletedAt = DateTime.UtcNow,
                    deletedBy = currentAdmin?.FullName
                }
            });
        }
        catch (Exception ex)
        {
            var errorMessage = $"Lỗi khi xóa user {user.UserId}: {ex.Message}";
            Console.WriteLine($"[USER_DELETE_ERROR] {errorMessage}");
            return StatusCode(500, new { message = "Có lỗi xảy ra khi xóa người dùng", error = ex.Message });
        }
    }

    /// <summary>
    /// Kiểm tra xem user hiện tại có phải admin không
    /// </summary>
    /// <returns>True nếu là admin, False nếu không</returns>
    private async Task<bool> IsAdminAsync()
    {
        var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrWhiteSpace(sub) || !int.TryParse(sub, out var currentUserId))
        {
            return false;
        }

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == currentUserId && !u.HasDelete);

        return user?.Role?.RoleName?.ToLower() == "admin";
    }
}