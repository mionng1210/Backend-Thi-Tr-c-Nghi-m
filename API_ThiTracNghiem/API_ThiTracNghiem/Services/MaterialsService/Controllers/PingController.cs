using Microsoft.AspNetCore.Mvc;

namespace MaterialsService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { service = "materials", status = "ok" });
}


