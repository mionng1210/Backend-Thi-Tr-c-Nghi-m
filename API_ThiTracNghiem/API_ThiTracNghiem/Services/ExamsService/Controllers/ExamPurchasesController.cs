using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamsService.Data;
using ExamsService.DTOs;
using ExamsService.Models;
using API_ThiTracNghiem.Middleware;

namespace ExamsService.Controllers
{
    [ApiController]
    [Route("api/Exams")]
    public class ExamPurchasesController : ControllerBase
    {
        private readonly ExamsDbContext _context;
        private readonly ILogger<ExamPurchasesController> _logger;

        public ExamPurchasesController(ExamsDbContext context, ILogger<ExamPurchasesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Mua bài thi có phí: tạo giao dịch thanh toán và (tuỳ chọn) xác nhận ngay.
        /// </summary>
        [HttpPost("{examId}/purchase")]
        [Authorize]
        public async Task<IActionResult> PurchaseExam(int examId, [FromBody] PurchaseExamRequest request)
        {
            try
            {
                var userId = HttpContext.GetSyncedUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                // Load exam with course pricing
                var exam = await _context.Exams.Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.ExamId == examId && !e.HasDelete);

                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Bài thi không tồn tại", 404));
                }

                if (exam.Status != "Active")
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bài thi chưa được kích hoạt", 400));
                }

                var price = exam.Course?.Price ?? 0;
                var isFree = exam.Course?.IsFree == true || price <= 0;
                if (isFree)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bài thi này không cần thanh toán", 400));
                }

                // Idempotency: has successful transaction already?
                var existingSuccess = await _context.Set<PaymentTransaction>()
                    .Where(t => t.UserId == userId.Value && t.OrderId != null && t.OrderId.Contains($"EXAM_{examId}_") && t.Status == "Success")
                    .FirstOrDefaultAsync();

                if (existingSuccess != null)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bạn đã mua bài thi này rồi", 400));
                }

                var orderId = $"EXAM_{examId}_{userId.Value}_{DateTime.UtcNow:yyyyMMddHHmmss}";

                var transaction = new PaymentTransaction
                {
                    OrderId = orderId,
                    UserId = userId.Value,
                    Amount = price,
                    Currency = request.Currency ?? "VND",
                    Gateway = request.Gateway ?? "VNPay",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Add(transaction);
                await _context.SaveChangesAsync();

                // Simulate payment URL and QR code
                var paymentUrl = $"https://payment-gateway.example.com/pay?orderId={orderId}&amount={price}&currency={transaction.Currency}";
                var qrData = $"PAY:{orderId}:{price}:{transaction.Currency}";

                transaction.QrCodeData = qrData;
                transaction.Payload = $"{{\"examId\":{exam.ExamId},\"userId\":{userId.Value},\"amount\":{price}}}";
                if (request.SimulateSuccess)
                {
                    transaction.Status = "Success";
                    transaction.PaidAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();

                // If payment is successful (simulated), ensure an active enrollment exists
                if (transaction.Status == "Success")
                {
                    var existingEnrollment = await _context.Set<ExamEnrollment>()
                        .FirstOrDefaultAsync(en => en.ExamId == exam.ExamId && en.UserId == userId.Value && !en.HasDelete);

                    if (existingEnrollment == null)
                    {
                        var enrollment = new ExamEnrollment
                        {
                            ExamId = exam.ExamId,
                            UserId = userId.Value,
                            Status = "Active",
                            CreatedAt = DateTime.UtcNow,
                            HasDelete = false
                        };
                        _context.Add(enrollment);
                        await _context.SaveChangesAsync();
                    }
                    else if (existingEnrollment.Status != "Active")
                    {
                        existingEnrollment.Status = "Active";
                        await _context.SaveChangesAsync();
                    }
                }

                var response = new PurchaseExamResponse
                {
                    TransactionId = transaction.TransactionId,
                    OrderId = transaction.OrderId ?? string.Empty,
                    Amount = transaction.Amount,
                    Currency = transaction.Currency ?? string.Empty,
                    Gateway = transaction.Gateway ?? string.Empty,
                    Status = transaction.Status ?? string.Empty,
                    QrCodeData = transaction.QrCodeData,
                    PaymentUrl = paymentUrl,
                    CreatedAt = transaction.CreatedAt,
                    Exam = new ExamPurchaseInfo
                    {
                        ExamId = exam.ExamId,
                        ExamTitle = exam.Title,
                        CourseTitle = exam.Course?.Title,
                        Price = price,
                        IsCourseFree = exam.Course?.IsFree == true
                    },
                    EnrollmentStatus = transaction.Status == "Success" ? "Active" : "Pending"
                };

                return Ok(ApiResponse.SuccessResponse(response, "Tạo giao dịch thanh toán thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while purchasing exam {ExamId}", examId);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi tạo giao dịch", 500));
            }
        }
    }
}