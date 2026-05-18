using Microsoft.AspNetCore.Http;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class FileService : IFileService
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/msword",
        "text/plain"
    };

    public async Task<string> ExtractTextFromFileAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new InvalidOperationException("Arquivo vazio");
        }

        var mimeType = file.ContentType;
        if (!AllowedMimeTypes.Contains(mimeType))
        {
            throw new InvalidOperationException("Formato de arquivo não suportado");
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var buffer = memory.ToArray();

        try
        {
            if (mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractFromPdf(buffer);
            }

            if (mimeType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase) ||
                mimeType.Equals("application/msword", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractFromDocx(buffer);
            }

            return System.Text.Encoding.UTF8.GetString(buffer);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erro ao extrair texto: {ex.Message}", ex);
        }
    }

    private static string ExtractFromPdf(byte[] buffer)
    {
        using var document = PdfDocument.Open(buffer);
        return string.Join("\n", document.GetPages().Select(p => p.Text));
    }

    private static string ExtractFromDocx(byte[] buffer)
    {
        using var stream = new MemoryStream(buffer);
        using var wordDoc = WordprocessingDocument.Open(stream, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        return body?.InnerText ?? string.Empty;
    }
}
