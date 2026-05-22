using CurriculosProIA.Api.Infrastructure;
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
        var useMock = _configuration["USE_MOCK_AI"] is "true" or "1";
        var geminiConfigured = GeminiConfigHelper.IsValidApiKey(_configuration["GEMINI_API_KEY"]);
        var geminiKeyIssue = GeminiConfigHelper.GetInvalidKeyReason(_configuration["GEMINI_API_KEY"]);
        return Ok(new
        {
            status = "ok",
            message = "API funcionando",
            aiProvider = _configuration["AI_PROVIDER"] ?? "gemini",
            geminiConfigured,
            geminiKeyIssue,
            useMockAi = useMock,
            model = _configuration["GEMINI_MODEL"] ?? "gemini-3-flash-preview"
        });
    }
}
