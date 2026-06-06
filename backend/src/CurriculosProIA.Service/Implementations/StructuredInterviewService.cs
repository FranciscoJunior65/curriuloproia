using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Interfaces;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class StructuredInterviewService : IStructuredInterviewService
{
    private const int WrittenQuestionCount = 5;

    private static readonly InterviewPersonaDto[] Personas =
    [
        new() { Name = "Ana Ribeiro", Role = "Recrutadora Sênior", Initials = "AR", AvatarColor = "#6366f1" },
        new() { Name = "Carlos Mendes", Role = "Tech Lead", Initials = "CM", AvatarColor = "#0ea5e9" },
        new() { Name = "Marina Costa", Role = "Gerente de RH", Initials = "MC", AvatarColor = "#8b5cf6" }
    ];

    private readonly IAiService _ai;
    private readonly IJobSitesService _jobSites;
    private readonly IInterviewRepository _interviews;
    private readonly IInterviewConfigService _config;
    private readonly IAnalysisRepository _analysis;
    private readonly ILogger<StructuredInterviewService> _logger;

    public StructuredInterviewService(
        IAiService ai,
        IJobSitesService jobSites,
        IInterviewRepository interviews,
        IInterviewConfigService config,
        IAnalysisRepository analysis,
        ILogger<StructuredInterviewService> logger)
    {
        _ai = ai;
        _jobSites = jobSites;
        _interviews = interviews;
        _config = config;
        _analysis = analysis;
        _logger = logger;
    }

    public async Task<StructuredInterviewStatusResult> GetStatusAsync(
        string analysisId,
        string userId,
        string? resumeId,
        CancellationToken cancellationToken = default)
    {
        var status = await _analysis.GetServicesStatusAsync(analysisId, cancellationToken);
        var used = status.Itens.FirstOrDefault(i => i.Key == AnalysisBundledServiceKeys.Entrevista)?.Usado ?? false;

        if (!used || string.IsNullOrEmpty(resumeId))
        {
            return new StructuredInterviewStatusResult { AlreadyCompleted = false };
        }

        var simulation = await _interviews.GetLatestInterviewForResumeAsync(userId, resumeId, cancellationToken);
        if (simulation == null)
        {
            return new StructuredInterviewStatusResult { AlreadyCompleted = true };
        }

        var savedFeedback = await TryLoadSavedFeedbackAsync(simulation.Id, cancellationToken);
        return new StructuredInterviewStatusResult
        {
            AlreadyCompleted = true,
            SimulationId = simulation.Id,
            CanDownload = true,
            SavedFeedback = savedFeedback
        };
    }

    public async Task<StructuredInterviewStartResult> StartAsync(
        string resumeText,
        AnalysisInput analysis,
        string? siteId,
        string? userId,
        string? resumeId,
        CancellationToken cancellationToken = default)
    {
        var interviewConfig = await _config.GetConfigAsync(cancellationToken);
        var persona = await BuildPersonaAsync(siteId, cancellationToken);
        var context = BuildResumeContext(resumeText, analysis, persona.Company);
        var candidateName = await ExtractCandidateNameAsync(resumeText, cancellationToken);
        var maxWords = WordsForSeconds(interviewConfig.MaxSegmentSeconds);

        var prompt = ApplyTemplate(interviewConfig.QuestionsPrompt, new Dictionary<string, string>
        {
            ["resumeContext"] = context,
            ["maxWords"] = maxWords.ToString()
        });

        var raw = await _ai.GenerateTextAsync(prompt, 0.7, 1200, cancellationToken);
        var writtenQuestions = ParseStringArray(raw, "questions");
        if (writtenQuestions.Count < WrittenQuestionCount)
        {
            writtenQuestions = DefaultWrittenQuestions(analysis);
        }

        writtenQuestions = writtenQuestions.Take(WrittenQuestionCount).ToList();

        string? simulationId = null;
        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(resumeId) && !string.IsNullOrEmpty(siteId))
        {
            try
            {
                simulationId = await _interviews.CreateInterviewSimulationAsync(
                    userId,
                    resumeId,
                    siteId,
                    writtenQuestions,
                    "Entrevista estruturada",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível criar simulação de entrevista estruturada");
            }
        }

        return new StructuredInterviewStartResult
        {
            SimulationId = simulationId,
            Persona = persona,
            CandidateName = candidateName,
            WrittenQuestions = writtenQuestions,
            Phase1Minutes = interviewConfig.Phase1Minutes
        };
    }

    public async Task<StructuredInterviewVoicePhaseResult> BeginVoicePhaseAsync(
        string resumeText,
        AnalysisInput analysis,
        string? siteId,
        string candidateName,
        CancellationToken cancellationToken = default)
    {
        var interviewConfig = await _config.GetConfigAsync(cancellationToken);
        var persona = await BuildPersonaAsync(siteId, cancellationToken);
        var context = BuildResumeContext(resumeText, analysis, persona.Company);
        var introSeconds = interviewConfig.IntroMaxSeconds is > 0 and <= 60
            ? interviewConfig.IntroMaxSeconds
            : 22;
        var maxWords = WordsForSeconds(introSeconds);

        var prompt = ApplyTemplate(interviewConfig.IntroductionPrompt, new Dictionary<string, string>
        {
            ["personaName"] = persona.Name,
            ["personaRole"] = persona.Role,
            ["company"] = persona.Company,
            ["candidateName"] = candidateName,
            ["resumeContext"] = context,
            ["maxWords"] = maxWords.ToString(),
            ["introMaxSeconds"] = introSeconds.ToString(),
            ["maxSegmentSeconds"] = introSeconds.ToString()
        });

        var raw = await _ai.GenerateTextAsync(prompt, 0.65, 400, cancellationToken);
        var introScript = ParseJsonField(raw, "script")
            ?? $"Olá, {candidateName}! Sou {persona.Name}, {persona.Role}. Em instantes você terá tempo para se apresentar.";

        return new StructuredInterviewVoicePhaseResult
        {
            IntroScript = introScript.Trim()
        };
    }

    public Task SavePhaseAsync(
        string simulationId,
        int phaseIndex,
        string interviewerScript,
        string candidateAnswer,
        CancellationToken cancellationToken = default) =>
        _interviews.SaveStructuredPhaseAsync(
            simulationId,
            phaseIndex,
            interviewerScript,
            candidateAnswer ?? "",
            cancellationToken);

    public Task SaveWrittenAnswersAsync(
        string simulationId,
        IReadOnlyList<string> questions,
        IReadOnlyList<string> answers,
        CancellationToken cancellationToken = default) =>
        _interviews.SaveStructuredWrittenAnswersAsync(simulationId, questions, answers, cancellationToken);

    public async Task<StructuredInterviewFinishResult> FinishAsync(
        string? simulationId,
        string resumeText,
        AnalysisInput analysis,
        string? siteId,
        string candidateName,
        string introScript,
        IReadOnlyList<string> writtenQuestions,
        IReadOnlyList<string> writtenAnswers,
        string phase1Answer,
        CancellationToken cancellationToken = default)
    {
        var interviewConfig = await _config.GetConfigAsync(cancellationToken);
        var persona = await BuildPersonaAsync(siteId, cancellationToken);

        var questions = PadList(writtenQuestions, WrittenQuestionCount, "Pergunta");
        var answers = PadList(writtenAnswers, WrittenQuestionCount, "");

        var writtenBlock = BuildWrittenAnswersBlock(questions, answers);
        var responseSummary = BuildResponseSummary(questions, answers, phase1Answer);

        var maxFeedbackSeconds = Math.Max(60, interviewConfig.MaxVideoSpeechSeconds);
        var maxFeedbackWords = WordsForSeconds(maxFeedbackSeconds);

        var prompt = ApplyTemplate(interviewConfig.FeedbackPrompt, new Dictionary<string, string>
        {
            ["personaName"] = persona.Name,
            ["personaRole"] = persona.Role,
            ["company"] = persona.Company,
            ["candidateName"] = candidateName,
            ["writtenAnswersBlock"] = writtenBlock,
            ["phase1Answer"] = FormatCandidateLine(candidateName, phase1Answer),
            ["responseSummary"] = responseSummary,
            ["maxWords"] = maxFeedbackWords.ToString(),
            ["maxFeedbackSeconds"] = maxFeedbackSeconds.ToString()
        });

        var raw = await _ai.GenerateTextAsync(prompt, 0.55, 1800, cancellationToken);
        var result = ParseFinishResult(raw);
        ApplyNoResponseSafeguard(
            result,
            candidateName,
            CountWords(phase1Answer) + answers.Sum(CountWords));

        if (!string.IsNullOrEmpty(simulationId))
        {
            try
            {
                await _interviews.SaveStructuredWrittenAnswersAsync(simulationId, questions, answers, cancellationToken);
                await _interviews.SaveStructuredPhaseAsync(
                    simulationId,
                    WrittenQuestionCount,
                    introScript,
                    phase1Answer ?? "",
                    cancellationToken);

                await _interviews.SaveStructuredFeedbackAsync(
                    simulationId,
                    result.FeedbackScript,
                    new InterviewEvaluation
                    {
                        Score = result.Score,
                        Feedback = result.OverallFeedback,
                        Strengths = result.Strengths,
                        Improvements = result.Improvements
                    },
                    cancellationToken);

                var answerItems = answers
                    .Select(_ => new InterviewAnswerItem { Evaluation = new InterviewEvaluation { Score = result.Score } })
                    .ToList();
                answerItems.Add(new InterviewAnswerItem { Evaluation = new InterviewEvaluation { Score = result.Score } });

                await _interviews.UpdateSimulationAnswersAsync(simulationId, answerItems, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao persistir entrevista estruturada");
            }
        }

        result.SimulationId = simulationId;
        return result;
    }

    private async Task<StructuredInterviewSavedFeedback?> TryLoadSavedFeedbackAsync(
        string simulationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _interviews.GetInterviewByIdAsync(simulationId, cancellationToken);
            var feedbackMsg = detail?.Messages?
                .Where(m => string.Equals(m.Tipo, "feedback", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Ordem)
                .FirstOrDefault();

            if (feedbackMsg == null)
            {
                var fallbackFeedback = ReadFeedbackText(detail?.FeedbackGeral);
                if (detail?.ScoreGeral is > 0 || !string.IsNullOrWhiteSpace(fallbackFeedback))
                {
                    return new StructuredInterviewSavedFeedback
                    {
                        Score = detail?.ScoreGeral ?? 0,
                        OverallFeedback = fallbackFeedback
                    };
                }

                return null;
            }

            var score = TryGetIntFromExtras(feedbackMsg.DadosExtras, "score") ?? detail?.ScoreGeral ?? 0;

            var strengths = ParseExtrasStringList(feedbackMsg.DadosExtras, "strengths");
            var improvements = ParseExtrasStringList(feedbackMsg.DadosExtras, "improvements");
            var script = feedbackMsg.DadosExtras?.GetValueOrDefault("videoScript")?.ToString()
                ?? TryParseFeedbackScript(feedbackMsg.Conteudo);

            return new StructuredInterviewSavedFeedback
            {
                Score = score,
                OverallFeedback = feedbackMsg.Feedback ?? ReadFeedbackText(detail?.FeedbackGeral),
                FeedbackScript = script ?? "",
                Strengths = strengths,
                Improvements = improvements
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar feedback salvo da entrevista");
            return null;
        }
    }

    private static string ReadFeedbackText(JsonElement? feedbackGeral)
    {
        if (!feedbackGeral.HasValue)
        {
            return "";
        }

        var el = feedbackGeral.Value;
        if (el.ValueKind == JsonValueKind.String)
        {
            return el.GetString() ?? "";
        }

        if (el.TryGetProperty("overallFeedback", out var overall))
        {
            return overall.GetString() ?? el.ToString();
        }

        return el.ToString();
    }

    private static int? TryGetIntFromExtras(Dictionary<string, object?>? extras, string key)
    {
        if (extras == null || !extras.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            int i => i,
            long l => (int)l,
            JsonElement je when je.TryGetInt32(out var n) => n,
            _ when int.TryParse(value.ToString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static List<string> ParseExtrasStringList(Dictionary<string, object?>? extras, string key)
    {
        if (extras == null || !extras.TryGetValue(key, out var value) || value == null)
        {
            return [];
        }

        if (value is JsonElement arr && arr.ValueKind == JsonValueKind.Array)
        {
            return arr.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }

        return [];
    }

    private static string? TryParseFeedbackScript(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("script", out var script))
            {
                return script.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
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

    private async Task<string> ExtractCandidateNameAsync(string resumeText, CancellationToken cancellationToken)
    {
        try
        {
            var prompt = """
                Extraia o nome completo do candidato deste currículo.
                Se não encontrar, retorne o primeiro nome mais provável ou "Candidato".
                Retorne APENAS JSON com campo name.

                CURRÍCULO:
                """ + SafeTake(resumeText, 1500);

            var raw = await _ai.GenerateTextAsync(prompt, 0.2, 200, cancellationToken);
            var name = ParseJsonField(raw, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao extrair nome do candidato");
        }

        return "Candidato";
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

    private static List<string> DefaultWrittenQuestions(AnalysisInput analysis)
    {
        var area = analysis.AreaAtuacao ?? "sua área";
        return
        [
            $"Descreva sua experiência mais relevante em {area} e os resultados que alcançou.",
            "Quais tecnologias ou ferramentas do seu currículo você domina melhor? Dê um exemplo prático.",
            "Conte sobre um desafio profissional difícil e como você resolveu.",
            "Como você trabalha em equipe e lida com prazos apertados?",
            "O que você busca no próximo passo da sua carreira?"
        ];
    }

    private static string BuildWrittenAnswersBlock(IReadOnlyList<string> questions, IReadOnlyList<string> answers)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var a = i < answers.Count ? answers[i] : "";
            sb.AppendLine($"Pergunta {i + 1}: {q}");
            sb.AppendLine(string.IsNullOrWhiteSpace(a) ? "Resposta: (não respondida)" : $"Resposta: {a.Trim()}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildResponseSummary(
        IReadOnlyList<string> questions,
        IReadOnlyList<string> answers,
        string phase1Answer)
    {
        var sb = new StringBuilder();
        var writtenWords = 0;
        var emptyWritten = 0;

        for (var i = 0; i < questions.Count; i++)
        {
            var a = i < answers.Count ? answers[i]?.Trim() ?? "" : "";
            var words = CountWords(a);
            writtenWords += words;
            if (string.IsNullOrWhiteSpace(a))
            {
                emptyWritten++;
                sb.AppendLine($"- Pergunta escrita {i + 1}: SEM RESPOSTA");
            }
            else
            {
                sb.AppendLine($"- Pergunta escrita {i + 1}: {words} palavras");
            }
        }

        var spokenWords = CountWords(phase1Answer);
        sb.AppendLine(spokenWords < 5
            ? "- Apresentação em voz: SEM RESPOSTA ou muito curta"
            : $"- Apresentação em voz: {spokenWords} palavras");
        sb.AppendLine($"- Total palavras escritas: {writtenWords}");
        sb.AppendLine($"- Perguntas escritas vazias: {emptyWritten} de {questions.Count}");

        return sb.ToString();
    }

    private static List<string> PadList(IReadOnlyList<string> items, int count, string fallbackPrefix)
    {
        var list = items?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? [];
        while (list.Count < count)
        {
            list.Add($"{fallbackPrefix} {list.Count + 1}");
        }

        return list.Take(count).ToList();
    }

    private static string FormatCandidateLine(string candidateName, string? answer)
    {
        var trimmed = answer?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return $"[{candidateName}]: (não respondeu / sem fala registrada)";
        }

        return $"[{candidateName}]: {trimmed}";
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static void ApplyNoResponseSafeguard(
        StructuredInterviewFinishResult result,
        string candidateName,
        int totalCandidateWords)
    {
        if (totalCandidateWords >= 20)
        {
            return;
        }

        result.Score = Math.Min(result.Score, totalCandidateWords < 8 ? 20 : 40);
        result.OverallFeedback =
            "Você deixou respostas em branco ou falou muito pouco. " +
            "O feedback considera apenas o que foi escrito e falado nesta simulação.";
        result.Strengths = totalCandidateWords > 0
            ? ["Participou parcialmente da simulação"]
            : [];
        result.Improvements =
        [
            "Responder às 5 perguntas escritas com exemplos concretos",
            "Usar o tempo de apresentação em voz para se descrever profissionalmente",
            "Detalhar resultados e tecnologias que domina"
        ];
        result.FeedbackScript =
            $"Olá, {candidateName}. Obrigado por participar. " +
            "Percebi pouco conteúdo nas respostas escritas e na apresentação em voz. " +
            "Na próxima vez, aproveite todas as etapas para demonstrar sua experiência. Sucesso!";
    }

    private static int WordsForSeconds(int seconds) => Math.Max(15, (int)(seconds * 2.5));

    private static string ApplyTemplate(string template, Dictionary<string, string> values)
    {
        var result = template;
        foreach (var (key, value) in values)
        {
            result = result.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        }

        return result;
    }

    private static string SafeTake(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || maxLength <= 0)
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
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

    private static List<string> ParseStringArray(string raw, string field)
    {
        try
        {
            var cleaned = AiService.CleanMarkdownFence(raw.Trim());
            var match = Regex.Match(cleaned, @"\{.*\}", RegexOptions.Singleline);
            var json = match.Success ? match.Value : cleaned;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(field, out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                return arr.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToList();
            }
        }
        catch
        {
            // ignore
        }

        return [];
    }

    private static StructuredInterviewFinishResult ParseFinishResult(string raw)
    {
        try
        {
            var cleaned = AiService.CleanMarkdownFence(raw.Trim());
            var match = Regex.Match(cleaned, @"\{.*\}", RegexOptions.Singleline);
            var json = match.Success ? match.Value : cleaned;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new StructuredInterviewFinishResult
            {
                Score = root.TryGetProperty("score", out var s) && s.TryGetInt32(out var sc)
                    ? Math.Clamp(sc, 0, 100)
                    : 75,
                FeedbackScript = root.TryGetProperty("script", out var script)
                    ? script.GetString() ?? ""
                    : "",
                OverallFeedback = root.TryGetProperty("overallFeedback", out var f)
                    ? f.GetString() ?? ""
                    : "",
                Strengths = ParseStringArrayFromRoot(root, "strengths"),
                Improvements = ParseStringArrayFromRoot(root, "improvements")
            };
        }
        catch
        {
            return new StructuredInterviewFinishResult
            {
                Score = 75,
                FeedbackScript = "Obrigado pela participação. Continue praticando respostas objetivas com exemplos concretos.",
                OverallFeedback = "Entrevista concluída. Revise o relatório para detalhes.",
                Strengths = ["Participou da entrevista"],
                Improvements = ["Detalhar mais resultados quantificáveis"]
            };
        }
    }

    private static List<string> ParseStringArrayFromRoot(JsonElement root, string property)
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
