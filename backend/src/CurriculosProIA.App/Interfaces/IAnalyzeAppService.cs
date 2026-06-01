using CurriculosProIA.Domain.Signatures.Analyze;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Interfaces;

public interface IAnalyzeAppService
{
    Task<IActionResult> Upload(IFormFile? file, string? userId, string? siteId, string? curriculoId, CancellationToken cancellationToken = default);
    Task<IActionResult> GenerateImproved(GenerateImprovedSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> GenerateEnglishResume(GenerateEnglishExcelSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> GenerateCoverLetter(GenerateCoverLetterSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> SearchJobs(SearchJobsSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> StartInterview(StartInterviewSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> EvaluateInterview(EvaluateInterviewSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> FinishInterview(FinishInterviewSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> ListUserInterviews(CancellationToken cancellationToken = default);
    Task<IActionResult> GetInterview(string simulationId, CancellationToken cancellationToken = default);
    Task<IActionResult> DownloadInterview(string simulationId, CancellationToken cancellationToken = default);
    Task<IActionResult> StartVoiceInterview(VoiceInterviewStartSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> VoiceInterviewTurn(VoiceInterviewTurnSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> FinishVoiceInterview(VoiceInterviewFinishSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> GetPlans(CancellationToken cancellationToken = default);
    Task<IActionResult> GetPricingConfig(CancellationToken cancellationToken = default);
    Task<IActionResult> GetActivePaymentProvider(CancellationToken cancellationToken = default);
    Task<IActionResult> ValidateCoupon(string? code, string? cpf, CouponValidateSignature? body, CancellationToken cancellationToken = default);
    Task<IActionResult> CreatePaymentSession(CreatePaymentSessionSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> AdminFreeCredits(AdminFreeCreditsSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> VerifyPayment(string? sessionId, string? payment_id, string? paymentId, string? provider, CancellationToken cancellationToken = default);
    Task<IActionResult> GetCredits(string? userId, CancellationToken cancellationToken = default);
    Task<IActionResult> ListJobSites(CancellationToken cancellationToken = default);
    Task<IActionResult> ListUserAnalyses(int limit, int offset, CancellationToken cancellationToken = default);
    Task<IActionResult> GetAnalysis(string analysisId, CancellationToken cancellationToken = default);
    Task<IActionResult> GetPendingServices(CancellationToken cancellationToken = default);
}
