using CurriculosProIA.App.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

/// <summary>
/// Rotas compatíveis com o Cakto SDK no browser (3DS). Evita CORS em api.cakto.com.br.
/// </summary>
[ApiController]
[Route("api/financial")]
public class CaktoFinancialController : ControllerBase
{
    private readonly IAnalyzeAppService _analyze;

    public CaktoFinancialController(IAnalyzeAppService analyze) => _analyze = analyze;

    [HttpGet("3ds/token")]
    [HttpGet("3ds/token/")]
    public Task<IActionResult> Get3dsToken([FromQuery] string? provider, CancellationToken ct) =>
        _analyze.GetCakto3dsToken(provider, ct);
}
