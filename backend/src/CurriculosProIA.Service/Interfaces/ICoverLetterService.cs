using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface ICoverLetterService
{
    Task<string> GenerateCoverLetterAsync(
        string resumeText,
        AnalysisInput analysis,
        string? siteId = null,
        CancellationToken cancellationToken = default);

    byte[] GenerateCoverLetterPdf(string coverLetterText);
}
