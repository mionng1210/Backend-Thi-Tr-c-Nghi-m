using MaterialsService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Shared.Contracts.Materials;
using MaterialsService.DTOs;
using MaterialsService.Data;
using MaterialsService.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using API_ThiTracNghiem.Services;
using API_ThiTracNghiem.Middleware;

namespace MaterialsService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaterialsController : ControllerBase
{
    private readonly IMaterialsService _service;
    private readonly MaterialsDbContext _db;
    private readonly IUserSyncService _userSyncService;
    private readonly ILogger<MaterialsController> _logger;
    private readonly IWebHostEnvironment _env;
    
    public MaterialsController(IMaterialsService service, MaterialsDbContext db, IUserSyncService userSyncService, ILogger<MaterialsController> logger, IWebHostEnvironment env)
    {
        _service = service;
        _db = db;
        _userSyncService = userSyncService;
        _logger = logger;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var data = await _service.SearchAsync(search!, pageIndex, pageSize);
            return Ok(data);
        }
        else
        {
            var data = await _service.GetAsync(pageIndex, pageSize);
            return Ok(data);
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var m = await _service.GetByIdAsync(id);
        return m == null ? NotFound() : Ok(m);
    }

    /// <summary>
    /// Lấy danh sách tài liệu theo CourseId
    /// </summary>
    [HttpGet("by-course/{courseId}")]
    public async Task<IActionResult> GetByCourseId(int courseId, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 100)
    {
        try
        {
            if (pageIndex <= 0) pageIndex = 1;
            if (pageSize <= 0) pageSize = 100;
            if (pageSize > 1000) pageSize = 1000; // Max page size

            var query = _db.Materials
                .Where(m => m.CourseId == courseId && !m.HasDelete)
                .OrderBy(m => m.OrderIndex ?? int.MaxValue)
                .ThenBy(m => m.CreatedAt);

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MaterialListItemDto
                {
                    Id = m.MaterialId,
                    Title = m.Title,
                    Description = m.Description,
                    MediaType = m.MediaType,
                    IsPaid = m.IsPaid,
                    Price = m.Price,
                    ExternalLink = m.ExternalLink,
                    FileUrl = m.FileUrl,
                    DurationSeconds = m.DurationSeconds,
                    CourseId = m.CourseId,
                    OrderIndex = m.OrderIndex,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                pageIndex = pageIndex,
                pageSize = pageSize,
                totalItems = totalItems,
                totalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                items = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting materials by courseId {CourseId}", courseId);
            return StatusCode(500, new { message = "Lỗi hệ thống khi lấy tài liệu", error = ex.Message });
        }
    }

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(524288000)]
    public async Task<IActionResult> CreateMany(
        [FromForm] int courseId,
        [FromForm] string? title,
        [FromForm] string? description,
        [FromForm] bool isPaid,
        [FromForm] decimal? price,
        [FromForm] int? orderIndex,
        [FromForm] IFormFileCollection files)
    {
        try
        {
            _logger.LogInformation("📤 CreateMany request: courseId={CourseId}, title={Title}, isPaid={IsPaid}, filesCount={FilesCount}", 
                courseId, title, isPaid, files?.Count ?? 0);
            
            if (files == null || files.Count == 0)
            {
                _logger.LogWarning("❌ No files provided");
                return BadRequest(new { message = "Vui lòng chọn tệp" });
            }

            // Note: MaterialsService doesn't have Courses table, so we skip validation
            // CourseId validation should be done at the API Gateway or ExamsService level
            _logger.LogInformation("📝 Creating materials for courseId={CourseId} (validation skipped - MaterialsService doesn't have Courses table)", courseId);

            var uploaded = await _service.CreateManyAsync(courseId, title, description, isPaid, price, orderIndex, files);
            _logger.LogInformation("✅ Successfully created {Count} materials for course {CourseId}", uploaded.Count, courseId);
            return Ok(uploaded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error creating materials: {Message}. Inner: {InnerException}", 
                ex.Message, ex.InnerException?.Message ?? "None");
            _logger.LogError(ex, "❌ Stack trace: {StackTrace}", ex.StackTrace);
            
            // Return detailed error for debugging
            var errorResponse = new { 
                message = "Lỗi hệ thống khi tạo tài liệu", 
                error = ex.Message, 
                innerException = ex.InnerException?.Message,
                stackTrace = _env.IsDevelopment() ? ex.StackTrace : null
            };
            return StatusCode(500, errorResponse);
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    [RequestSizeLimit(524288000)]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateMaterialForm form)
    {
        var updated = await _service.UpdateAsync(id, form.CourseId, form.Title, form.Description, form.IsPaid, form.Price, form.OrderIndex, form.File);
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok ? Ok() : NotFound();
    }

    /// <summary>
    /// Thanh toán mở khóa tài liệu có phí. Tạo giao dịch và gọi API thanh toán.
    /// </summary>
    [HttpPost("purchase")]
    [Authorize]
    public async Task<IActionResult> Purchase([FromBody] PurchaseMaterialRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Dữ liệu không hợp lệ");
            }

            // Get current user ID from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized("Token không hợp lệ");
            }

            // Check if material exists and is paid
            var material = await _db.Materials
                .Where(m => m.MaterialId == request.MaterialId && !m.HasDelete)
                .FirstOrDefaultAsync();

