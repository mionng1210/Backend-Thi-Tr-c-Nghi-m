using Microsoft.AspNetCore.Mvc;

namespace API_ThiTracNghiem.Services.AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { service = "auth", status = "ok" });
}


