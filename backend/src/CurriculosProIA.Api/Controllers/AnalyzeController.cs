using CurriculosProIA.App.Interfaces;
using CurriculosProIA.Domain.Signatures.Analyze;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/analyze")]
[RequestSizeLimit(10 * 1024 * 1024)]
public class AnalyzeController : ControllerBase
{
    private readonly IAnalyzeAppService _analyze;

    public AnalyzeController(IAnalyzeAppService analyze) => _analyze = analyze;

    [HttpPost("upload")]
    public Task<IActionResult> Upload(IFormFile? file, [FromForm] string? userId, [FromForm] string? siteId, [FromForm] string? curriculoId, CancellationToken ct) =>
        _analyze.Upload(file, userId, siteId, curriculoId, ct);

    [HttpPost("generate-improved")]
    public Task<IActionResult> GenerateImproved([FromBody] GenerateImprovedSignature body, CancellationToken ct) =>
        _analyze.GenerateImproved(body, ct);

    [HttpPost("generate-cover-letter")]
    public Task<IActionResult> GenerateCoverLetter([FromBody] GenerateCoverLetterSignature body, CancellationToken ct) =>
        _analyze.GenerateCoverLetter(body, ct);

    [HttpPost("search-jobs")]
    public Task<IActionResult> SearchJobs([FromBody] SearchJobsSignature body, CancellationToken ct) =>
        _analyze.SearchJobs(body, ct);

    [HttpPost("interview/start")]
    public Task<IActionResult> StartInterview([FromBody] StartInterviewSignature body, CancellationToken ct) =>
        _analyze.StartInterview(body, ct);

    [HttpPost("interview/evaluate")]
    public Task<IActionResult> EvaluateInterview([FromBody] EvaluateInterviewSignature body, CancellationToken ct) =>
        _analyze.EvaluateInterview(body, ct);

    [HttpPost("interview/finish")]
    public Task<IActionResult> FinishInterview([FromBody] FinishInterviewSignature body, CancellationToken ct) =>
        _analyze.FinishInterview(body, ct);

    [HttpGet("interview/user/list")]
    public Task<IActionResult> ListUserInterviews(CancellationToken ct) => _analyze.ListUserInterviews(ct);

    [HttpGet("interview/{simulationId}")]
    public Task<IActionResult> GetInterview(string simulationId, CancellationToken ct) =>
        _analyze.GetInterview(simulationId, ct);

    [HttpGet("interview/{simulationId}/download")]
    public Task<IActionResult> DownloadInterview(string simulationId, CancellationToken ct) =>
        _analyze.DownloadInterview(simulationId, ct);

    [HttpGet("plans")]
    public IActionResult GetPlans() => _analyze.GetPlans();

    [HttpGet("payment/provider")]
    public Task<IActionResult> GetActivePaymentProvider(CancellationToken ct) =>
        _analyze.GetActivePaymentProvider(ct);

    [HttpGet("coupon/validate")]
    [HttpPost("coupon/validate")]
    public Task<IActionResult> ValidateCoupon([FromQuery] string? code, [FromQuery] string? cpf, [FromBody] CouponValidateSignature? body, CancellationToken ct) =>
        _analyze.ValidateCoupon(code, cpf, body, ct);

    [HttpPost("payment/create-session")]
    public Task<IActionResult> CreatePaymentSession([FromBody] CreatePaymentSessionSignature body, CancellationToken ct) =>
        _analyze.CreatePaymentSession(body, ct);

    [HttpPost("payment/admin-free-credits")]
    public Task<IActionResult> AdminFreeCredits([FromBody] AdminFreeCreditsSignature body, CancellationToken ct) =>
        _analyze.AdminFreeCredits(body, ct);

    [HttpGet("payment/verify")]
    public Task<IActionResult> VerifyPayment([FromQuery] string? sessionId, [FromQuery] string? payment_id, [FromQuery] string? paymentId, [FromQuery] string? provider, CancellationToken ct) =>
        _analyze.VerifyPayment(sessionId, payment_id, paymentId, provider, ct);

    [HttpGet("credits")]
    public Task<IActionResult> GetCredits([FromQuery] string? userId, CancellationToken ct) =>
        _analyze.GetCredits(userId, ct);

    [HttpGet("job-sites")]
    public Task<IActionResult> ListJobSites(CancellationToken ct) => _analyze.ListJobSites(ct);

    [HttpGet("analyses")]
    public Task<IActionResult> ListUserAnalyses([FromQuery] int limit = 50, [FromQuery] int offset = 0, CancellationToken ct = default) =>
        _analyze.ListUserAnalyses(limit, offset, ct);

    [HttpGet("analyses/{analysisId}")]
    public Task<IActionResult> GetAnalysis(string analysisId, CancellationToken ct) =>
        _analyze.GetAnalysis(analysisId, ct);
}
