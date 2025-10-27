using MaterialsService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Materials;
using MaterialsService.DTOs;
using MaterialsService.Data;
using MaterialsService.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MaterialsService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaterialsController : ControllerBase
{
    private readonly IMaterialsService _service;
    private readonly MaterialsDbContext _db;
    public MaterialsController(IMaterialsService service, MaterialsDbContext db)
    {
        _service = service;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
    {
        var data = await _service.GetAsync(pageIndex, pageSize);
        return Ok(data);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var m = await _service.GetByIdAsync(id);
        return m == null ? NotFound() : Ok(m);
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
        if (files == null || files.Count == 0) return BadRequest("Vui lòng chọn tệp");
        var uploaded = await _service.CreateManyAsync(courseId, title, description, isPaid, price, orderIndex, files);
        return Ok(uploaded);
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
}


