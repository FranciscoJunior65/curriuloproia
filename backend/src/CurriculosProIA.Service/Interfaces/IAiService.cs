using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IAiService
{
    Task<ResumeAnalysisResult> AnalyzeResumeAsync(
        string resumeText,
        string? userId = null,
        string? curriculoId = null,
        string? siteId = null,
        CancellationToken cancellationToken = default);

    Task<string> GenerateTextAsync(
        string prompt,
        double temperature = 0.7,
        int maxOutputTokens = 3000,
        CancellationToken cancellationToken = default);
}
