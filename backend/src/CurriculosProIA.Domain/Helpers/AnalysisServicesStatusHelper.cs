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

        var itens = status.Select(kv =>
        {
            var isUnlimited = AnalysisBundledServiceKeys.Unlimited.Contains(kv.Key);
            return new AnalysisServiceItemDto
            {
                Key = kv.Key,
                Label = AnalysisBundledServiceKeys.Labels.TryGetValue(kv.Key, out var label) ? label : kv.Key,
                Ilimitado = isUnlimited,
                Usado = isUnlimited || kv.Value,
                Pendente = !isUnlimited &&
                           AnalysisBundledServiceKeys.Optional.Contains(kv.Key) &&
                           !kv.Value
            };
        }).ToList();

        var pendentes = AnalysisBundledServiceKeys.Optional.Count(k => !status.GetValueOrDefault(k));

        var inglesPago = status.GetValueOrDefault(AnalysisBundledServiceKeys.CurriculoInglesPago);
        var inglesLegacy = status.GetValueOrDefault(AnalysisBundledServiceKeys.CurriculoIngles);
        var inglesPdf = status.GetValueOrDefault(AnalysisBundledServiceKeys.CurriculoInglesPdf) || inglesLegacy;
        var inglesWord = status.GetValueOrDefault(AnalysisBundledServiceKeys.CurriculoInglesWord) || inglesLegacy;
        var inglesGerado = inglesPdf && inglesWord;

        itens.Add(new AnalysisServiceItemDto
        {
            Key = AnalysisBundledServiceKeys.CurriculoInglesPago,
            Label = AnalysisBundledServiceKeys.Labels[AnalysisBundledServiceKeys.CurriculoInglesPago],
            Usado = inglesPago,
            Pendente = false,
            Ilimitado = false
        });
        itens.Add(new AnalysisServiceItemDto
        {
            Key = AnalysisBundledServiceKeys.CurriculoInglesPdf,
            Label = AnalysisBundledServiceKeys.Labels[AnalysisBundledServiceKeys.CurriculoInglesPdf],
            Usado = inglesPdf,
            Pendente = inglesPago && !inglesPdf,
            Ilimitado = false
        });
        itens.Add(new AnalysisServiceItemDto
        {
            Key = AnalysisBundledServiceKeys.CurriculoInglesWord,
            Label = AnalysisBundledServiceKeys.Labels[AnalysisBundledServiceKeys.CurriculoInglesWord],
            Usado = inglesWord,
            Pendente = inglesPago && !inglesWord,
            Ilimitado = false
        });

        return new AnalysisServicesStatusDto
        {
            Itens = itens,
            ServicosPendentes = pendentes,
            PacoteConcluido = pendentes == 0,
            CurriculoInglesPago = inglesPago,
            CurriculoInglesGerado = inglesGerado,
            CurriculoInglesPdf = inglesPdf,
            CurriculoInglesWord = inglesWord
        };
    }

    public static JsonElement SerializeStatus(Dictionary<string, bool> status)
    {
        status[AnalysisBundledServiceKeys.Analise] = true;
        return JsonSerializer.SerializeToElement(status);
    }
}
