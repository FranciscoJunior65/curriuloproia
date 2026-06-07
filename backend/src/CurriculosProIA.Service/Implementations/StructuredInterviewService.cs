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

    private static readonly InterviewPersonaDto DefaultInterviewer = new()
    {
        Name = "Entrevistadora",
        Role = "Recrutadora",
        Initials = "AR",
        AvatarColor = "#6366f1"
    };

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
        var writtenQuestions = ParseWrittenQuestions(raw);
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
                    writtenQuestions.Select(q => q.Text).ToList(),
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

        var fallbackIntro =
            $"Olá, {candidateName}! Sou {persona.Name}, {persona.Role}. Em instantes você terá tempo para se apresentar.";

        string introScript;
        try
        {
            var raw = await _ai.GenerateTextAsync(prompt, 0.65, 1024, cancellationToken);
            introScript = ParseJsonField(raw, "script") ?? fallbackIntro;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gerar roteiro de abertura com IA; usando roteiro padrão");
            introScript = fallbackIntro;
        }

        return new StructuredInterviewVoicePhaseResult
        {
            IntroScript = EnsureCandidateNameInScript(introScript.Trim(), candidateName)
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
        var persona = DefaultInterviewer;
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
        var fromResume = TryExtractNameFromResumeText(resumeText);
        if (!string.IsNullOrWhiteSpace(fromResume))
        {
            return fromResume;
        }

        try
        {
            var prompt = """
                Extraia o nome completo do candidato deste currículo.
                Use exatamente a grafia que aparece no documento (acentos e maiúsculas).
                Se não encontrar, retorne o primeiro nome mais provável ou "Candidato".
                Retorne APENAS JSON com campo name.

                CURRÍCULO:
                """ + SafeTake(resumeText, 1500);

            var raw = await _ai.GenerateTextAsync(prompt, 0.2, 512, cancellationToken);
            var name = ParseJsonField(raw, "name");
            if (!string.IsNullOrWhiteSpace(name) && IsPlausibleCandidateName(name))
            {
                return NormalizeCandidateName(name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao extrair nome do candidato");
        }

        return "Candidato";
    }

    private static string? TryExtractNameFromResumeText(string? resumeText)
    {
        if (string.IsNullOrWhiteSpace(resumeText))
        {
            return null;
        }

        var lines = resumeText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Take(20)
            .ToList();

        foreach (var line in lines)
        {
            var labeled = Regex.Match(line, @"^(?:nome|name)\s*[:\-]\s*(.+)$", RegexOptions.IgnoreCase);
            if (labeled.Success)
            {
                var name = NormalizeCandidateName(labeled.Groups[1].Value.Trim());
                if (IsPlausibleCandidateName(name))
                {
                    return name;
                }
            }
        }

        foreach (var line in lines.Take(5))
        {
            if (IsPlausibleCandidateName(line))
            {
                return NormalizeCandidateName(line);
            }
        }

        return null;
    }

    private static bool IsPlausibleCandidateName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length < 3 || trimmed.Length > 80)
        {
            return false;
        }

        if (Regex.IsMatch(trimmed, @"[@#/\\]|https?://|www\.|\d{4,}|\.com\b|\.br\b|linkedin|github|curriculum|currículo|resume|cv\b", RegexOptions.IgnoreCase))
        {
            return false;
        }

        var words = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is < 2 or > 6)
        {
            return false;
        }

        return words.All(word => Regex.IsMatch(word, @"^[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ''\-.]*$"));
    }

    private static string EnsureCandidateNameInScript(string script, string candidateName)
    {
        if (string.IsNullOrWhiteSpace(script)
            || string.IsNullOrWhiteSpace(candidateName)
            || string.Equals(candidateName, "Candidato", StringComparison.OrdinalIgnoreCase))
        {
            return script;
        }

        if (script.Contains(candidateName, StringComparison.OrdinalIgnoreCase))
        {
            return script;
        }

        var firstName = candidateName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstName)
            && script.Contains(firstName, StringComparison.OrdinalIgnoreCase))
        {
            return script;
        }

        return $"Olá, {candidateName}! {script}";
    }

    private static string NormalizeCandidateName(string name)
    {
        var trimmed = Regex.Replace(name.Trim(), @"\s+", " ");
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        if (trimmed.All(c => !char.IsLetter(c) || char.IsUpper(c)))
        {
            return string.Join(' ', trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpper(word[0]) + word[1..].ToLowerInvariant()));
        }

        return trimmed;
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

    private static List<WrittenQuestionDto> DefaultWrittenQuestions(AnalysisInput analysis)
    {
        var area = analysis.AreaAtuacao ?? "sua área";
        return
        [
            new WrittenQuestionDto
            {
                Text = $"Qual experiência em {area} melhor representa seu perfil?",
                Type = "choice",
                Options =
                [
                    "Experiência sênior com resultados mensuráveis",
                    "Experiência intermediária em projetos relevantes",
                    "Experiência júnior com aprendizado acelerado",
                    "Experiência em outra área com transferência de habilidades"
                ]
            },
            new WrittenQuestionDto
            {
                Text = "Como você costuma trabalhar em equipe?",
                Type = "choice",
                Options =
                [
                    "Colaboração constante e comunicação aberta",
                    "Autonomia com alinhamentos periódicos",
                    "Liderança técnica e mentoria",
                    "Execução focada com entregas individuais"
                ]
            },
            new WrittenQuestionDto
            {
                Text = "O que mais te motiva profissionalmente?",
                Type = "choice",
                Options =
                [
                    "Resolver problemas complexos",
                    "Crescer em novas tecnologias",
                    "Impactar resultados do negócio",
                    "Estabilidade e evolução de carreira"
                ]
            },
            new WrittenQuestionDto
            {
                Text = "Quais tecnologias ou ferramentas do seu currículo você domina melhor? Dê um exemplo prático.",
                Type = "open"
            },
            new WrittenQuestionDto
            {
                Text = "Conte sobre um desafio profissional difícil e como você resolveu.",
                Type = "open"
            }
        ];
    }

    private static List<WrittenQuestionDto> ParseWrittenQuestions(string raw)
    {
        try
        {
            var cleaned = AiService.CleanMarkdownFence(raw.Trim());
            var match = Regex.Match(cleaned, @"\{.*\}", RegexOptions.Singleline);
            var json = match.Success ? match.Value : cleaned;
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("questions", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var questions = new List<WrittenQuestionDto>();
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        questions.Add(new WrittenQuestionDto { Text = text.Trim(), Type = "open" });
                    }

                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var questionText = item.TryGetProperty("text", out var textEl)
                    ? textEl.GetString()
                    : item.TryGetProperty("question", out var legacyEl)
                        ? legacyEl.GetString()
                        : null;
                if (string.IsNullOrWhiteSpace(questionText))
                {
                    continue;
                }

                var type = item.TryGetProperty("type", out var typeEl)
                    ? typeEl.GetString()?.Trim().ToLowerInvariant()
                    : "open";
                var isChoice = type == "choice";
                var options = new List<string>();
                if (item.TryGetProperty("options", out var optionsEl) &&
                    optionsEl.ValueKind == JsonValueKind.Array)
                {
                    options = optionsEl.EnumerateArray()
                        .Select(e => e.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s!.Trim())
                        .Take(6)
                        .ToList();
                }

                if (isChoice && options.Count < 2)
                {
                    isChoice = false;
                }

                questions.Add(new WrittenQuestionDto
                {
                    Text = questionText.Trim(),
                    Type = isChoice ? "choice" : "open",
                    Options = isChoice ? options : []
                });
            }

            return questions;
        }
        catch
        {
            return [];
        }
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
