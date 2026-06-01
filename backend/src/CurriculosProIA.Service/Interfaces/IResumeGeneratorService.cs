using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;

namespace CurriculosProIA.Service.Interfaces;

public interface IResumeGeneratorService
{
    Task<string> GenerateImprovedResumeAsync(
        string originalText,
        AnalysisInput analysis,
        string? siteId = null,
        CancellationToken cancellationToken = default);

    byte[] GenerateResumePdf(string resumeText);

    byte[] GenerateResumeDocx(string resumeText);

    Task<string> GenerateEnglishResumeAsync(
        string originalText,
        AnalysisInput? analysis,
        string? siteId = null,
        CancellationToken cancellationToken = default);

    byte[] GenerateResumeExcel(string resumeText);
}
