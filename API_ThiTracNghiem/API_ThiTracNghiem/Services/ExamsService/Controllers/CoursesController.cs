using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using ExamsService.Data;
using ExamsService.Models;
using ExamsService.DTOs;
using System.Security.Claims;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using API_ThiTracNghiem.Services;

namespace ExamsService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesController : ControllerBase
    {
        private readonly ExamsDbContext _context;
        private readonly ILogger<CoursesController> _logger;
        private readonly IConfiguration _config;
        private readonly Cloudinary _cloudinary;
        private readonly IUserSyncService _userSyncService;

        public CoursesController(ExamsDbContext context, ILogger<CoursesController> logger, IConfiguration config, IUserSyncService userSyncService)
        {
            _context = context;
            _logger = logger;
            _config = config;
            _userSyncService = userSyncService;
            
            // Initialize Cloudinary for course thumbnail uploads
            try
            {
                var cloud = config["Cloudinary:CloudName"];
                var key = config["Cloudinary:ApiKey"];
                var secret = config["Cloudinary:ApiSecret"];
                
                if (string.IsNullOrEmpty(cloud) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(secret))
                {
                    _logger.LogError("❌ [CoursesService] Cloudinary config is missing!");
                    _cloudinary = null!;
                }
                else
                {
                    _cloudinary = new Cloudinary(new Account(cloud, key, secret)) { Api = { Secure = true } };
                    _logger.LogInformation("✅ [CoursesService] Cloudinary initialized successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [CoursesService] Failed to initialize Cloudinary");
                _cloudinary = null!;
            }
        }

        /// <summary>
        /// Lấy danh sách khóa học với pagination và search
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetCourses(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? teacherId = null,
            [FromQuery] int? subjectId = null,
            [FromQuery] string? search = null)
        {
            try
            {
                if (pageIndex <= 0) pageIndex = 1;
                if (pageSize <= 0) pageSize = 10;
                if (pageSize > 100) pageSize = 100; // Max page size

                var query = _context.Courses
                    .Where(c => !c.HasDelete)
                    .AsQueryable();

                // Filter by teacher
                if (teacherId.HasValue)
                {
                    query = query.Where(c => c.TeacherId == teacherId.Value);
                }

                // Filter by subject
                if (subjectId.HasValue)
                {
                    query = query.Where(c => c.SubjectId == subjectId.Value);
                }

                // Search by title or description
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(c => 
                        c.Title.ToLower().Contains(searchLower) ||
                        (c.Description != null && c.Description.ToLower().Contains(searchLower)));
                }

                var totalItems = await query.CountAsync();

                var courses = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new CourseListItemDto
                    {
                        CourseId = c.CourseId,
                        Title = c.Title,
                        Description = c.Description,
                        TeacherId = c.TeacherId,
                        TeacherName = c.Teacher != null ? c.Teacher.FullName : null,
                        SubjectId = c.SubjectId,
                        SubjectName = c.Subject != null ? c.Subject.Name : null,
                        Price = c.Price,
                        IsFree = c.IsFree,
                        ThumbnailUrl = c.ThumbnailUrl,
                        DurationMinutes = c.DurationMinutes,
                        Level = c.Level,
                        Status = c.Status,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt
                    })
                    .ToListAsync();

                var response = new PagedResponse<CourseListItemDto>
                {
                    Items = courses,
                    Total = totalItems,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                };

                return Ok(ApiResponse.SuccessResponse(response, "Lấy danh sách khóa học thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting courses");
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy danh sách khóa học", 500));
            }
        }

        /// <summary>
        /// Lấy chi tiết khóa học theo ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetCourseById(int id)
        {
            try
            {
                var course = await _context.Courses
                    .Where(c => c.CourseId == id && !c.HasDelete)
                    .Select(c => new CourseListItemDto
                    {
                        CourseId = c.CourseId,
                        Title = c.Title,
                        Description = c.Description,
                        TeacherId = c.TeacherId,
                        TeacherName = c.Teacher != null ? c.Teacher.FullName : null,
                        SubjectId = c.SubjectId,
                        SubjectName = c.Subject != null ? c.Subject.Name : null,
                        Price = c.Price,
                        IsFree = c.IsFree,
                        ThumbnailUrl = c.ThumbnailUrl,
                        DurationMinutes = c.DurationMinutes,
                        Level = c.Level,
                        Status = c.Status,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (course == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy khóa học", 404));
                }

                return Ok(ApiResponse.SuccessResponse(course, "Lấy thông tin khóa học thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting course {CourseId}", id);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy thông tin khóa học", 500));
            }
        }

        /// <summary>
        /// Tạo khóa học mới
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Dữ liệu không hợp lệ", 400));
                }

                // Validate and sync teacher if provided - ALWAYS fetch from AuthService for fresh data
                if (request.TeacherId.HasValue)
                {
                    // ALWAYS sync teacher from AuthService to ensure fresh data
                    _logger.LogInformation("🔄 Syncing teacher {TeacherId} from AuthService...", request.TeacherId.Value);
                    
                    var teacherFromAuth = await _userSyncService.GetUserByIdAsync(request.TeacherId.Value);
                    _logger.LogInformation("🔍 Teacher from AuthService: UserId={UserId}, RoleName={RoleName}, FullName={FullName}, Email={Email}", 
                        request.TeacherId.Value, teacherFromAuth?.RoleName, teacherFromAuth?.FullName, teacherFromAuth?.Email);
                    
                    if (teacherFromAuth == null)
                    {
                        _logger.LogWarning("❌ Teacher {TeacherId} not found in AuthService", request.TeacherId.Value);
                        return BadRequest(ApiResponse.ErrorResponse("Giáo viên không tồn tại", 400));
                    }
                    
                    // Check role name (case-insensitive)
                    var roleNameLower = teacherFromAuth.RoleName?.ToLower() ?? "";
                    if (roleNameLower != "teacher")
                    {
                        _logger.LogWarning("❌ User {UserId} is not a teacher. RoleName={RoleName}", request.TeacherId.Value, teacherFromAuth.RoleName);
                        return BadRequest(ApiResponse.ErrorResponse("Người dùng này không phải là giáo viên", 400));
                    }
                    
                    // Check if user already exists in ExamsService (even if soft deleted)
                    var existingUser = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.UserId == teacherFromAuth.UserId);
                    
                    if (existingUser != null)
                    {
                        // User exists: UPDATE with fresh data from AuthService
                        _logger.LogInformation("🔄 Updating existing teacher {UserId} in ExamsService with fresh data from AuthService", teacherFromAuth.UserId);
                        
                        // Validate RoleId exists before updating
                        var roleIdToSet = teacherFromAuth.RoleId ?? 2;
                        var roleExists = await _context.Roles.AnyAsync(r => r.RoleId == roleIdToSet);
                        if (!roleExists)
                        {
                            _logger.LogWarning("⚠️ RoleId {RoleId} does not exist, using default RoleId 2", roleIdToSet);
                            roleIdToSet = 2;
                        }
                        
                        existingUser.Email = teacherFromAuth.Email ?? "";
                        existingUser.FullName = teacherFromAuth.FullName ?? "";
                        existingUser.RoleId = roleIdToSet;
                        existingUser.Status = teacherFromAuth.Status ?? "Active";
                        existingUser.IsEmailVerified = teacherFromAuth.IsEmailVerified;
                        existingUser.HasDelete = teacherFromAuth.HasDelete;
                        if (string.IsNullOrEmpty(existingUser.PasswordHash))
                        {
                            existingUser.PasswordHash = "SYNCED_USER";
                        }
                        
                        try
                        {
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("✅ Teacher {TeacherId} updated from AuthService", request.TeacherId.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Error updating teacher {TeacherId}. Inner: {InnerException}", 
                                request.TeacherId.Value, ex.InnerException?.Message ?? "None");
                            return BadRequest(ApiResponse.ErrorResponse($"Lỗi khi cập nhật giáo viên: {ex.InnerException?.Message ?? ex.Message}", 400));
                        }
                    }
                    else
                    {
                        // User doesn't exist: CREATE with specific UserId using IDENTITY_INSERT
                        _logger.LogInformation("➕ Creating new teacher {UserId} in ExamsService from AuthService", teacherFromAuth.UserId);
                        try
                        {
                            var roleIdToSet = teacherFromAuth.RoleId ?? 2;
                            var roleExists = await _context.Roles.AnyAsync(r => r.RoleId == roleIdToSet);
                            if (!roleExists)
                            {
                                _logger.LogWarning("⚠️ RoleId {RoleId} does not exist, using default RoleId 2", roleIdToSet);
                                roleIdToSet = 2;
                            }
                            
                            var createdAt = teacherFromAuth.CreatedAt != default(DateTime) ? teacherFromAuth.CreatedAt : DateTime.UtcNow;
                            
                            var sql = @"
                                SET IDENTITY_INSERT Users ON;
                                
                                INSERT INTO Users (UserId, Email, FullName, PasswordHash, RoleId, Status, IsEmailVerified, CreatedAt, HasDelete)
                                VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8});
                                
                                SET IDENTITY_INSERT Users OFF;
                            ";
                            
                            await _context.Database.ExecuteSqlRawAsync(sql,
                                teacherFromAuth.UserId,
                                teacherFromAuth.Email ?? "",
                                teacherFromAuth.FullName ?? "",
                                "SYNCED_USER",
                                roleIdToSet,
                                teacherFromAuth.Status ?? "Active",
                                teacherFromAuth.IsEmailVerified,
                                createdAt,
                                teacherFromAuth.HasDelete);
                            
                            _logger.LogInformation("✅ Teacher {TeacherId} created in ExamsService", request.TeacherId.Value);
                        }
                        catch (Exception ex)
                        {
                            try { await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Users OFF"); } catch { }
                            
                            _logger.LogError(ex, "❌ Error creating teacher {TeacherId}. Inner: {InnerException}", 
                                request.TeacherId.Value, ex.InnerException?.Message ?? "None");
                            return BadRequest(ApiResponse.ErrorResponse($"Lỗi khi tạo giáo viên: {ex.InnerException?.Message ?? ex.Message}", 400));
                        }
                    }
                }

                // Validate subject if provided
                if (request.SubjectId.HasValue)
                {
                    var subjectExists = await _context.Subjects
                        .AnyAsync(s => s.SubjectId == request.SubjectId.Value);
                    if (!subjectExists)
                    {
                        return BadRequest(ApiResponse.ErrorResponse("Môn học không tồn tại", 400));
                    }
                }

                // If user is Teacher, set TeacherId automatically
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                {
                    var user = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.UserId == userId);
                    
                    if (user != null && user.Role?.Name?.ToLower() == "teacher")
                    {
                        request.TeacherId = userId;
                    }
                }

                var course = new Course
                {
                    Title = request.Title.Trim(),
                    Description = request.Description?.Trim(),
                    TeacherId = request.TeacherId,
                    SubjectId = request.SubjectId,
                    Price = request.IsFree == true ? null : request.Price,
                    IsFree = request.IsFree ?? true,
                    ThumbnailUrl = request.ThumbnailUrl?.Trim(),
                    DurationMinutes = request.DurationMinutes,
                    Level = request.Level?.Trim(),
                    Status = request.Status ?? "Draft",
                    CreatedAt = DateTime.UtcNow,
                    HasDelete = false
                };

                _context.Courses.Add(course);
                await _context.SaveChangesAsync();

                var response = new CourseListItemDto
                {
                    CourseId = course.CourseId,
                    Title = course.Title,
                    Description = course.Description,
                    TeacherId = course.TeacherId,
                    SubjectId = course.SubjectId,
                    Price = course.Price,
                    IsFree = course.IsFree,
                    ThumbnailUrl = course.ThumbnailUrl,
                    DurationMinutes = course.DurationMinutes,
                    Level = course.Level,
                    Status = course.Status,
                    CreatedAt = course.CreatedAt,
                    UpdatedAt = course.UpdatedAt
                };

                return CreatedAtAction(nameof(GetCourseById), new { id = course.CourseId }, 
                    ApiResponse.SuccessResponse(response, "Tạo khóa học thành công"));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating course");
                return StatusCode(500, ApiResponse.ErrorResponse(
                    $"Lỗi database khi tạo khóa học: {dbEx.InnerException?.Message ?? dbEx.Message}", 500));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating course");
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống khi tạo khóa học: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Cập nhật khóa học
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseRequest request)
        {
            try
            {
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.CourseId == id && !c.HasDelete);

                if (course == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy khóa học", 404));
                }

                // Check permission: Teacher can only update their own courses
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                {
                    var user = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.UserId == userId);
                    
                    if (user?.Role?.Name?.ToLower() == "teacher" && course.TeacherId != userId)
                    {
                        return StatusCode(403, ApiResponse.ErrorResponse("Bạn không có quyền cập nhật khóa học này", 403));
                    }
                }

                // Validate and sync teacher if provided - ALWAYS fetch from AuthService for fresh data
                if (request.TeacherId.HasValue)
                {
                    // ALWAYS sync teacher from AuthService to ensure fresh data
                    _logger.LogInformation("🔄 Syncing teacher {TeacherId} from AuthService...", request.TeacherId.Value);
                    
                    var teacherFromAuth = await _userSyncService.GetUserByIdAsync(request.TeacherId.Value);
                    _logger.LogInformation("🔍 Teacher from AuthService: UserId={UserId}, RoleName={RoleName}, FullName={FullName}, Email={Email}", 
                        request.TeacherId.Value, teacherFromAuth?.RoleName, teacherFromAuth?.FullName, teacherFromAuth?.Email);
                    
                    if (teacherFromAuth == null)
                    {
                        _logger.LogWarning("❌ Teacher {TeacherId} not found in AuthService", request.TeacherId.Value);
                        return BadRequest(ApiResponse.ErrorResponse("Giáo viên không tồn tại", 400));
                    }
                    
                    // Check role name (case-insensitive)
                    var roleNameLower = teacherFromAuth.RoleName?.ToLower() ?? "";
                    if (roleNameLower != "teacher")
                    {
                        _logger.LogWarning("❌ User {UserId} is not a teacher. RoleName={RoleName}", request.TeacherId.Value, teacherFromAuth.RoleName);
                        return BadRequest(ApiResponse.ErrorResponse("Người dùng này không phải là giáo viên", 400));
                    }
                    
                    // Check if user already exists in ExamsService (even if soft deleted)
                    var existingUser = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.UserId == teacherFromAuth.UserId);
                    
                    if (existingUser != null)
                    {
                        // User exists: UPDATE with fresh data from AuthService
                        _logger.LogInformation("🔄 Updating existing teacher {UserId} in ExamsService with fresh data from AuthService", teacherFromAuth.UserId);
                        
                        // Validate RoleId exists before updating
                        var roleIdToSet = teacherFromAuth.RoleId ?? 2;
                        var roleExists = await _context.Roles.AnyAsync(r => r.RoleId == roleIdToSet);
                        if (!roleExists)
                        {
                            _logger.LogWarning("⚠️ RoleId {RoleId} does not exist, using default RoleId 2", roleIdToSet);
                            roleIdToSet = 2;
                        }
                        
                        existingUser.Email = teacherFromAuth.Email ?? "";
                        existingUser.FullName = teacherFromAuth.FullName ?? "";
                        existingUser.RoleId = roleIdToSet;
                        existingUser.Status = teacherFromAuth.Status ?? "Active";
                        existingUser.IsEmailVerified = teacherFromAuth.IsEmailVerified;
                        existingUser.HasDelete = teacherFromAuth.HasDelete;
                        if (string.IsNullOrEmpty(existingUser.PasswordHash))
                        {
                            existingUser.PasswordHash = "SYNCED_USER";
                        }
                        
                        try
                        {
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("✅ Teacher {TeacherId} updated from AuthService", request.TeacherId.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Error updating teacher {TeacherId}. Inner: {InnerException}", 
                                request.TeacherId.Value, ex.InnerException?.Message ?? "None");
                            return BadRequest(ApiResponse.ErrorResponse($"Lỗi khi cập nhật giáo viên: {ex.InnerException?.Message ?? ex.Message}", 400));
                        }
                    }
                    else
                    {
                        // User doesn't exist: CREATE with specific UserId using IDENTITY_INSERT
                        _logger.LogInformation("➕ Creating new teacher {UserId} in ExamsService from AuthService", teacherFromAuth.UserId);
                        try
                        {
                            var roleIdToSet = teacherFromAuth.RoleId ?? 2;
                            var roleExists = await _context.Roles.AnyAsync(r => r.RoleId == roleIdToSet);
                            if (!roleExists)
                            {
                                _logger.LogWarning("⚠️ RoleId {RoleId} does not exist, using default RoleId 2", roleIdToSet);
                                roleIdToSet = 2;
                            }
                            
                            var createdAt = teacherFromAuth.CreatedAt != default(DateTime) ? teacherFromAuth.CreatedAt : DateTime.UtcNow;
                            
                            var sql = @"
                                SET IDENTITY_INSERT Users ON;
                                
                                INSERT INTO Users (UserId, Email, FullName, PasswordHash, RoleId, Status, IsEmailVerified, CreatedAt, HasDelete)
                                VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8});
                                
                                SET IDENTITY_INSERT Users OFF;
                            ";
                            
                            await _context.Database.ExecuteSqlRawAsync(sql,
                                teacherFromAuth.UserId,
                                teacherFromAuth.Email ?? "",
                                teacherFromAuth.FullName ?? "",
                                "SYNCED_USER",
                                roleIdToSet,
                                teacherFromAuth.Status ?? "Active",
                                teacherFromAuth.IsEmailVerified,
                                createdAt,
                                teacherFromAuth.HasDelete);
                            
                            _logger.LogInformation("✅ Teacher {TeacherId} created in ExamsService", request.TeacherId.Value);
                        }
                        catch (Exception ex)
                        {
                            try { await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Users OFF"); } catch { }
                            
                            _logger.LogError(ex, "❌ Error creating teacher {TeacherId}. Inner: {InnerException}", 
                                request.TeacherId.Value, ex.InnerException?.Message ?? "None");
                            return BadRequest(ApiResponse.ErrorResponse($"Lỗi khi tạo giáo viên: {ex.InnerException?.Message ?? ex.Message}", 400));
                        }
                    }
                }

                // Validate subject if provided
                if (request.SubjectId.HasValue)
                {
                    var subjectExists = await _context.Subjects
                        .AnyAsync(s => s.SubjectId == request.SubjectId.Value);
                    if (!subjectExists)
                    {
                        return BadRequest(ApiResponse.ErrorResponse("Môn học không tồn tại", 400));
                    }
                }

                // Update fields
                if (!string.IsNullOrWhiteSpace(request.Title))
                {
                    course.Title = request.Title.Trim();
                }

                if (request.Description != null)
                {
                    course.Description = request.Description.Trim();
                }

                if (request.TeacherId.HasValue)
                {
                    course.TeacherId = request.TeacherId.Value;
                }

                if (request.SubjectId.HasValue)
                {
                    course.SubjectId = request.SubjectId.Value;
                }

                if (request.IsFree.HasValue)
                {
                    course.IsFree = request.IsFree.Value;
                    if (request.IsFree.Value)
                    {
                        course.Price = null;
                    }
                    else if (request.Price.HasValue)
                    {
                        course.Price = request.Price.Value;
                    }
                }
                else if (request.Price.HasValue)
                {
                    course.Price = request.Price.Value;
                    course.IsFree = false;
                }

                if (request.ThumbnailUrl != null)
                {
                    course.ThumbnailUrl = request.ThumbnailUrl.Trim();
                }

                if (request.DurationMinutes.HasValue)
                {
                    course.DurationMinutes = request.DurationMinutes.Value;
                }

                if (!string.IsNullOrWhiteSpace(request.Level))
                {
                    course.Level = request.Level.Trim();
                }

                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    course.Status = request.Status.Trim();
                }

                course.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var response = new CourseListItemDto
                {
                    CourseId = course.CourseId,
                    Title = course.Title,
                    Description = course.Description,
                    TeacherId = course.TeacherId,
                    SubjectId = course.SubjectId,
                    Price = course.Price,
                    IsFree = course.IsFree,
                    ThumbnailUrl = course.ThumbnailUrl,
                    DurationMinutes = course.DurationMinutes,
                    Level = course.Level,
                    Status = course.Status,
                    CreatedAt = course.CreatedAt,
                    UpdatedAt = course.UpdatedAt
                };

                return Ok(ApiResponse.SuccessResponse(response, "Cập nhật khóa học thành công"));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating course {CourseId}", id);
                return StatusCode(500, ApiResponse.ErrorResponse(
                    $"Lỗi database khi cập nhật khóa học: {dbEx.InnerException?.Message ?? dbEx.Message}", 500));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating course {CourseId}", id);
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống khi cập nhật khóa học: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Xóa khóa học (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            try
            {
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.CourseId == id && !c.HasDelete);

                if (course == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Không tìm thấy khóa học", 404));
                }

                // Check permission: Teacher can only delete their own courses
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                {
                    var user = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.UserId == userId);
                    
                    if (user?.Role?.Name?.ToLower() == "teacher" && course.TeacherId != userId)
                    {
                        return StatusCode(403, ApiResponse.ErrorResponse("Bạn không có quyền xóa khóa học này", 403));
                    }
                }

                // Soft delete
                course.HasDelete = true;
                course.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(ApiResponse.SuccessResponse(null, "Xóa khóa học thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting course {CourseId}", id);
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống khi xóa khóa học: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Upload course thumbnail image to Cloudinary
        /// </summary>
        [HttpPost("upload-image")]
        [Authorize(Roles = "Admin,Teacher")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20_000_000)] // 20MB
        public async Task<IActionResult> UploadCourseImage(IFormFile file)
        {
            try
            {
                _logger.LogInformation("📤 [CoursesService] Upload course image request. File: {FileName}, Size: {FileSize}, ContentType: {ContentType}", 
                    file?.FileName, file?.Length, file?.ContentType);

                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("❌ [CoursesService] File is null or empty");
                    return BadRequest(ApiResponse.ErrorResponse("File rỗng", 400));
                }

                // Validate file type (only images)
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                var contentType = file.ContentType?.ToLower() ?? "";
                if (!allowedTypes.Contains(contentType))
                {
                    _logger.LogWarning("❌ [CoursesService] Invalid file type: {ContentType}", contentType);
                    return BadRequest(ApiResponse.ErrorResponse($"Chỉ chấp nhận file ảnh (jpg, png, gif, webp). File type: {contentType}", 400));
                }

                // Check Cloudinary initialization
                if (_cloudinary == null)
                {
                    _logger.LogError("❌ [CoursesService] Cloudinary is null - check config");
                    return StatusCode(500, ApiResponse.ErrorResponse("Cloudinary chưa được cấu hình", 500));
                }

                _logger.LogInformation("☁️ [CoursesService] Starting Cloudinary upload to folder: courses/thumbnails");

                await using var stream = file.OpenReadStream();
                var upload = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "courses/thumbnails",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false,
                    Transformation = new Transformation()
                        .Width(800)
                        .Height(450)
                        .Crop("fill")
                        .Quality("auto")
                };

                var result = await _cloudinary.UploadAsync(upload);

                _logger.LogInformation("☁️ [CoursesService] Cloudinary upload result - StatusCode: {StatusCode}, Error: {Error}", 
                    result.StatusCode, result.Error?.Message ?? "None");

                if (result.StatusCode == System.Net.HttpStatusCode.OK || 
                    result.StatusCode == System.Net.HttpStatusCode.Created)
                {
                    var url = result.SecureUrl?.ToString();
                    _logger.LogInformation("✅ [CoursesService] Upload successful: {Url}", url);
                    return Ok(ApiResponse.SuccessResponse(new { url }, "Upload ảnh thành công"));
                }

                _logger.LogError("❌ [CoursesService] Cloudinary upload failed: StatusCode={StatusCode}, Error={Error}", 
                    result.StatusCode, result.Error?.Message ?? "Unknown");
                return StatusCode(500, ApiResponse.ErrorResponse($"Upload thất bại: {result.Error?.Message ?? "Unknown error"}", 500));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [CoursesService] Exception during image upload: {Message} | StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                
                var errorMessage = $"Lỗi hệ thống: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" | Inner: {ex.InnerException.Message}";
                    _logger.LogError("Inner exception: {InnerMessage}", ex.InnerException.Message);
                }
                return StatusCode(500, ApiResponse.ErrorResponse(errorMessage, 500));
            }
        }

        /// <summary>
        /// Đăng ký khóa học
        /// </summary>
        [HttpPost("{id}/enroll")]
        [Authorize]
        public async Task<IActionResult> EnrollCourse(int id, [FromBody] EnrollCourseRequest? request = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không tìm thấy thông tin người dùng", 401));
                }

                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.CourseId == id && !c.HasDelete);

                if (course == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Khóa học không tồn tại", 404));
                }

                // Kiểm tra đã đăng ký chưa
                var existingEnrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == id && e.Status != "Cancelled");

                if (existingEnrollment != null)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bạn đã đăng ký khóa học này rồi", 400));
                }

                // Nếu khóa học có phí, cần xử lý thanh toán
                if (!course.IsFree && course.Price.HasValue && course.Price.Value > 0)
                {
                    // TODO: Xử lý thanh toán nếu cần
                    // Hiện tại chỉ tạo enrollment với status "Pending" nếu chưa thanh toán
                    // Có thể tích hợp với PaymentTransaction sau
                }

                var enrollment = new Enrollment
                {
                    UserId = userId,
                    CourseId = id,
                    EnrollmentDate = DateTime.UtcNow,
                    Status = course.IsFree ? "Active" : "Pending",
                    ProgressPercent = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Enrollments.Add(enrollment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ User {UserId} enrolled in course {CourseId}", userId, id);

                return Ok(ApiResponse.SuccessResponse(new
                {
                    enrollmentId = enrollment.EnrollmentId,
                    courseId = enrollment.CourseId,
                    userId = enrollment.UserId,
                    status = enrollment.Status,
                    enrolledAt = enrollment.EnrollmentDate
                }, "Đăng ký khóa học thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error enrolling course {CourseId}: {Message}", id, ex.Message);
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Thêm đánh giá cho khóa học (chỉ dành cho người đã đăng ký)
        /// </summary>
        [HttpPost("{id}/reviews")]
        [Authorize]
        public async Task<IActionResult> AddCourseReview(int id, [FromBody] AddCourseReviewRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không tìm thấy thông tin người dùng", 401));
                }

                // Kiểm tra khóa học tồn tại
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.CourseId == id && !c.HasDelete);

                if (course == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Khóa học không tồn tại", 404));
                }

                // Kiểm tra user đã đăng ký và hoàn thành khóa học chưa
                var enrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == id && e.Status != "Cancelled");

                if (enrollment == null)
                {
                    return StatusCode(403, ApiResponse.ErrorResponse("Bạn cần đăng ký khóa học trước khi đánh giá", 403));
                }

                // Kiểm tra đã hoàn thành khóa học chưa
                if (enrollment.Status != "Completed")
                {
                    return StatusCode(403, ApiResponse.ErrorResponse("Bạn cần hoàn thành khóa học trước khi đánh giá", 403));
                }

                // Validate rating
                if (request.Rating < 1 || request.Rating > 5)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Đánh giá phải từ 1 đến 5 sao", 400));
                }

                // Kiểm tra đã đánh giá chưa (có thể cho phép cập nhật)
                var existingFeedback = await _context.Feedbacks
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.CourseId == id && !f.HasDelete);

                if (existingFeedback != null)
                {
                    // Cập nhật đánh giá cũ
                    existingFeedback.Rating = request.Rating;
                    existingFeedback.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
                    existingFeedback.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Tạo đánh giá mới
                    var feedback = new Feedback
                    {
                        UserId = userId,
                        CourseId = id,
                        Rating = request.Rating,
                        Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Feedbacks.Add(feedback);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ User {UserId} added review for course {CourseId}", userId, id);

                return Ok(ApiResponse.SuccessResponse(null, "Đánh giá đã được gửi thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error adding course review {CourseId}: {Message}", id, ex.Message);
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Hoàn thành khóa học
        /// </summary>
        [HttpPost("{id}/complete")]
        [Authorize]
        public async Task<IActionResult> CompleteCourse(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không tìm thấy thông tin người dùng", 401));
                }

                // Kiểm tra khóa học tồn tại
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.CourseId == id && !c.HasDelete);

                if (course == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Khóa học không tồn tại", 404));
                }

                // Kiểm tra enrollment
                var enrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == id && e.Status != "Cancelled");

                if (enrollment == null)
                {
                    return StatusCode(403, ApiResponse.ErrorResponse("Bạn chưa đăng ký khóa học này", 403));
                }

                // Cập nhật status thành Completed
                enrollment.Status = "Completed";
                enrollment.ProgressPercent = 100;
                enrollment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ User {UserId} completed course {CourseId}", userId, id);

                return Ok(ApiResponse.SuccessResponse(new
                {
                    enrollmentId = enrollment.EnrollmentId,
                    courseId = enrollment.CourseId,
                    status = enrollment.Status,
                    progressPercent = enrollment.ProgressPercent
                }, "Chúc mừng bạn đã hoàn thành khóa học!"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error completing course {CourseId}: {Message}", id, ex.Message);
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Kiểm tra enrollment status và completion status của user cho một khóa học
        /// </summary>
        [HttpGet("{id}/enrollment-status")]
        [Authorize]
        public async Task<IActionResult> GetEnrollmentStatus(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không tìm thấy thông tin người dùng", 401));
                }

                var enrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == id && e.Status != "Cancelled");

                return Ok(ApiResponse.SuccessResponse(new
                {
                    isEnrolled = enrollment != null,
                    isCompleted = enrollment != null && enrollment.Status == "Completed",
                    enrollmentStatus = enrollment?.Status ?? "NotEnrolled",
                    progressPercent = enrollment?.ProgressPercent ?? 0
                }, "Lấy trạng thái enrollment thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting enrollment status {CourseId}: {Message}", id, ex.Message);
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Lấy danh sách đánh giá của khóa học
        /// </summary>
        [HttpGet("{id}/reviews")]
        public async Task<IActionResult> GetCourseReviews(int id)
        {
            try
            {
                var reviews = await _context.Feedbacks
                    .Include(f => f.User)
                    .Where(f => f.CourseId == id && !f.HasDelete && f.Rating.HasValue)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new
                    {
                        id = f.FeedbackId,
                        name = f.User != null ? f.User.FullName ?? f.User.Email ?? "Người dùng" : "Người dùng",
                        rating = f.Rating ?? 0,
                        comment = f.Comment ?? "",
                        date = f.CreatedAt,
                        avatar = f.User != null ? f.User.AvatarUrl : null
                    })
                    .ToListAsync();

                return Ok(ApiResponse.SuccessResponse(reviews, "Lấy danh sách đánh giá thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting course reviews {CourseId}: {Message}", id, ex.Message);
                return StatusCode(500, ApiResponse.ErrorResponse($"Lỗi hệ thống: {ex.Message}", 500));
            }
        }
    }

    // DTOs
    public class AddCourseReviewRequest
    {
        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }
        
        [MaxLength(1000)]
        public string? Comment { get; set; }
    }

    public class CourseListItemDto
    {
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public int? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public decimal? Price { get; set; }
        public bool IsFree { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int? DurationMinutes { get; set; }
        public string? Level { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateCourseRequest
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? TeacherId { get; set; }

        public int? SubjectId { get; set; }

        public decimal? Price { get; set; }

        public bool? IsFree { get; set; }

        [MaxLength(500)]
        public string? ThumbnailUrl { get; set; }

        public int? DurationMinutes { get; set; }

        [MaxLength(50)]
        public string? Level { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }
    }

    public class UpdateCourseRequest
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? TeacherId { get; set; }

        public int? SubjectId { get; set; }

        public decimal? Price { get; set; }

        public bool? IsFree { get; set; }

        [MaxLength(500)]
        public string? ThumbnailUrl { get; set; }

        public int? DurationMinutes { get; set; }

        [MaxLength(50)]
        public string? Level { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }
    }

    public class EnrollCourseRequest
    {
        public string? PaymentMethod { get; set; }
    }
}

