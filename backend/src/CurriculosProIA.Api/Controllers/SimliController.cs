using CurriculosProIA.App.Interfaces;
using CurriculosProIA.Domain.Signatures.Simli;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/simli")]
public class SimliController : ControllerBase
{
    private readonly ISimliAppService _simli;

    public SimliController(ISimliAppService simli) => _simli = simli;

    [HttpGet("config")]
    [AllowAnonymous]
    public IActionResult GetConfig() => _simli.GetConfig();

    [HttpPost("session")]
    [Authorize]
    public Task<IActionResult> CreateSession([FromBody] CreateSimliSessionSignature body, CancellationToken ct) =>
        _simli.CreateSession(body, ct);

    [HttpPost("speech")]
    [Authorize]
    public Task<IActionResult> SynthesizeSpeech([FromBody] SimliSpeechSignature body, CancellationToken ct) =>
        _simli.SynthesizeSpeech(body, ct);
}
