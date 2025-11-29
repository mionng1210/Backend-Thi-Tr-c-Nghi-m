using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamsService.Data;
using ExamsService.DTOs;
using ExamsService.Models;
using API_ThiTracNghiem.Middleware;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Security.Claims;

namespace ExamsService.Controllers
{
    [ApiController]
    [Route("api/Exams")]
    public class ExamPurchasesController : ControllerBase
    {
        private readonly ExamsDbContext _context;
        private readonly ILogger<ExamPurchasesController> _logger;
        private readonly ExamsService.Services.PayOSClient _payOS;

        private DateTime NowVn()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                }
                catch
                {
                    return DateTime.UtcNow.AddHours(7);
                }
            }
        }

        public ExamPurchasesController(ExamsDbContext context, ILogger<ExamPurchasesController> logger, ExamsService.Services.PayOSClient payOS)
        {
            _context = context;
            _logger = logger;
            _payOS = payOS;
        }

        [HttpGet("payments")]
        [Authorize]
        public async Task<IActionResult> GetPayments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? gateway = null,
            [FromQuery] string? search = null
        )
        {
            try
            {
                var role = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;
                var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
                if (!isAdmin)
                {
                    return Forbid("Chỉ admin mới có thể truy cập endpoint này");
                }

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var q = _context.Set<PaymentTransaction>().Include(t => t.User).AsQueryable();
                if (!string.IsNullOrWhiteSpace(status))
                {
                    var sLower = status.ToLower();
                    q = q.Where(t => t.Status != null && t.Status.ToLower() == sLower);
                }
                if (!string.IsNullOrWhiteSpace(gateway))
                {
                    var gLower = gateway.ToLower();
                    q = q.Where(t => t.Gateway != null && t.Gateway.ToLower() == gLower);
                }
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.ToLower();
                    q = q.Where(t =>
                        (t.OrderId != null && t.OrderId.ToLower().Contains(s)) ||
                        (t.User != null && (
                            (t.User.Email != null && t.User.Email.ToLower().Contains(s)) ||
                            (t.User.FullName != null && t.User.FullName.ToLower().Contains(s))
                        ))
                    );
                }

                var totalCount = await q.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var raw = await q
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new
                    {
                        transactionId = t.TransactionId,
                        orderId = t.OrderId,
                        status = t.Status,
                        amount = t.Amount,
                        currency = t.Currency,
                        gateway = t.Gateway,
                        paidAt = t.PaidAt,
                        createdAt = t.CreatedAt,
                        user = new { id = t.UserId, name = t.User != null ? t.User.FullName : null, email = t.User != null ? t.User.Email : null },
                        payload = t.Payload
                    })
                    .ToListAsync();

                var items = raw.Select(r => new
                {
                    r.transactionId,
                    r.orderId,
                    r.status,
                    r.amount,
                    r.currency,
                    r.gateway,
                    r.paidAt,
                    r.createdAt,
                    r.user,
                    examInfo = BuildExamInfo(r.payload)
                }).ToList();

                return Ok(new { data = items, totalCount, page, pageSize, totalPages });

                object? BuildExamInfo(string? payload)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(payload)) return null;
                        using var doc = System.Text.Json.JsonDocument.Parse(payload);
                        var root = doc.RootElement;
                        int examId = 0;
                        string? buyerName = null;
                        string? buyerEmail = null;
                        if (root.TryGetProperty("examId", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.Number)
                            examId = e.GetInt32();
                        if (root.TryGetProperty("buyerName", out var bn) && bn.ValueKind == System.Text.Json.JsonValueKind.String)
                            buyerName = bn.GetString();
                        if (root.TryGetProperty("buyerEmail", out var be) && be.ValueKind == System.Text.Json.JsonValueKind.String)
                            buyerEmail = be.GetString();
                        string? examTitle = null;
                        decimal? price = null;
                        if (examId > 0)
                        {
                            var exam = _context.Exams.Include(x => x.Course).FirstOrDefault(x => x.ExamId == examId);
                            if (exam != null)
                            {
                                examTitle = exam.Title;
                                price = exam.Price ?? exam.Course?.Price;
                            }
                        }
                        return new { examId, examTitle, buyerName, buyerEmail, price };
                    }
                    catch { return null; }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while listing payments");
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy danh sách thanh toán", 500));
            }
        }

        [HttpGet("payments/{id}")]
        [Authorize]
        public async Task<IActionResult> GetPaymentById(int id)
        {
            try
            {
                var role = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;
                var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
                if (!isAdmin)
                {
                    return Forbid("Chỉ admin mới có thể truy cập endpoint này");
                }

                var t = await _context.Set<PaymentTransaction>().Include(x => x.User).FirstOrDefaultAsync(x => x.TransactionId == id);
                if (t == null) return NotFound(new { message = "Không tìm thấy giao dịch" });

                var data = new
                {
                    transactionId = t.TransactionId,
                    orderId = t.OrderId,
                    status = t.Status,
                    amount = t.Amount,
                    currency = t.Currency,
                    gateway = t.Gateway,
                    gatewayTransactionId = t.GatewayTransactionId,
                    paidAt = t.PaidAt,
                    createdAt = t.CreatedAt,
                    qrCodeData = t.QrCodeData,
                    payload = t.Payload,
                    user = new { id = t.UserId, name = t.User?.FullName, email = t.User?.Email },
                    examInfo = BuildExamInfo(t.Payload)
                };
                return Ok(data);

                object? BuildExamInfo(string? payload)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(payload)) return null;
                        using var doc = System.Text.Json.JsonDocument.Parse(payload);
                        var root = doc.RootElement;
                        int examId = 0;
                        string? buyerName = null;
                        string? buyerEmail = null;
                        if (root.TryGetProperty("examId", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.Number)
                            examId = e.GetInt32();
                        if (root.TryGetProperty("buyerName", out var bn) && bn.ValueKind == System.Text.Json.JsonValueKind.String)
                            buyerName = bn.GetString();
                        if (root.TryGetProperty("buyerEmail", out var be) && be.ValueKind == System.Text.Json.JsonValueKind.String)
                            buyerEmail = be.GetString();
                        string? examTitle = null;
                        decimal? price = null;
                        if (examId > 0)
                        {
                            var exam = _context.Exams.Include(x => x.Course).FirstOrDefault(x => x.ExamId == examId);
                            if (exam != null)
                            {
                                examTitle = exam.Title;
                                price = exam.Price ?? exam.Course?.Price;
                            }
                        }
                        return new { examId, examTitle, buyerName, buyerEmail, price };
                    }
                    catch { return null; }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting payment detail {Id}", id);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy chi tiết thanh toán", 500));
            }
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

                if (!(string.Equals(exam.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(exam.Status, "Published", StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bài thi chưa được kích hoạt", 400));
                }

                decimal price = exam.Price ?? exam.Course?.Price ?? 0m;
                var isFree = (exam.Course?.IsFree == true) || price <= 0m;
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

                var orderId = $"EXAM_{examId}_{userId.Value}_{NowVn():yyyyMMddHHmmss}";

                var transaction = new PaymentTransaction
                {
                    OrderId = orderId,
                    UserId = userId.Value,
                    Amount = price,
                    Currency = request.Currency ?? "VND",
                    Gateway = request.Gateway ?? "VNPay",
                    Status = "Pending",
                    CreatedAt = NowVn()
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
                    transaction.PaidAt = NowVn();
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
                            CreatedAt = NowVn(),
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

        [HttpPost("{examId}/purchase/payos")]
        [Authorize]
        public async Task<IActionResult> PurchaseExamPayOS(int examId, [FromBody] System.Text.Json.JsonElement? body)
        {
            try
            {
                var currentUserId = HttpContext.GetSyncedUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                var exam = await _context.Exams.Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.ExamId == examId && !e.HasDelete);
                if (exam == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Bài thi không tồn tại", 404));
                }
                if (!(string.Equals(exam.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(exam.Status, "Published", StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bài thi chưa được kích hoạt", 400));
                }
                decimal price = exam.Price ?? exam.Course?.Price ?? 0m;
                var isFree = (exam.Course?.IsFree == true) || price <= 0m;
                if (isFree)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Bài thi này không cần thanh toán", 400));
                }

                var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string ReadString(string name)
                {
                    if (body.HasValue && body.Value.TryGetProperty(name, out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String)
                        return e.GetString() ?? string.Empty;
                    return string.Empty;
                }
                // Cho phép Admin tạo link cho học viên khác (buyer)
                var role = HttpContext.GetSyncedUserRole() ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;
                int targetUserId = currentUserId.Value;
                if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    int? buyerUserId = null;
                    if (body.HasValue && body.Value.TryGetProperty("buyerUserId", out var buid) && buid.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        buyerUserId = buid.GetInt32();
                    }
                    var buyerEmailInput = ReadString("buyerEmail");
                    if (buyerUserId.HasValue)
                    {
                        var buyer = await _context.Users.FirstOrDefaultAsync(u => u.UserId == buyerUserId.Value && !u.HasDelete);
                        if (buyer != null)
                        {
                            targetUserId = buyer.UserId;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(buyerEmailInput))
                    {
                        var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == buyerEmailInput.ToLower() && !u.HasDelete);
                        if (buyer != null)
                        {
                            targetUserId = buyer.UserId;
                        }
                    }
                }
                var description = ReadString("description");
                if (string.IsNullOrWhiteSpace(description)) description = $"Thanh toán bài thi {exam.Title}";
                var returnUrl = ReadString("returnUrl");
                var cancelUrl = ReadString("cancelUrl");

                var amountInt = (int)Math.Round(price);
                var items = new List<Net.payOS.Types.ItemData>
                {
                    new Net.payOS.Types.ItemData(exam.Title ?? $"Exam {exam.ExamId}", 1, amountInt)
                };

                if (!_payOS.IsConfigured)
                {
                    return StatusCode(503, ApiResponse.ErrorResponse("Thiếu cấu hình PayOS (CLIENT_ID/API_KEY/CHECKSUM_KEY)", 503));
                }

                try
                {
                    var create = await _payOS.CreatePaymentLink(orderCode, amountInt, description, returnUrl, cancelUrl, items);

                    var transaction = new PaymentTransaction
                    {
                        OrderId = orderCode.ToString(),
                        UserId = targetUserId,
                        Amount = price,
                        Currency = "VND",
                        Gateway = "PayOS",
                        GatewayTransactionId = create.paymentLinkId,
                        Status = "Pending",
                        QrCodeData = create.qrCode,
                        Payload = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            examId = exam.ExamId,
                            bin = create.bin,
                            accountNumber = create.accountNumber,
                            amount = create.amount,
                            description = create.description,
                            buyerName = ReadString("buyerName"),
                            buyerEmail = ReadString("buyerEmail"),
                            buyerPhone = ReadString("buyerPhone"),
                            returnUrl,
                            cancelUrl
                        })
                    };
                    _context.Add(transaction);
                    await _context.SaveChangesAsync();

                    var result = new
                    {
                        bin = create.bin,
                        accountNumber = create.accountNumber,
                        amount = create.amount,
                        description = create.description,
                        orderCode = create.orderCode,
                        checkoutUrl = create.checkoutUrl,
                        qrCode = create.qrCode
                    };

                    return Ok(ApiResponse.SuccessResponse(result, "Tạo liên kết thanh toán PayOS thành công"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while creating PayOS link for exam {ExamId}", examId);
                    var baseMsg = ex.InnerException?.Message ?? ex.Message;
                    var msg = string.IsNullOrWhiteSpace(baseMsg) ? "Lỗi hệ thống khi tạo liên kết thanh toán" : baseMsg;
                    var status = 500;
                    if (!_payOS.IsConfigured)
                    {
                        status = 503;
                        msg = "Thiếu cấu hình PayOS (CLIENT_ID/API_KEY/CHECKSUM_KEY)";
                    }
                    else if (ex is HttpRequestException)
                    {
                        status = 502;
                    }
                    else
                    {
                        var m = msg.ToLowerInvariant();
                        if (m.Contains("unauthorized") || m.Contains("401") || m.Contains("forbidden") || m.Contains("403")) status = 502;
                        else if (m.Contains("bad request") || m.Contains("400") || m.Contains("invalid") || m.Contains("argument") || m.Contains("null")) status = 400;
                    }
                    return StatusCode(status, ApiResponse.ErrorResponse(msg, status));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating PayOS link for exam {ExamId}", examId);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi tạo liên kết thanh toán", 500));
            }
        }

        [HttpGet("payos/order/{orderCode}")]
        [Authorize]
        public async Task<IActionResult> GetPayOSOrder(long orderCode)
        {
            try
            {
                if (!_payOS.IsConfigured)
                {
                    return StatusCode(503, ApiResponse.ErrorResponse("Thiếu cấu hình PayOS (CLIENT_ID/API_KEY/CHECKSUM_KEY)", 503));
                }
                var info = await _payOS.GetPaymentLinkInformation(orderCode);
                var tx = await _context.Set<PaymentTransaction>().FirstOrDefaultAsync(t => t.OrderId == orderCode.ToString());
                if (tx != null)
                {
                    if ((info.amountPaid >= info.amount) || info.status == "PAID")
                    {
                        if (tx.Status != "Success")
                        {
                            tx.Status = "Success";
                            tx.PaidAt = NowVn();
                            await _context.SaveChangesAsync();
                            var payload = tx.Payload;
                            int examId = 0;
                            try
                            {
                                var d = System.Text.Json.JsonDocument.Parse(payload);
                                if (d.RootElement.TryGetProperty("examId", out var e)) examId = e.GetInt32();
                            }
                            catch {}
                            if (examId > 0)
                            {
                                var userId = tx.UserId;
                                var existingEnrollment = await _context.Set<ExamEnrollment>().FirstOrDefaultAsync(en => en.ExamId == examId && en.UserId == userId && !en.HasDelete);
                                if (existingEnrollment == null)
                                {
                                    var enrollment = new ExamEnrollment { ExamId = examId, UserId = userId, Status = "Active", CreatedAt = NowVn(), HasDelete = false };
                                    _context.Add(enrollment);
                                    await _context.SaveChangesAsync();
                                }
                                else if (existingEnrollment.Status != "Active")
                                {
                                    existingEnrollment.Status = "Active";
                                    await _context.SaveChangesAsync();
                                }
                            }
                        }
                    }
                }
                if (tx != null && tx.Status == "Success")
                {
                    var paidInfo = new
                    {
                        orderCode = orderCode,
                        amount = (int)Math.Round(tx.Amount),
                        amountPaid = (int)Math.Round(tx.Amount),
                        status = "PAID"
                    };
                    return Ok(ApiResponse.SuccessResponse(paidInfo, "Lấy trạng thái đơn PayOS thành công"));
                }
                return Ok(ApiResponse.SuccessResponse(info, "Lấy trạng thái đơn PayOS thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting PayOS order info {OrderCode}", orderCode);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy trạng thái đơn", 500));
            }
        }

        [HttpPost("payos/order/{orderCode}/cancel")]
        [Authorize]
        public async Task<IActionResult> CancelPayOSOrder(long orderCode, [FromBody] dynamic body)
        {
            try
            {
                var reason = (string)(body?.cancellationReason ?? "");
                object? info = null;
                try
                {
                    if (_payOS.IsConfigured)
                    {
                        info = await _payOS.CancelPaymentLink(orderCode, reason);
                    }
                }
                catch
                {
                }

                var tx = await _context.Set<PaymentTransaction>().FirstOrDefaultAsync(t => t.OrderId == orderCode.ToString());
                if (tx != null)
                {
                    tx.Status = "Canceled";
                    var payload = tx.Payload;
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(payload ?? "{}");
                        var root = doc.RootElement;
                        var updated = new System.Text.Json.Nodes.JsonObject();
                        foreach (var p in root.EnumerateObject())
                        {
                            updated[p.Name] = System.Text.Json.Nodes.JsonNode.Parse(p.Value.GetRawText());
                        }
                        updated["cancellationReason"] = reason;
                        updated["canceledAt"] = NowVn().ToString("o");
                        tx.Payload = updated.ToJsonString();
                    }
                    catch
                    {
                        tx.Payload = System.Text.Json.JsonSerializer.Serialize(new { cancellationReason = reason, canceledAt = NowVn() });
                    }
                    await _context.SaveChangesAsync();
                }

                return Ok(ApiResponse.SuccessResponse(info ?? new { orderCode, status = "CANCELED" }, "Huỷ liên kết thanh toán PayOS thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while canceling PayOS order {OrderCode}", orderCode);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi huỷ liên kết thanh toán", 500));
            }
        }

        [HttpGet("payos/order/{orderCode}/invoices")]
        [Authorize]
        public async Task<IActionResult> GetPayOSInvoices(long orderCode)
        {
            try
            {
                var clientId = Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID") ?? (HttpContext.RequestServices.GetService<IConfiguration>()?["PAYOS_CLIENT_ID"] ?? string.Empty);
                var apiKey = Environment.GetEnvironmentVariable("PAYOS_API_KEY") ?? (HttpContext.RequestServices.GetService<IConfiguration>()?["PAYOS_API_KEY"] ?? string.Empty);
                var url = $"https://api-merchant.payos.vn/v2/payment-requests/{orderCode}/invoices";
                using var client = new HttpClient();
                if (!string.IsNullOrWhiteSpace(clientId)) client.DefaultRequestHeaders.Add("x-client-id", clientId);
                if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                var res = await client.GetAsync(url);
                var content = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    return StatusCode((int)res.StatusCode, content);
                }
                var json = JsonDocument.Parse(content);
                return Ok(ApiResponse.SuccessResponse(json.RootElement, "Lấy thông tin hóa đơn thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting PayOS invoices for {OrderCode}", orderCode);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy thông tin hóa đơn", 500));
            }
        }

        [HttpGet("payos/order/{orderCode}/invoices/{invoiceId}/download")]
        [Authorize]
        public async Task<IActionResult> DownloadPayOSInvoice(long orderCode, string invoiceId)
        {
            try
            {
                var clientId = Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID") ?? (HttpContext.RequestServices.GetService<IConfiguration>()?["PAYOS_CLIENT_ID"] ?? string.Empty);
                var apiKey = Environment.GetEnvironmentVariable("PAYOS_API_KEY") ?? (HttpContext.RequestServices.GetService<IConfiguration>()?["PAYOS_API_KEY"] ?? string.Empty);
                var url = $"https://api-merchant.payos.vn/v2/payment-requests/{orderCode}/invoices/{invoiceId}/download";
                using var client = new HttpClient();
                if (!string.IsNullOrWhiteSpace(clientId)) client.DefaultRequestHeaders.Add("x-client-id", clientId);
                if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                var res = await client.GetAsync(url);
                var bytes = await res.Content.ReadAsByteArrayAsync();
                if (!res.IsSuccessStatusCode)
                {
                    return StatusCode((int)res.StatusCode);
                }
                return File(bytes, "application/pdf", $"invoice-{invoiceId}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while downloading PayOS invoice {InvoiceId} for {OrderCode}", invoiceId, orderCode);
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi tải hóa đơn", 500));
            }
        }

        [HttpPost("payos/confirm-webhook")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ConfirmPayOSWebhook([FromBody] dynamic body)
        {
            try
            {
                var webhookUrl = (string)(body?.webhookUrl ?? "");
                if (string.IsNullOrWhiteSpace(webhookUrl))
                {
                    return BadRequest(ApiResponse.ErrorResponse("WebhookUrl không được để trống", 400));
                }

                var clientId = Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID") ?? (HttpContext.RequestServices.GetService<IConfiguration>()?["PAYOS_CLIENT_ID"] ?? string.Empty);
                var apiKey = Environment.GetEnvironmentVariable("PAYOS_API_KEY") ?? (HttpContext.RequestServices.GetService<IConfiguration>()?["PAYOS_API_KEY"] ?? string.Empty);
                using var client = new HttpClient();
                if (!string.IsNullOrWhiteSpace(clientId)) client.DefaultRequestHeaders.Add("x-client-id", clientId);
                if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Add("x-api-key", apiKey);

                var json = System.Text.Json.JsonSerializer.Serialize(new { webhookUrl });
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var res = await client.PostAsync("https://api-merchant.payos.vn/confirm-webhook", content);
                var text = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    return StatusCode((int)res.StatusCode, text);
                }
                var doc = System.Text.Json.JsonDocument.Parse(text);
                return Ok(ApiResponse.SuccessResponse(doc.RootElement, "Xác thực và cập nhật webhook thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while confirming PayOS webhook");
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi xác thực webhook", 500));
            }
        }

        [HttpPost("payos/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PayOSWebhook()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var bodyText = await reader.ReadToEndAsync();
                var bodyObj = System.Text.Json.JsonSerializer.Deserialize<Net.payOS.Types.WebhookType>(bodyText);
                var data = _payOS.VerifyWebhook(bodyObj!);

                var tx = await _context.Set<PaymentTransaction>().FirstOrDefaultAsync(t => t.OrderId == data.orderCode.ToString());
                if (tx != null)
                {
                    tx.Status = "Success";
                    tx.PaidAt = NowVn();
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(tx.Payload ?? "{}");
                        var root = doc.RootElement;
                        var updated = new System.Text.Json.Nodes.JsonObject();
                        foreach (var p in root.EnumerateObject())
                        {
                            updated[p.Name] = System.Text.Json.Nodes.JsonNode.Parse(p.Value.GetRawText());
                        }
                        updated["orderCode"] = data.orderCode;
                        updated["amount"] = data.amount;
                        updated["amountPaid"] = data.amount;
                        updated["status"] = "PAID";
                        updated["reference"] = data.reference;
                        updated["accountNumber"] = data.accountNumber;
                        updated["currency"] = data.currency;
                        updated["paymentLinkId"] = data.paymentLinkId;
                        updated["transactionDateTime"] = data.transactionDateTime;
                        updated["counterAccountBankId"] = data.counterAccountBankId;
                        updated["counterAccountBankName"] = data.counterAccountBankName;
                        updated["counterAccountName"] = data.counterAccountName;
                        updated["counterAccountNumber"] = data.counterAccountNumber;
                        updated["virtualAccountName"] = data.virtualAccountName;
                        updated["virtualAccountNumber"] = data.virtualAccountNumber;
                        tx.Payload = updated.ToJsonString();
                    }
                    catch {}
                    await _context.SaveChangesAsync();

                    int examId = 0;
                    try
                    {
                        var d = System.Text.Json.JsonDocument.Parse(tx.Payload ?? "{}");
                        if (d.RootElement.TryGetProperty("examId", out var e)) examId = e.GetInt32();
                    }
                    catch {}
                    if (examId > 0)
                    {
                        var userId = tx.UserId;
                        var existingEnrollment = await _context.Set<ExamEnrollment>().FirstOrDefaultAsync(en => en.ExamId == examId && en.UserId == userId && !en.HasDelete);
                        if (existingEnrollment == null)
                        {
                            var enrollment = new ExamEnrollment { ExamId = examId, UserId = userId, Status = "Active", CreatedAt = NowVn(), HasDelete = false };
                            _context.Add(enrollment);
                            await _context.SaveChangesAsync();
                        }
                        else if (existingEnrollment.Status != "Active")
                        {
                            existingEnrollment.Status = "Active";
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while handling PayOS webhook");
                return StatusCode(500);
            }
        }

        [HttpGet("payments/my")]
        [Authorize]
        public async Task<IActionResult> GetMyPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = HttpContext.GetSyncedUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(ApiResponse.ErrorResponse("Không thể xác thực người dùng", 401));
                }

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var query = _context.Set<PaymentTransaction>()
                    .Where(t => t.UserId == userId.Value);

                var total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new
                    {
                        transactionId = t.TransactionId,
                        orderId = t.OrderId,
                        status = t.Status,
                        amount = t.Amount,
                        currency = t.Currency,
                        gateway = t.Gateway,
                        paidAt = t.PaidAt,
                        createdAt = t.CreatedAt,
                        payload = t.Payload
                    })
                    .ToListAsync();

                return Ok(ApiResponse.SuccessResponse(new { items, total, page, pageSize }, "Lấy giao dịch của tôi thành công"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting my payments");
                return StatusCode(500, ApiResponse.ErrorResponse("Lỗi hệ thống khi lấy giao dịch", 500));
            }
        }
    }
}