            if (material == null)
            {
                return NotFound("Không tìm thấy tài liệu");
            }

            if (!material.IsPaid || material.Price == null || material.Price <= 0)
            {
                return BadRequest("Tài liệu này không cần thanh toán");
            }

            // Check if user already purchased this material
            var existingTransaction = await _db.PaymentTransactions
                .Where(t => t.UserId == userId && t.OrderId != null && t.OrderId.Contains($"MAT_{request.MaterialId}_") && t.Status == "Success")
                .FirstOrDefaultAsync();

            if (existingTransaction != null)
            {
                return BadRequest("Bạn đã mua tài liệu này rồi");
            }

            // Generate unique order ID
            var orderId = $"MAT_{request.MaterialId}_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}";

            // Create payment transaction
            var transaction = new PaymentTransaction
            {
                OrderId = orderId,
                UserId = userId,
                Amount = material.Price.Value,
                Currency = request.Currency ?? "VND",
                Gateway = request.Gateway ?? "VNPay",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _db.PaymentTransactions.Add(transaction);
            await _db.SaveChangesAsync();

            // TODO: Integrate with actual payment gateway (VNPay, MoMo, etc.)
            // For now, we'll simulate a payment URL
            var paymentUrl = $"https://payment-gateway.example.com/pay?orderId={orderId}&amount={material.Price}&currency={transaction.Currency}";
            
            // Generate QR code data (simplified)
            var qrCodeData = $"PAY:{orderId}:{material.Price}:{transaction.Currency}";
            
            // Update transaction with payment info
            transaction.QrCodeData = qrCodeData;
            transaction.Payload = $"{{\"materialId\":{material.MaterialId},\"userId\":{userId},\"amount\":{material.Price}}}";
            await _db.SaveChangesAsync();

            var response = new PurchaseMaterialResponse
            {
                TransactionId = transaction.TransactionId,
                OrderId = transaction.OrderId,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                Gateway = transaction.Gateway,
                Status = transaction.Status,
                QrCodeData = transaction.QrCodeData,
                PaymentUrl = paymentUrl,
                CreatedAt = transaction.CreatedAt,
                Material = new MaterialPurchaseInfo
                {
                    MaterialId = material.MaterialId,
                    Title = material.Title,
                    Price = material.Price.Value,
                    MediaType = material.MediaType
                }
            };

            return Ok(response);
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống khi tạo giao dịch: {ex.Message}");
        }
    }

    /// <summary>
    /// Demo User Sync - Lấy thông tin user hiện tại từ middleware
    /// </summary>
    [HttpGet("user-sync-demo")]
    [Authorize]
    public IActionResult GetUserSyncDemo()
    {
        try
        {
            // Sử dụng HttpContext Extension từ middleware
            var syncedUser = HttpContext.GetSyncedUser();
            var userId = HttpContext.GetSyncedUserId();
            var userRole = HttpContext.GetSyncedUserRole();

            if (syncedUser == null)
            {
                return Unauthorized("User not found or invalid token");
            }

            _logger.LogInformation($"User {syncedUser.FullName} ({syncedUser.Email}) is accessing materials user sync demo");

            return Ok(new
            {
                Message = "Materials User sync demo - Thông tin user được đồng bộ từ AuthService",
                ServiceName = "MaterialsService (Port 5003)",
                SyncedUser = syncedUser,
                Permissions = new
                {
                    IsAdmin = HttpContext.IsAdmin(),
                    IsTeacher = HttpContext.IsTeacher(),
                    IsStudent = HttpContext.IsStudent()
                },
                AccessTime = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in materials user sync demo");
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    /// <summary>
    /// Demo User Sync - Kiểm tra quyền truy cập tài liệu theo role
    /// </summary>
    [HttpGet("access-check-demo/{materialId}")]
    [Authorize]
    public async Task<IActionResult> GetMaterialAccessDemo(int materialId)
    {
        try
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Missing or invalid authorization header");
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _userSyncService.GetUserFromTokenAsync(token);

            if (user == null)
            {
                return Unauthorized("Invalid token or user not found");
            }

            // Kiểm tra quyền truy cập tài liệu
            var material = await _db.Materials.FindAsync(materialId);
            if (material == null)
            {
                return NotFound($"Material with ID {materialId} not found");
            }

            // Logic kiểm tra quyền truy cập
            bool hasAccess = user.RoleName?.ToLower() switch
            {
                "admin" => true, // Admin có thể truy cập tất cả
                "teacher" => true, // Teacher có thể truy cập tất cả tài liệu
                "student" => !material.IsPaid || material.Price == 0, // Student chỉ truy cập tài liệu miễn phí
                _ => false
            };

            _logger.LogInformation($"Material access check for user {user.FullName}: Material={materialId}, Access={hasAccess}");

            return Ok(new
            {
                Message = "Material access check demo - Kiểm tra quyền truy cập từ AuthService",
                ServiceName = "MaterialsService (Port 5003)",
                User = user,
                Material = new
                {
                    material.MaterialId,
                    material.Title,
                    material.IsPaid,
                    material.Price,
                    material.MediaType
                },
                AccessResult = new
                {
                    HasAccess = hasAccess,
                    Reason = hasAccess ? "Access granted" : 
                            user.RoleName?.ToLower() == "student" ? "Students can only access free materials" : 
                            "Access denied"
                },
                CheckTime = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in material access check demo");
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }
}


