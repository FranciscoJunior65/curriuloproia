using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.App.Helpers;

public sealed class ResolvedAnalysisContext
{
    public required AnalysisInput Analysis { get; init; }
    public string? ResumeText { get; init; }
    public string? ResumeId { get; init; }
    public string? SiteId { get; init; }
}
