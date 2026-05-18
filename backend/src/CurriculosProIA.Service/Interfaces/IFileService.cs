using Microsoft.AspNetCore.Http;

namespace CurriculosProIA.Service.Interfaces;

public interface IFileService
{
    Task<string> ExtractTextFromFileAsync(IFormFile file, CancellationToken cancellationToken = default);
}
