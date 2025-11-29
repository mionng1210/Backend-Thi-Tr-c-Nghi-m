using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Net.payOS.Types;
using ExamsService.Services;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using System.Threading.Tasks;

namespace ExamsService.Controllers
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
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateRequest req)
        {
            if (!_client.IsConfigured)
            {
                return StatusCode(503, new { success = false, message = "Thiếu cấu hình PayOS (CLIENT_ID/API_KEY/CHECKSUM_KEY)" });
            }
            var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var result = await _client.CreatePaymentLink(orderCode, req.Amount, req.Description, req.ReturnUrl, req.CancelUrl);
            return Ok(new { success = true, data = result });
        }

        [HttpGet("status/{orderCode:long}")]
        [Authorize]
        public async Task<IActionResult> Status(long orderCode)
        {
            if (!_client.IsConfigured)
            {
                return StatusCode(503, new { success = false, message = "Thiếu cấu hình PayOS (CLIENT_ID/API_KEY/CHECKSUM_KEY)" });
            }
            var info = await _client.GetPaymentLinkInformation(orderCode);
            return Ok(new { success = true, data = info });
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
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