using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using API_ThiTracNghiem.Services.AuthService.Data;
using API_ThiTracNghiem.Shared.Contracts;
using System.Security.Claims;

namespace API_ThiTracNghiem.Services.AuthService.Controllers
{
    /// <summary>
    /// Controller để đồng bộ thông tin User cho các microservices khác
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UserSyncController : ControllerBase
    {
        private readonly AuthDbContext _db;

        public UserSyncController(AuthDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Lấy thông tin User theo UserId (cho internal services)
        /// </summary>
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<UserSyncResponse>> GetUserById(int userId)
        {
            try
            {
                var user = await _db.Users
                    .Include(u => u.Role)
                    .Where(u => u.UserId == userId && !u.HasDelete)
                    .Select(u => new UserSyncDto
                    {
                        UserId = u.UserId,
                        Email = u.Email,
                        FullName = u.FullName,
                        RoleId = u.RoleId,
                        RoleName = u.Role != null ? u.Role.RoleName : "Student",
                        Status = u.Status,
                        IsEmailVerified = u.IsEmailVerified,
                        CreatedAt = u.CreatedAt,
                        UpdatedAt = u.UpdatedAt,
                        LastLoginAt = u.LastLoginAt,
                        HasDelete = u.HasDelete
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Ok(new UserSyncResponse
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new UserSyncResponse
                {
                    Success = true,
                    Message = "User found",
                    User = user
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new UserSyncResponse
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lấy thông tin User theo Email (cho internal services)
        /// </summary>
        [HttpGet("user/email/{email}")]
        public async Task<ActionResult<UserSyncResponse>> GetUserByEmail(string email)
        {
            try
            {
                var user = await _db.Users
                    .Include(u => u.Role)
                    .Where(u => u.Email == email && !u.HasDelete)
                    .Select(u => new UserSyncDto
                    {
                        UserId = u.UserId,
                        Email = u.Email,
                        FullName = u.FullName,
                        RoleId = u.RoleId,
                        RoleName = u.Role != null ? u.Role.RoleName : "Student",
                        Status = u.Status,
                        IsEmailVerified = u.IsEmailVerified,
                        CreatedAt = u.CreatedAt,
                        UpdatedAt = u.UpdatedAt,
                        LastLoginAt = u.LastLoginAt,
                        HasDelete = u.HasDelete
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Ok(new UserSyncResponse
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new UserSyncResponse
                {
                    Success = true,
                    Message = "User found",
                    User = user
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new UserSyncResponse
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lấy thông tin User hiện tại từ JWT Token
        /// </summary>
        [HttpGet("user/current")]
        [Authorize]
        public async Task<ActionResult<UserSyncResponse>> GetCurrentUser()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Ok(new UserSyncResponse
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var user = await _db.Users
                    .Include(u => u.Role)
                    .Where(u => u.UserId == userId && !u.HasDelete)
                    .Select(u => new UserSyncDto
                    {
                        UserId = u.UserId,
                        Email = u.Email,
                        FullName = u.FullName,
                        RoleId = u.RoleId,
                        RoleName = u.Role != null ? u.Role.RoleName : "Student",
                        Status = u.Status,
                        IsEmailVerified = u.IsEmailVerified,
                        CreatedAt = u.CreatedAt,
                        UpdatedAt = u.UpdatedAt,
                        LastLoginAt = u.LastLoginAt,
                        HasDelete = u.HasDelete
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Ok(new UserSyncResponse
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new UserSyncResponse
                {
                    Success = true,
                    Message = "User found",
                    User = user
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new UserSyncResponse
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Kiểm tra quyền của User
        /// </summary>
        [HttpGet("user/{userId}/permission/{role}")]
        public async Task<ActionResult<bool>> CheckUserPermission(int userId, string role)
        {
            try
            {
                var user = await _db.Users
                    .Include(u => u.Role)
                    .Where(u => u.UserId == userId && !u.HasDelete && u.Status == "Active")
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Ok(false);

                var userRole = user.Role?.RoleName?.ToLower();
                return Ok(userRole == role.ToLower() || userRole == "admin");
            }
            catch
            {
                return Ok(false);
            }
        }
    }
}