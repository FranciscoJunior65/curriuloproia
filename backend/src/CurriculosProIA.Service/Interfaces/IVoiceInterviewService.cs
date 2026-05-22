using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IVoiceInterviewService
{
    Task<VoiceInterviewStartResult> StartAsync(
        string resumeText,
        AnalysisInput analysis,
        string? siteId,
        string? userId,
        string? resumeId,
        CancellationToken cancellationToken = default);

    Task<VoiceInterviewTurnResult> ProcessTurnAsync(
        string resumeText,
        AnalysisInput analysis,
        string? siteId,
        string candidateMessage,
        IReadOnlyList<VoiceConversationMessageDto> history,
        int turnNumber,
        CancellationToken cancellationToken = default);

    Task<VoiceInterviewSummaryResult> FinishAsync(
        string? simulationId,
        string resumeText,
        AnalysisInput analysis,
        IReadOnlyList<VoiceConversationMessageDto> history,
        CancellationToken cancellationToken = default);
}
