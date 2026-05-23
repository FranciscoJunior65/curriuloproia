using CurriculosProIA.Api.Infrastructure;
using CurriculosProIA.Service.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;

    public HealthController(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var useMock = AiRuntimeOptions.UseMockAi(_configuration, _hostEnvironment);
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
