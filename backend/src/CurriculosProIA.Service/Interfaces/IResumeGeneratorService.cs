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
        string? candidateName = null,
        CancellationToken cancellationToken = default);

    byte[] GenerateResumePdf(string resumeText, string? candidateName = null);

    byte[] GenerateResumeDocx(string resumeText, string? candidateName = null);

    Task<string> GenerateEnglishResumeAsync(
        string originalText,
        AnalysisInput? analysis,
        string? siteId = null,
        string? candidateName = null,
        CancellationToken cancellationToken = default);

    byte[] GenerateResumeExcel(string resumeText);
}
