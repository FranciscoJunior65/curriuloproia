using CurriculosProIA.App.Interfaces;
using CurriculosProIA.Domain.Signatures.Purchase;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/purchase")]
public class PurchaseController : ControllerBase
{
    private readonly IPurchaseAppService _purchase;

    public PurchaseController(IPurchaseAppService purchase) => _purchase = purchase;

    [HttpGet("test")]
    public IActionResult Test() => _purchase.Test();

    [HttpPost("mock")]
    public Task<IActionResult> CreateMockPurchase([FromBody] MockPurchaseSignature body, CancellationToken ct) =>
        _purchase.CreateMockPurchase(body, ct);

    [HttpGet("history")]
    public Task<IActionResult> GetHistory([FromQuery] int limit = 50, CancellationToken ct = default) =>
        _purchase.GetHistory(limit, ct);

    [HttpGet("credits/history")]
    public Task<IActionResult> GetCreditHistory([FromQuery] int limit = 50, CancellationToken ct = default) =>
        _purchase.GetCreditHistory(limit, ct);

    [HttpPost("credits/use")]
    public Task<IActionResult> RecordCreditUse([FromBody] RecordCreditUseSignature body, CancellationToken ct) =>
        _purchase.RecordCreditUse(body, ct);

    [HttpGet("export")]
    public Task<IActionResult> ExportHistory([FromQuery] string format = "json", [FromQuery] int limit = 500, CancellationToken ct = default) =>
        _purchase.ExportHistory(format, limit, ct);
}
