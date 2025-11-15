using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Globalization;
using API_ThiTracNghiem.Services.AuthService.Data;
using API_ThiTracNghiem.Services.AuthService.DTOs;
using API_ThiTracNghiem.Services.AuthService.Models;
using API_ThiTracNghiem.Services.AuthService.Services;

namespace API_ThiTracNghiem.Services.AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all admin endpoints
public class AdminController : ControllerBase
{
    private readonly AuthDbContext _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory? _httpClientFactory;
    
    public AdminController(AuthDbContext db, IEmailService email, IConfiguration config, IHttpClientFactory? httpClientFactory = null)
    {
        _db = db;
        _email = email;
        _config = config;
        _httpClientFactory = httpClientFactory;
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
        [FromQuery] int? roleId = null,
        [FromQuery] string? role = null)
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

        // Apply role filter (by roleId or role name)
        if (roleId.HasValue)
        {
            query = query.Where(u => u.RoleId == roleId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(role))
        {
            var roleLower = role.ToLower();
            query = query.Where(u => u.Role != null && u.Role.RoleName.ToLower() == roleLower);
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
    /// Thống kê tổng hợp cho admin: người dùng, yêu cầu phân quyền, doanh thu và thống kê bài thi (tùy chọn theo examId)
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetAdminStatistics([FromQuery] int? examId = null)
    {
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        // User stats
        var usersQuery = _db.Users.Include(u => u.Role).Where(u => !u.HasDelete);
        var users = await usersQuery.ToListAsync();
        var userStats = new API_ThiTracNghiem.Services.AuthService.DTOs.UserStatsDto
        {
            TotalUsers = users.Count,
            TotalStudents = users.Count(u => (u.Role?.RoleName ?? string.Empty).Equals("Student", StringComparison.OrdinalIgnoreCase)),
            TotalTeachers = users.Count(u => (u.Role?.RoleName ?? string.Empty).Equals("Teacher", StringComparison.OrdinalIgnoreCase)),
            TotalAdmins = users.Count(u => (u.Role?.RoleName ?? string.Empty).Equals("Admin", StringComparison.OrdinalIgnoreCase)),
            NewUsersLast7Days = users.Count(u => (u.CreatedAt) >= DateTime.UtcNow.AddDays(-7))
        };

        // Permission stats
        var permissionsQuery = _db.Set<PermissionRequest>();
        var permissions = await permissionsQuery.ToListAsync();
        var successStatuses = new[] { "paid", "success", "completed" };
        var paidAmount = permissions
            .Where(r => r.PaymentAmount.HasValue && !string.IsNullOrWhiteSpace(r.PaymentStatus) && successStatuses.Contains(r.PaymentStatus!.ToLower()))
            .Sum(r => r.PaymentAmount!.Value);

        var permissionStats = new API_ThiTracNghiem.Services.AuthService.DTOs.PermissionStatsDto
        {
            PendingCount = permissions.Count(r => (r.Status ?? string.Empty).Equals("pending", StringComparison.OrdinalIgnoreCase)),
            ApprovedCount = permissions.Count(r => (r.Status ?? string.Empty).Equals("approved", StringComparison.OrdinalIgnoreCase)),
            RejectedCount = permissions.Count(r => (r.Status ?? string.Empty).Equals("rejected", StringComparison.OrdinalIgnoreCase)),
            PaidAmount = paidAmount
        };

        // Revenue stats (tạm thời lấy từ PermissionRequests)
        var revenueStats = new API_ThiTracNghiem.Services.AuthService.DTOs.RevenueStatsDto
        {
            TotalRevenue = paidAmount,
            Currency = "VND",
            Notes = "Bao gồm khoản phí duyệt quyền (PermissionRequests). Có thể mở rộng để tính doanh thu tài liệu từ MaterialsService."
        };

        API_ThiTracNghiem.Services.AuthService.DTOs.ExamStatsDto? examStats = null;

        if (examId.HasValue)
        {
            var baseUrl = _config["Services:ExamsService:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(500, new { message = "Chưa cấu hình base URL của ExamsService" });
            }

            var rawAuth = Request.Headers["Authorization"].ToString().Trim('"');
            var token = rawAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? rawAuth[7..] : rawAuth;

            HttpClient client;
            if (_httpClientFactory != null)
            {
                client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(baseUrl);
            }
            else
            {
                client = new HttpClient { BaseAddress = new Uri(baseUrl) };
            }
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var path = $"/api/Exams/exam-results/{examId.Value}";
            var resp = await client.GetAsync(path);
            var content = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                return StatusCode((int)resp.StatusCode, new { message = "Không thể lấy thống kê bài thi từ ExamsService", detail = content });
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Lấy phần Data từ ApiResponse, hỗ trợ cả "Data" (PascalCase) và "data" (camelCase)
                System.Text.Json.JsonElement dataEl;
                if (root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty("Data", out var dUpper) && dUpper.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                {
                    dataEl = dUpper;
                }
                else if (root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty("data", out var dLower) && dLower.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                {
                    dataEl = dLower;
                }
                else
                {
                    dataEl = root;
                }

                // Hàm helper đọc an toàn
                int ReadInt(System.Text.Json.JsonElement obj, string prop, int defVal = 0)
                {
                    if (obj.ValueKind == System.Text.Json.JsonValueKind.Object && obj.TryGetProperty(prop, out var p) && p.ValueKind == System.Text.Json.JsonValueKind.Number)
                        return p.GetInt32();
                    return defVal;
                }

                decimal ReadDecimal(System.Text.Json.JsonElement obj, string prop, decimal defVal = 0)
                {
                    if (obj.ValueKind == System.Text.Json.JsonValueKind.Object && obj.TryGetProperty(prop, out var p) && p.ValueKind == System.Text.Json.JsonValueKind.Number)
                        return p.GetDecimal();
                    return defVal;
                }

                double ReadDouble(System.Text.Json.JsonElement obj, string prop, double defVal = 0)
                {
                    if (obj.ValueKind == System.Text.Json.JsonValueKind.Object && obj.TryGetProperty(prop, out var p) && p.ValueKind == System.Text.Json.JsonValueKind.Number)
                        return p.GetDouble();
                    return defVal;
                }

                string? ReadString(System.Text.Json.JsonElement obj, string prop)
                {
                    if (obj.ValueKind == System.Text.Json.JsonValueKind.Object && obj.TryGetProperty(prop, out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String)
                        return p.GetString();
                    return null;
                }

                // Đọc stats nếu có
                System.Text.Json.JsonElement statsEl = default;
                var hasStats = dataEl.ValueKind == System.Text.Json.JsonValueKind.Object && dataEl.TryGetProperty("Statistics", out statsEl) && statsEl.ValueKind == System.Text.Json.JsonValueKind.Object;

                examStats = new API_ThiTracNghiem.Services.AuthService.DTOs.ExamStatsDto
                {
                    ExamId = ReadInt(dataEl, "ExamId", examId.Value),
                    ExamTitle = ReadString(dataEl, "ExamTitle"),
                    CourseName = ReadString(dataEl, "CourseName"),
                    SubjectName = ReadString(dataEl, "SubjectName"),
                    TotalStudents = hasStats ? ReadInt(statsEl, "TotalStudents") : 0,
                    PassedStudents = hasStats ? ReadInt(statsEl, "PassedStudents") : 0,
                    FailedStudents = hasStats ? ReadInt(statsEl, "FailedStudents") : 0,
                    PassRate = hasStats ? ReadDouble(statsEl, "PassRate") : 0,
                    AverageScore = hasStats ? ReadDecimal(statsEl, "AverageScore") : 0,
                    HighestScore = hasStats ? ReadDecimal(statsEl, "HighestScore") : 0,
                    LowestScore = hasStats ? ReadDecimal(statsEl, "LowestScore") : 0,
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi phân tích dữ liệu từ ExamsService", error = ex.Message });
            }
        }

        var response = new API_ThiTracNghiem.Services.AuthService.DTOs.AdminStatisticsResponse
        {
            Users = userStats,
            Permissions = permissionStats,
            Revenue = revenueStats,
            Exam = examStats,
            GeneratedAt = DateTime.UtcNow
        };

        return Ok(response);
    }

    /// <summary>
    /// Lấy danh sách báo cáo người dùng (proxy sang ChatService) - Admin only
    /// </summary>
    [HttpGet("reports")]
    public async Task<IActionResult> GetUserReports([FromQuery] string? status = null)
    {
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        var baseUrl = _config["Services:ChatService:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return StatusCode(500, new { message = "Chưa cấu hình base URL của ChatService" });
        }

        var rawAuth = Request.Headers["Authorization"].ToString().Trim('"');
        var token = rawAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? rawAuth[7..] : rawAuth;

        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var path = "/api/admin/reports" + (string.IsNullOrWhiteSpace(status) ? string.Empty : $"?status={Uri.EscapeDataString(status)}");
        var resp = await client.GetAsync(path);
        var content = await resp.Content.ReadAsStringAsync();
        return new ContentResult { Content = content, ContentType = "application/json", StatusCode = (int)resp.StatusCode };
    }

    /// <summary>
    /// Cập nhật trạng thái báo cáo: đang xử lý / đã xử lý (proxy sang ChatService)
    /// </summary>
    [HttpPut("reports/{id}")]
    public async Task<IActionResult> UpdateReportStatus(int id, [FromBody] UpdateReportStatusRequest request)
    {
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var baseUrl = _config["Services:ChatService:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return StatusCode(500, new { message = "Chưa cấu hình base URL của ChatService" });
        }

        var rawAuth = Request.Headers["Authorization"].ToString().Trim('"');
        var token = rawAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? rawAuth[7..] : rawAuth;

        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var path = $"/api/admin/reports/{id}";
        var body = System.Text.Json.JsonSerializer.Serialize(request);
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PutAsync(path, content);
        var respBody = await resp.Content.ReadAsStringAsync();
        return new ContentResult { Content = respBody, ContentType = "application/json", StatusCode = (int)resp.StatusCode };
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
    /// Cập nhật vai trò người dùng theo tên vai trò (Admin only)
    /// </summary>
    /// <param name="userId">ID của user cần đổi vai trò</param>
    /// <param name="request">Body chứa tên vai trò, ví dụ: { "role": "student" }</param>
    /// <returns>Thông tin user đã cập nhật</returns>
    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> UpdateUserRoleByName(int userId, [FromBody] AdminUpdateUserRoleRequest request)
    {
        // Kiểm tra quyền admin
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId && !u.HasDelete);

        if (user == null)
            return NotFound(new { message = "Không tìm thấy người dùng" });

        var requestedRoleName = request.Role?.Trim();
        if (string.IsNullOrWhiteSpace(requestedRoleName))
            return BadRequest(new { message = "Role không được bỏ trống" });

        // Tìm role theo tên, không phân biệt hoa thường
        var targetRole = await _db.Roles
            .FirstOrDefaultAsync(r => r.RoleName.ToLower() == requestedRoleName.ToLower());

        if (targetRole == null)
            return BadRequest(new { message = "Role không tồn tại" });

        // Không cho phép đổi role của admin sang role khác ngoài Admin
        if (user.Role?.RoleName?.ToLower() == "admin" && targetRole.RoleName.ToLower() != "admin")
            return BadRequest(new { message = "Không thể thay đổi vai trò của admin" });

        // Nếu vai trò không đổi, trả về kèm thông tin user
        if (user.RoleId == targetRole.RoleId)
        {
            var unchangedUser = new GetUserResponse
            {
                UserId = user.UserId,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                FullName = user.FullName,
                RoleId = user.RoleId,
                RoleName = user.Role != null ? user.Role.RoleName : null,
                Gender = user.Gender,
                DateOfBirth = user.DateOfBirth,
                AvatarUrl = user.AvatarUrl,
                Status = user.Status,
                IsEmailVerified = user.IsEmailVerified,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                LastLoginAt = user.LastLoginAt
            };

            return Ok(new { message = "Vai trò không thay đổi", user = unchangedUser });
        }

        user.RoleId = targetRole.RoleId;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var updatedUser = await _db.Users
            .Include(u => u.Role)
            .Where(u => u.UserId == userId && !u.HasDelete)
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

        return Ok(updatedUser);
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

    /// <summary>
    /// Lấy danh sách yêu cầu phân quyền (mặc định: pending)
    /// </summary>
    [HttpGet("permissions/requests")]
    public async Task<IActionResult> GetPermissionRequests([FromQuery] string? status = "pending")
    {
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        var query = _db.Set<PermissionRequest>()
            .Include(r => r.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var results = await query
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new PermissionRequestItem
            {
                Id = r.PermissionRequestId,
                UserId = r.UserId,
                Email = r.User!.Email,
                FullName = r.User!.FullName,
                RequestedRoleId = r.RequestedRoleId,
                Status = r.Status,
                SubmittedAt = r.SubmittedAt,
                ReviewedAt = r.ReviewedAt,
                ReviewedById = r.ReviewedById,
                RejectReason = r.RejectReason
            })
            .ToListAsync();

        return Ok(new { requests = results, count = results.Count });
    }

    /// <summary>
    /// Duyệt yêu cầu phân quyền và cập nhật role = Teacher
    /// </summary>
    [HttpPut("permissions/approve/{id}")]
    public async Task<IActionResult> ApprovePermissionRequest(int id)
    {
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        var request = await _db.Set<PermissionRequest>().FirstOrDefaultAsync(r => r.PermissionRequestId == id);
        if (request == null)
            return NotFound(new { message = "Không tìm thấy yêu cầu" });

        if (request.Status != "pending")
            return BadRequest(new { message = "Yêu cầu đã được xử lý trước đó" });

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == request.UserId && !u.HasDelete);
        if (user == null)
            return NotFound(new { message = "Không tìm thấy người dùng" });

        if (user.Role?.RoleName?.ToLower() == "admin")
            return BadRequest(new { message = "Không thể thay đổi vai trò của admin" });

        var teacherRoleId = await _db.Roles.Where(r => r.RoleName == "Teacher").Select(r => r.RoleId).FirstOrDefaultAsync();
        if (teacherRoleId <= 0)
            return StatusCode(500, new { message = "Role 'Teacher' không tồn tại" });

        user.RoleId = teacherRoleId;
        user.UpdatedAt = DateTime.UtcNow;

        var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var currentAdminId = int.Parse(sub!);

        request.Status = "approved";
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedById = currentAdminId;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Gửi email thông báo
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var subject = "Yêu cầu trở thành giáo viên đã được duyệt";
            var body = $"Chúc mừng {user.FullName}, yêu cầu trở thành giáo viên đã được duyệt. Bạn hiện có quyền Teacher.";
            await _email.SendAsync(user.Email, subject, body);
        }

        return Ok(new { message = "Đã duyệt yêu cầu và cập nhật role = Teacher", userId = user.UserId });
    }

    /// <summary>
    /// Từ chối yêu cầu phân quyền, ghi lý do và gửi email
    /// </summary>
    [HttpPut("permissions/reject/{id}")]
    public async Task<IActionResult> RejectPermissionRequest(int id, [FromBody] RejectPermissionRequest requestBody)
    {
        if (!await IsAdminAsync())
        {
            return Forbid("Chỉ admin mới có thể truy cập endpoint này");
        }

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var request = await _db.Set<PermissionRequest>().Include(r => r.User).FirstOrDefaultAsync(r => r.PermissionRequestId == id);
        if (request == null)
            return NotFound(new { message = "Không tìm thấy yêu cầu" });

        if (request.Status != "pending")
            return BadRequest(new { message = "Yêu cầu đã được xử lý trước đó" });

        var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var currentAdminId = int.Parse(sub!);

        request.Status = "rejected";
        request.RejectReason = requestBody.Reason;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedById = currentAdminId;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Gửi email thông báo
        var userEmail = request.User?.Email;
        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            var subject = "Yêu cầu trở thành giáo viên đã bị từ chối";
            var body = $"Xin chào {request.User?.FullName}, yêu cầu trở thành giáo viên đã bị từ chối. Lý do: {requestBody.Reason}.";
            await _email.SendAsync(userEmail, subject, body);
        }

        return Ok(new { message = "Đã từ chối yêu cầu và gửi email thông báo", requestId = id });
    }
}