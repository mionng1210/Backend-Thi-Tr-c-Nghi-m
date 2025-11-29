using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using API_ThiTracNghiem.Data;

namespace API_ThiTracNghiem.Controllers
{
    [ApiController]
    [Route("api/Admin/payments")]
    [Authorize]
    public class AdminPaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory? _httpClientFactory;

        public AdminPaymentsController(ApplicationDbContext db, IConfiguration config, IHttpClientFactory? httpClientFactory = null)
        {
            _db = db;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetPayments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? gateway = null,
            [FromQuery] string? search = null
        )
        {
            if (!await IsAdminAsync())
            {
                return Forbid("Chỉ admin mới có thể truy cập endpoint này");
            }

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

            var qp = new List<string>();
            qp.Add($"page={page}");
            qp.Add($"pageSize={pageSize}");
            if (!string.IsNullOrWhiteSpace(status)) qp.Add($"status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrWhiteSpace(gateway)) qp.Add($"gateway={Uri.EscapeDataString(gateway)}");
            if (!string.IsNullOrWhiteSpace(search)) qp.Add($"search={Uri.EscapeDataString(search)}");
            var path = $"/api/Exams/payments?{string.Join("&", qp)}";

            var resp = await client.GetAsync(path);
            var content = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return StatusCode((int)resp.StatusCode, content);
            }
            return Content(content, "application/json");
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPaymentById(int id)
        {
            if (!await IsAdminAsync())
            {
                return Forbid("Chỉ admin mới có thể truy cập endpoint này");
            }

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

            var path = $"/api/Exams/payments/{id}";
            var resp = await client.GetAsync(path);
            var content = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return StatusCode((int)resp.StatusCode, content);
            }
            return Content(content, "application/json");
        }

        private async Task<bool> IsAdminAsync()
        {
            var sub = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(sub) || !int.TryParse(sub, out var userId)) return false;
            var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
            var roleName = user?.Role?.RoleName ?? string.Empty;
            return string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}