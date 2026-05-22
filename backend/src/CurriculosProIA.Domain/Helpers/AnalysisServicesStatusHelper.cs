using System.Text.Json;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Domain.Helpers;

public static class AnalysisServicesStatusHelper
{
    public static Dictionary<string, bool> ParseStatus(JsonElement? servicosUtilizados)
    {
        var status = AnalysisBundledServiceKeys.CreateDefaultStatus();
        if (servicosUtilizados is not { ValueKind: JsonValueKind.Object })
        {
            return status;
        }

        foreach (var key in status.Keys.ToList())
        {
            if (servicosUtilizados.Value.TryGetProperty(key, out var prop) &&
                prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                status[key] = prop.GetBoolean();
            }
        }

        status[AnalysisBundledServiceKeys.Analise] = true;
        return status;
    }

    public static AnalysisServicesStatusDto Build(
        Dictionary<string, bool> status,
        bool hasInterviewSimulation = false)
    {
        if (hasInterviewSimulation)
        {
            status[AnalysisBundledServiceKeys.Entrevista] = true;
        }

        var itens = status.Select(kv => new AnalysisServiceItemDto
        {
            Key = kv.Key,
            Label = AnalysisBundledServiceKeys.Labels.TryGetValue(kv.Key, out var label) ? label : kv.Key,
            Usado = kv.Value,
            Pendente = AnalysisBundledServiceKeys.Optional.Contains(kv.Key) && !kv.Value
        }).ToList();

        var pendentes = AnalysisBundledServiceKeys.Optional.Count(k => !status.GetValueOrDefault(k));

        return new AnalysisServicesStatusDto
        {
            Itens = itens,
            ServicosPendentes = pendentes,
            PacoteConcluido = pendentes == 0
        };
    }

    public static JsonElement SerializeStatus(Dictionary<string, bool> status)
    {
        status[AnalysisBundledServiceKeys.Analise] = true;
        return JsonSerializer.SerializeToElement(status);
    }
}
