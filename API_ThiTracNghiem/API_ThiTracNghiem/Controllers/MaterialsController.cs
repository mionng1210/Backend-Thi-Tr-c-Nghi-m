using System.Threading.Tasks;
using API_ThiTracNghiem.Contracts;
using API_ThiTracNghiem.Infrastructure;
using API_ThiTracNghiem.Services;
using API_ThiTracNghiem.Data;
using API_ThiTracNghiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API_ThiTracNghiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MaterialsController : ControllerBase
    {
        private readonly IMaterialsService _service;
        private readonly ILogger<MaterialsController> _logger;
        private readonly ApplicationDbContext _db;

        public MaterialsController(IMaterialsService service, ILogger<MaterialsController> logger, ApplicationDbContext db)
        {
            _service = service;
            _logger = logger;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] PagedRequest request)
        {
            try
            {
                var pagedData = await _service.GetAsync(request.PageIndex, request.PageSize);
                
                // Create response with pagination info
                var response = new
                {
                    pageIndex = pagedData.PageIndex,
                    pageSize = pagedData.PageSize,
                    totalItems = pagedData.TotalItems,
                    totalPages = (int)Math.Ceiling((double)pagedData.TotalItems / pagedData.PageSize),
                    items = pagedData.Items
                };
                
                return Ok(ApiResponse.Success(response, "Lấy danh sách tài liệu thành công"));
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting materials list");
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi lấy danh sách tài liệu", 500));
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết tài liệu theo ID
        /// </summary>
        /// <param name="id">ID của tài liệu</param>
        /// <returns>Thông tin chi tiết tài liệu</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                // Validate input
                if (id <= 0)
                {
                    return BadRequest(ApiResponse.Fail("ID tài liệu không hợp lệ", 400));
                }

                var material = await _service.GetByIdAsync(id);

                if (material == null)
                {
                    return NotFound(ApiResponse.Fail("Không tìm thấy tài liệu với ID đã cho", 404));
                }

                return Ok(ApiResponse.Success(material, "Lấy thông tin tài liệu thành công"));
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting material with ID: {MaterialId}", id);
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi lấy thông tin tài liệu", 500));
            }
        }

        /// <summary>
        /// Tạo mới tài liệu (hỗ trợ nhiều file). Nhận form-data gồm metadata + files[]
        /// </summary>
        /// <param name="courseId">ID khóa học</param>
        /// <param name="title">Tiêu đề áp dụng chung nếu cung cấp</param>
        /// <param name="description">Mô tả áp dụng chung nếu cung cấp</param>
        /// <param name="isPaid">Đánh dấu tài liệu trả phí</param>
        /// <param name="price">Giá (áp dụng khi isPaid = true)</param>
        /// <param name="orderIndex">Thứ tự bắt đầu (tăng dần theo số file)</param>
        /// <param name="files">Danh sách tệp upload</param>
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        [RequestSizeLimit(524288000)] // 500 MB
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
                if (courseId <= 0)
                {
                    return BadRequest(ApiResponse.Fail("CourseId không hợp lệ", 400));
                }

                if ((isPaid && (price == null || price <= 0)))
                {
                    return BadRequest(ApiResponse.Fail("Giá phải lớn hơn 0 khi là tài liệu trả phí", 400));
                }

                if (files == null || files.Count == 0)
                {
                    return BadRequest(ApiResponse.Fail("Vui lòng chọn ít nhất một tệp để tải lên", 400));
                }

                var uploaded = await _service.CreateManyAsync(courseId, title, description, isPaid, price, orderIndex, files);
                return Ok(ApiResponse.Success(uploaded, "Tạo tài liệu thành công"));
            }
            catch (System.ArgumentException aex)
            {
                return BadRequest(ApiResponse.Fail(aex.Message, 400));
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating materials");
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi tạo tài liệu", 500));
            }
        }

        /// <summary>
        /// Cập nhật tài liệu theo ID. Nhận form-data, file là tùy chọn.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> Update(int id, [FromForm] UpdateMaterialForm form)
        {
            try
            {
                if (id <= 0) return BadRequest(ApiResponse.Fail("ID không hợp lệ", 400));
                if (form.IsPaid == true && (form.Price == null || form.Price <= 0))
                {
                    return BadRequest(ApiResponse.Fail("Giá phải lớn hơn 0 khi là tài liệu trả phí", 400));
                }

                var updated = await _service.UpdateAsync(id, form.CourseId, form.Title, form.Description, form.IsPaid, form.Price, form.OrderIndex, form.File);
                if (updated == null) return NotFound(ApiResponse.Fail("Không tìm thấy tài liệu", 404));
                return Ok(ApiResponse.Success(updated, "Cập nhật tài liệu thành công"));
            }
            catch (System.ArgumentException aex)
            {
                return BadRequest(ApiResponse.Fail(aex.Message, 400));
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating material {MaterialId}", id);
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi cập nhật tài liệu", 500));
            }
        }

        /// <summary>
        /// Xóa tài liệu theo ID. Xóa cả file khỏi storage (Cloudinary/Supabase).
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (id <= 0) return BadRequest(ApiResponse.Fail("ID không hợp lệ", 400));

                var deleted = await _service.DeleteAsync(id);
                if (!deleted) return NotFound(ApiResponse.Fail("Không tìm thấy tài liệu hoặc đã bị xóa", 404));

                return Ok(ApiResponse.Success(null, "Xóa tài liệu thành công"));
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting material {MaterialId}", id);
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi xóa tài liệu", 500));
            }
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
                    return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ", 400));
                }

                // Get current user ID from JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(ApiResponse.Fail("Token không hợp lệ", 401));
                }

                // Check if material exists and is paid
                var material = await _db.Materials
                    .Where(m => m.MaterialId == request.MaterialId && !m.HasDelete)
                    .FirstOrDefaultAsync();

                if (material == null)
                {
                    return NotFound(ApiResponse.Fail("Không tìm thấy tài liệu", 404));
                }

                if (!material.IsPaid || material.Price == null || material.Price <= 0)
                {
                    return BadRequest(ApiResponse.Fail("Tài liệu này không cần thanh toán", 400));
                }

                // Check if user already purchased this material
                var existingTransaction = await _db.PaymentTransactions
                    .Where(t => t.UserId == userId && t.OrderId != null && t.OrderId.Contains($"MAT_{request.MaterialId}_") && t.Status == "Success")
                    .FirstOrDefaultAsync();

                if (existingTransaction != null)
                {
                    return BadRequest(ApiResponse.Fail("Bạn đã mua tài liệu này rồi", 400));
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

                return Ok(ApiResponse.Success(response, "Tạo giao dịch thanh toán thành công"));
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating purchase transaction for material {MaterialId}", request.MaterialId);
                return StatusCode(500, ApiResponse.Fail("Lỗi hệ thống khi tạo giao dịch", 500));
            }
        }
    }
}


