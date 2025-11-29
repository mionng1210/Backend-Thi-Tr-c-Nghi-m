using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Net.payOS.Types;
using PaymentService.Services;
using System.IO;

namespace PaymentService.Controllers
{
    [ApiController]
    [Route("api/payments/payos")]
    public class PayOSController : ControllerBase
    {
        private readonly PayOSClient _client;

        public PayOSController(PayOSClient client)
        {
            _client = client;
        }

        public record CreateRequest(int Amount, string Description, string ReturnUrl, string CancelUrl);

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateRequest req)
        {
            try
            {
                var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var result = await _client.CreatePaymentLink(orderCode, req.Amount, req.Description, req.ReturnUrl, req.CancelUrl);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("status/{orderCode:long}")]
        public async Task<IActionResult> Status(long orderCode)
        {
            var info = await _client.GetPaymentLinkInformation(orderCode);
            return Ok(new { success = true, data = info });
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();
            var body = JsonSerializer.Deserialize<WebhookType>(json);
            var data = _client.VerifyWebhook(body!);
            return Ok(new { success = true, data });
        }
    }
}