using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public HealthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var hasApiKey = !string.IsNullOrEmpty(_configuration["OPENAI_API_KEY"]);
        return Ok(new
        {
            status = "ok",
            message = "API funcionando",
            openaiConfigured = hasApiKey,
            model = _configuration["OPENAI_MODEL"] ?? "gpt-4"
        });
    }
}
