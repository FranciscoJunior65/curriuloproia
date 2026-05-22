using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Interfaces;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class VoiceInterviewService : IVoiceInterviewService
{
    private readonly IAiService _ai;
    private readonly IJobSitesService _jobSites;
    private readonly IInterviewRepository _interviews;
    private readonly ILogger<VoiceInterviewService> _logger;

    private static readonly InterviewPersonaDto[] Personas =
    [
        new() { Name = "Ana Ribeiro", Role = "Recrutadora Sênior", Initials = "AR", AvatarColor = "#6366f1" },
        new() { Name = "Carlos Mendes", Role = "Tech Lead", Initials = "CM", AvatarColor = "#0ea5e9" },
        new() { Name = "Marina Costa", Role = "Gerente de RH", Initials = "MC", AvatarColor = "#8b5cf6" }
    ];

    public VoiceInterviewService(
        IAiService ai,
        IJobSitesService jobSites,
        IInterviewRepository interviews,
        ILogger<VoiceInterviewService> logger)
    {
        _ai = ai;
        _jobSites = jobSites;
        _interviews = interviews;
        _logger = logger;
    }

    public async Task<VoiceInterviewStartResult> StartAsync(
        string resumeText,
        AnalysisInput analysis,
        string? siteId,
        string? userId,
        string? resumeId,
        CancellationToken cancellationToken = default)
    {
        var persona = await BuildPersonaAsync(siteId, cancellationToken);
        var siteName = persona.Company;
        var context = BuildResumeContext(resumeText, analysis, siteName);

        var prompt = $"""
            Você é {persona.Name}, {persona.Role}, conduzindo uma entrevista de emprego REAL por voz (como videoconferência).
            Fale em português brasileiro, tom profissional e acolhedor, frases curtas (máx. 3 frases por vez).

            {context}

            Esta é a ABERTURA da entrevista. Apresente-se brevemente e faça a primeira pergunta natural (não uma lista).
            Não mencione que é IA.

            Retorne APENAS JSON com o campo openingMessage (texto que você fala em voz alta).
            """;

        var raw = await _ai.GenerateTextAsync(prompt, 0.75, 800, cancellationToken);
        var opening = ParseJsonField(raw, "openingMessage")
            ?? $"Olá! Eu sou a {persona.Name}, {persona.Role}. É um prazer falar com você hoje. Para começarmos, pode se apresentar em poucas palavras e me contar o que te motivou a buscar esta oportunidade?";

        string? simulationId = null;
        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(resumeId) && !string.IsNullOrEmpty(siteId))
        {
            try
            {
                simulationId = await _interviews.CreateInterviewSimulationAsync(
                    userId,
                    resumeId,
                    siteId,
                    ["voice_conversational"],
                    "Entrevista por voz",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível criar simulação de entrevista por voz no banco");
            }
        }

        return new VoiceInterviewStartResult
        {
            SimulationId = simulationId,
            Persona = persona,
            OpeningMessage = opening.Trim()
        };
    }

    public async Task<VoiceInterviewTurnResult> ProcessTurnAsync(
        string resumeText,
        AnalysisInput analysis,
        string? siteId,
        string candidateMessage,
        IReadOnlyList<VoiceConversationMessageDto> history,
        int turnNumber,
        CancellationToken cancellationToken = default)
    {
        var persona = await BuildPersonaAsync(siteId, cancellationToken);
        var context = BuildResumeContext(resumeText, analysis, persona.Company);
        var transcript = FormatHistory(history);
        var minTurns = 4;
        var maxTurns = 14;
        var canEnd = turnNumber >= minTurns;

        var prompt = $"""
            Você é {persona.Name}, {persona.Role}, em entrevista por voz ao vivo.
            {context}

            HISTÓRICO DA CONVERSA:
            {transcript}

            ÚLTIMA FALA DO CANDIDATO:
            {candidateMessage}

            Turno atual: {turnNumber} (mínimo {minTurns}, máximo {maxTurns} turnos do candidato).

            Conduza como entrevista real:
            - Reaja ao que o candidato disse (não ignore)
            - Faça follow-ups naturais
            - Alterne comportamental e técnico conforme o currículo
            - Uma pergunta ou comentário por vez, linguagem falada
            - shouldEnd=true SOMENTE se: já cobriu apresentação, experiência, desafio técnico/comportamental e despedida OU turno >= {maxTurns}
            - shouldEnd=false se turno < {minTurns}

            Fases: opening, exploration, deep_dive, closing

            Retorne APENAS JSON com campos: interviewerMessage (string), shouldEnd (boolean), phase (string: opening|exploration|deep_dive|closing).
            """;

        var raw = await _ai.GenerateTextAsync(prompt, 0.7, 900, cancellationToken);
        var message = ParseJsonField(raw, "interviewerMessage")
            ?? "Entendi. Pode me contar um pouco mais sobre essa experiência?";
        var shouldEnd = ParseJsonBool(raw, "shouldEnd") && canEnd;
        var phase = ParseJsonField(raw, "phase") ?? "exploration";

        if (turnNumber >= maxTurns)
        {
            shouldEnd = true;
            if (!message.Contains("obrigad", StringComparison.OrdinalIgnoreCase))
            {
                message += " Muito obrigada pelo seu tempo hoje. Foi um prazer conversar com você!";
            }
        }

        return new VoiceInterviewTurnResult
        {
            InterviewerMessage = message.Trim(),
            ShouldEnd = shouldEnd,
            Phase = phase,
            TurnNumber = turnNumber
        };
    }

    public async Task<VoiceInterviewSummaryResult> FinishAsync(
        string? simulationId,
        string resumeText,
        AnalysisInput analysis,
        IReadOnlyList<VoiceConversationMessageDto> history,
        CancellationToken cancellationToken = default)
    {
        var transcript = FormatHistory(history);
        var prompt = $"""
            Analise esta entrevista por voz completa e gere feedback final.

            CURRÍCULO (resumo):
            {SafeTake(resumeText, 1500)}

            TRANSCRIÇÃO:
            {transcript}

            Retorne APENAS JSON com: score (0-100), overallFeedback, strengths (array), improvements (array), highlights (array).
            """;

        var raw = await _ai.GenerateTextAsync(prompt, 0.5, 1200, cancellationToken);
        var summary = ParseSummary(raw);

        if (!string.IsNullOrEmpty(simulationId) && history.Count > 0)
        {
            try
            {
                var answers = new List<InterviewAnswerItem>
                {
                    new()
                    {
                        Evaluation = new InterviewEvaluation
                        {
                            Score = summary.Score,
                            Feedback = summary.OverallFeedback,
                            Strengths = summary.Strengths,
                            Improvements = summary.Improvements
                        }
                    }
                };
                await _interviews.UpdateSimulationAnswersAsync(simulationId, answers, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao persistir entrevista por voz");
            }
        }

        return summary;
    }

    private async Task<InterviewPersonaDto> BuildPersonaAsync(string? siteId, CancellationToken cancellationToken)
    {
        var persona = Personas[Random.Shared.Next(Personas.Length)];
        var company = "empresa contratante";

        if (!string.IsNullOrEmpty(siteId))
        {
            var site = await _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken);
            if (site?.Nome != null)
            {
                company = $"oportunidades em {site.Nome}";
            }
        }

        return new InterviewPersonaDto
        {
            Name = persona.Name,
            Role = persona.Role,
            Company = company,
            Initials = persona.Initials,
            AvatarColor = persona.AvatarColor
        };
    }

    private static string BuildResumeContext(string resumeText, AnalysisInput analysis, string company)
    {
        var habilidades = analysis.Habilidades != null ? string.Join(", ", analysis.Habilidades.Take(8)) : "";
        return $"""
            Empresa/contexto: {company}
            Área: {analysis.AreaAtuacao ?? "geral"}
            Habilidades: {habilidades}
            Experiência (resumo): {SafeTake(analysis.Experiencia, 400)}
            Trecho do currículo: {SafeTake(resumeText, 1200)}
            """;
    }

    /// <summary>Recorta texto com Trim sem ultrapassar o tamanho real da string.</summary>
    private static string SafeTake(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || maxLength <= 0)
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string FormatHistory(IReadOnlyList<VoiceConversationMessageDto> history)
    {
        if (history.Count == 0)
        {
            return "(início da entrevista)";
        }

        var sb = new StringBuilder();
        foreach (var msg in history)
        {
            var label = msg.Role == "interviewer" ? "Entrevistador" : "Candidato";
            sb.AppendLine($"{label}: {msg.Content}");
        }

        return sb.ToString();
    }

    private static string? ParseJsonField(string raw, string field)
    {
        try
        {
            var cleaned = AiService.CleanMarkdownFence(raw.Trim());
            var match = Regex.Match(cleaned, @"\{.*\}", RegexOptions.Singleline);
            var json = match.Success ? match.Value : cleaned;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(field, out var prop))
            {
                return prop.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static bool ParseJsonBool(string raw, string field)
    {
        try
        {
            var cleaned = AiService.CleanMarkdownFence(raw.Trim());
            var match = Regex.Match(cleaned, @"\{.*\}", RegexOptions.Singleline);
            var json = match.Success ? match.Value : cleaned;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(field, out var prop))
            {
                return prop.ValueKind == JsonValueKind.True;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static VoiceInterviewSummaryResult ParseSummary(string raw)
    {
        try
        {
            var cleaned = AiService.CleanMarkdownFence(raw.Trim());
            var match = Regex.Match(cleaned, @"\{.*\}", RegexOptions.Singleline);
            var json = match.Success ? match.Value : cleaned;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new VoiceInterviewSummaryResult
            {
                Score = root.TryGetProperty("score", out var s) && s.TryGetInt32(out var sc) ? Math.Clamp(sc, 0, 100) : 75,
                OverallFeedback = root.TryGetProperty("overallFeedback", out var f) ? f.GetString() ?? "" : "",
                Strengths = ParseStringArray(root, "strengths"),
                Improvements = ParseStringArray(root, "improvements"),
                Highlights = ParseStringArray(root, "highlights")
            };
        }
        catch
        {
            return new VoiceInterviewSummaryResult
            {
                Score = 75,
                OverallFeedback = "Entrevista concluída. Continue praticando respostas objetivas com exemplos concretos.",
                Strengths = ["Participou da conversa"],
                Improvements = ["Detalhar mais resultados quantificáveis"],
                Highlights = []
            };
        }
    }

    private static List<string> ParseStringArray(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return arr.EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();
    }
}
