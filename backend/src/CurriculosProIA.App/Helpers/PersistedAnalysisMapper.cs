using System.Text.Json;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.App.Helpers;

/// <summary>
/// Reconstrói análise e texto do currículo a partir do banco (histórico).
/// </summary>
public static class PersistedAnalysisMapper
{
    public static AnalysisInput ToAnalysisInput(AnaliseCurriculoListItemDto dto)
    {
        var input = new AnalysisInput
        {
            PontosFortes = dto.PontosFortes ?? new List<string>(),
            PontosMelhorar = dto.PontosMelhorar ?? new List<string>(),
            Recomendacoes = dto.Recomendacoes ?? new List<string>(),
            Score = dto.ScoreGeral,
            Habilidades = dto.PalavrasChaveSugeridas?.ToList() ?? new List<string>(),
            Experiencia = string.Empty,
            Formacao = string.Empty
        };

        if (dto.ResultadoCompleto is { ValueKind: JsonValueKind.Object } rc)
        {
            if (rc.TryGetProperty("habilidades", out var hab) && hab.ValueKind == JsonValueKind.Array)
            {
                var fromRc = JsonSerializer.Deserialize<List<string>>(hab.GetRawText()) ?? new List<string>();
                if (fromRc.Count > 0)
                {
                    input.Habilidades = fromRc;
                }
            }

            if (rc.TryGetProperty("experiencia", out var exp) && exp.ValueKind == JsonValueKind.String)
            {
                input.Experiencia = exp.GetString() ?? string.Empty;
            }

            if (rc.TryGetProperty("formacao", out var form) && form.ValueKind == JsonValueKind.String)
            {
                input.Formacao = form.GetString() ?? string.Empty;
            }

            if (rc.TryGetProperty("areaAtuacao", out var area) && area.ValueKind == JsonValueKind.String)
            {
                input.AreaAtuacao = area.GetString();
            }
        }

        if (input.Habilidades.Count == 0 && dto.PalavrasChaveSugeridas?.Count > 0)
        {
            input.Habilidades = dto.PalavrasChaveSugeridas.ToList();
        }

        if (string.IsNullOrWhiteSpace(input.Experiencia) && input.PontosFortes.Count > 0)
        {
            input.Experiencia = string.Join(". ", input.PontosFortes.Take(5));
        }

        return input;
    }

    public static string? GetResumeText(AnaliseCurriculoListItemDto dto) =>
        dto.CurriculosImportados?.ConteudoExtraido?.Trim();

    public static string? GetResumeId(AnaliseCurriculoListItemDto dto) =>
        dto.IdCurriculo;

    public static string? GetSiteId(AnaliseCurriculoListItemDto dto) =>
        dto.IdSiteVagas;
}
