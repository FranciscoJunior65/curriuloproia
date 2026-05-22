using Microsoft.Extensions.Logging;
using System.Text;
using CurriculosProIA.Service.Interfaces;
using System.Text.Json;
using System.Text.RegularExpressions;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

using CurriculosProIA.Repository.Interfaces;

namespace CurriculosProIA.Service.Implementations;

public class InterviewSimulationService : IInterviewSimulationService
{
    private readonly IAiService _aiService;
    private readonly IJobSitesService _jobSites;
    private readonly IInterviewRepository _interviews;
    private readonly ILogger<InterviewSimulationService> _logger;

    public InterviewSimulationService(
        IAiService aiService,
        IJobSitesService jobSites,
        IInterviewRepository interviews,
        ILogger<InterviewSimulationService> logger)
    {
        _aiService = aiService;
        _jobSites = jobSites;
        _interviews = interviews;
        _logger = logger;
    }

    public async Task<(string? SimulationId, List<string> Questions)> StartInterviewAsync(
        string resumeText,
        AnalysisInput analysis,
        string? siteId,
        string? userId,
        string? resumeId,
        CancellationToken cancellationToken = default)
    {
        var questions = await GenerateInterviewQuestionsAsync(resumeText, analysis, siteId, cancellationToken);
        string? simulationId = null;

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(resumeId) && !string.IsNullOrEmpty(siteId))
        {
            try
            {
                simulationId = await _interviews.CreateInterviewSimulationAsync(
                    userId,
                    resumeId,
                    siteId,
                    questions,
                    analysis.AreaAtuacao ?? "Geral",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar simulação no banco");
            }
        }

        return (simulationId, questions);
    }

    public async Task<InterviewEvaluation> EvaluateAnswerAsync(
        string question,
        string answer,
        string resumeText,
        AnalysisInput analysis,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var habilidades = analysis.Habilidades != null ? string.Join(", ", analysis.Habilidades) : "Não especificado";
            var prompt = $"""
                Você é um recrutador técnico avaliando uma resposta de entrevista.

                PERGUNTA:
                {question}

                RESPOSTA DO CANDIDATO:
                {answer}

                CONTEXTO DO CURRÍCULO:
                {resumeText[..Math.Min(resumeText.Length, 1000)]}

                ANÁLISE DO CURRÍCULO:
                - Habilidades: {habilidades}
                - Experiência: {analysis.Experiencia ?? "Não especificado"}

                INSTRUÇÕES:
                1. Avalie a qualidade da resposta (0-100)
                2. Forneça feedback construtivo
                3. Identifique pontos fortes e fracos
                4. Retorne APENAS um objeto JSON com campos score, feedback, strengths e improvements
                """;

            var response = await _aiService.GenerateTextAsync(prompt, 0.7, 500, cancellationToken);
            var evaluation = ParseEvaluation(response);
            if (evaluation != null)
            {
                return evaluation;
            }

            throw new InvalidOperationException("A IA retornou uma avaliação inválida ou vazia.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao avaliar resposta com IA");
            throw new InvalidOperationException(
                "Não foi possível avaliar a resposta com IA. Verifique GEMINI_API_KEY e USE_MOCK_AI=false.",
                ex);
        }
    }

    public Task<bool> SaveInterviewMessageAsync(
        string simulationId,
        string question,
        string answer,
        InterviewEvaluation evaluation,
        int order,
        CancellationToken cancellationToken = default) =>
        _interviews.SaveInterviewMessageAsync(simulationId, question, answer, evaluation, order, cancellationToken);

    public async Task<int> FinishInterviewAsync(
        string simulationId,
        List<InterviewAnswerItem> allAnswers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _interviews.UpdateSimulationAnswersAsync(simulationId, allAnswers, cancellationToken);
            return result.AverageScore;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao finalizar simulação no banco");
            var scores = allAnswers.Select(a => a.Evaluation?.Score ?? 70).ToList();
            return scores.Count > 0 ? (int)Math.Round(scores.Average()) : 70;
        }
    }

    public Task<InterviewDetailDto?> GetInterviewByIdAsync(string simulationId, CancellationToken cancellationToken = default) =>
        _interviews.GetInterviewByIdAsync(simulationId, cancellationToken);

    public Task<List<SimulacaoEntrevistaRow>> GetUserInterviewsAsync(string userId, CancellationToken cancellationToken = default) =>
        _interviews.GetUserInterviewsAsync(userId, cancellationToken: cancellationToken);

    public string BuildInterviewDownloadContent(InterviewDetailDto interview)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========================================");
        sb.AppendLine("SIMULAÇÃO DE ENTREVISTA - RELATÓRIO COMPLETO");
        sb.AppendLine("========================================");
        sb.AppendLine();
        sb.AppendLine($"ID da Simulação: {interview.Id}");
        sb.AppendLine($"Data: {interview.CriadoEm?.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") ?? ""}");
        sb.AppendLine($"Área de Foco: {interview.AreaFoco ?? "Geral"}");
        sb.AppendLine($"Total de Perguntas: {interview.PerguntasFeitas?.Count ?? 0}");
        sb.AppendLine($"Score Médio: {interview.ScoreGeral ?? 0}/100");
        sb.AppendLine();

        if (interview.FeedbackGeral.HasValue &&
            interview.FeedbackGeral.Value.TryGetProperty("statistics", out var stats))
        {
            sb.AppendLine("Estatísticas:");
            sb.AppendLine($"- Respostas Boas (≥70): {GetStatInt(stats, "goodAnswers")}");
            sb.AppendLine($"- Respostas Médias (50-69): {GetStatInt(stats, "averageAnswers")}");
            sb.AppendLine($"- Precisam Melhorar (<50): {GetStatInt(stats, "poorAnswers")}");
            sb.AppendLine($"- Score Mínimo: {GetStatInt(stats, "minScore")}");
            sb.AppendLine($"- Score Máximo: {GetStatInt(stats, "maxScore")}");
            sb.AppendLine();
        }

        sb.AppendLine("========================================");
        sb.AppendLine("PERGUNTAS E RESPOSTAS");
        sb.AppendLine("========================================");
        sb.AppendLine();

        var questions = interview.Messages.Where(m => m.Tipo == "pergunta").ToList();
        for (var index = 0; index < questions.Count; index++)
        {
            var questionMsg = questions[index];
            var questionOrder = questionMsg.DadosExtras != null &&
                                questionMsg.DadosExtras.TryGetValue("questionIndex", out var qi) &&
                                qi is JsonElement je && je.ValueKind == JsonValueKind.Number
                ? je.GetInt32()
                : index;

            var answerMsg = interview.Messages.FirstOrDefault(m =>
                m.Tipo == "resposta" &&
                m.DadosExtras != null &&
                m.DadosExtras.TryGetValue("questionIndex", out var aqi) &&
                Convert.ToInt32(aqi) == questionOrder);

            var feedbackMsg = interview.Messages.FirstOrDefault(m =>
                m.Tipo == "feedback" &&
                m.DadosExtras != null &&
                m.DadosExtras.TryGetValue("questionIndex", out var fqi) &&
                Convert.ToInt32(fqi) == questionOrder);

            sb.AppendLine($"PERGUNTA {index + 1}:");
            sb.AppendLine(questionMsg.Conteudo);
            sb.AppendLine();

            if (answerMsg != null)
            {
                sb.AppendLine("RESPOSTA:");
                sb.AppendLine(answerMsg.Conteudo);
                sb.AppendLine();
            }

            if (feedbackMsg != null)
            {
                try
                {
                    var evaluation = JsonSerializer.Deserialize<InterviewEvaluation>(feedbackMsg.Conteudo ?? "{}");
                    sb.AppendLine("AVALIAÇÃO:");
                    sb.AppendLine($"Score: {evaluation?.Score ?? feedbackMsg.DadosExtras?.GetValueOrDefault("score") ?? 0}/100");
                    sb.AppendLine($"Feedback: {evaluation?.Feedback ?? feedbackMsg.Feedback ?? ""}");

                    if (evaluation?.Strengths is { Count: > 0 })
                    {
                        sb.AppendLine("Pontos Fortes:");
                        foreach (var strength in evaluation.Strengths)
                        {
                            sb.AppendLine($"- {strength}");
                        }
                    }

                    if (evaluation?.Improvements is { Count: > 0 })
                    {
                        sb.AppendLine("Pontos a Melhorar:");
                        foreach (var improvement in evaluation.Improvements)
                        {
                            sb.AppendLine($"- {improvement}");
                        }
                    }
                }
                catch
                {
                    sb.AppendLine($"Feedback: {feedbackMsg.Feedback ?? ""}");
                }
            }

            sb.AppendLine();
            sb.AppendLine(new string('=', 40));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static int GetStatInt(JsonElement stats, string property)
    {
        return stats.TryGetProperty(property, out var value) && value.TryGetInt32(out var n) ? n : 0;
    }

    private async Task<List<string>> GenerateInterviewQuestionsAsync(
        string resumeText,
        AnalysisInput analysis,
        string? siteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var siteInfo = "";
            if (!string.IsNullOrEmpty(siteId))
            {
                var site = await _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken);
                if (site != null)
                {
                    siteInfo = $"\n\nCONTEXTO DO SITE DE VAGAS: {site.Nome}\n{site.Descricao ?? ""}";
                }
            }

            var technologies = ExtractTechnologies(resumeText, analysis);
            var habilidades = analysis.Habilidades != null ? string.Join(", ", analysis.Habilidades) : "Não especificado";
            var pontosFortes = analysis.PontosFortes != null ? string.Join(", ", analysis.PontosFortes.Take(5)) : "Não especificado";

            var prompt = $"""
                Você é um recrutador técnico experiente. Com base no currículo e análise fornecidos, gere uma lista de 8-10 perguntas de entrevista técnica e comportamental relevantes.

                CURRÍCULO:
                {resumeText[..Math.Min(resumeText.Length, 2000)]}

                ANÁLISE DO CURRÍCULO:
                - Habilidades: {habilidades}
                - Experiência: {analysis.Experiencia ?? "Não especificado"}
                - Pontos Fortes: {pontosFortes}
                - Área de Atuação: {analysis.AreaAtuacao ?? "Não especificado"}
                {(technologies.Count > 0 ? $"- Tecnologias Identificadas: {string.Join(", ", technologies)}" : "")}
                {siteInfo}

                INSTRUÇÕES:
                1. Gere perguntas técnicas específicas sobre as tecnologias mencionadas no currículo
                2. Inclua perguntas comportamentais
                3. Retorne APENAS um array JSON de strings, sem explicações

                FORMATO DE RESPOSTA (JSON array):
                ["Pergunta 1", "Pergunta 2", "Pergunta 3", ...]
                """;

            var response = await _aiService.GenerateTextAsync(prompt, 0.7, 1000, cancellationToken);
            var questions = ParseQuestions(response);
            if (questions.Count > 0)
            {
                return questions;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar perguntas com IA");
            throw new InvalidOperationException(
                "Não foi possível gerar perguntas da entrevista com IA. Verifique GEMINI_API_KEY e USE_MOCK_AI=false.",
                ex);
        }

        throw new InvalidOperationException("A IA não retornou perguntas válidas para a entrevista.");
    }

    private static List<string> ExtractTechnologies(string resumeText, AnalysisInput analysis)
    {
        var techList = new List<string>();
        if (analysis.Habilidades != null)
        {
            techList.AddRange(analysis.Habilidades);
        }

        var text = resumeText.ToLowerInvariant();
        var patterns = new[]
        {
            "javascript", "typescript", "python", "java", "c#", "php", "ruby", "go", "rust",
            "react", "angular", "vue", "node.js", "express", "django", "flask", "spring",
            "sql", "mysql", "postgresql", "mongodb", "redis", "aws", "azure", "docker", "kubernetes", "git"
        };

        foreach (var tech in patterns.Where(t => text.Contains(t)))
        {
            techList.Add(tech);
        }

        return techList.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
    }

    private static List<string> ParseQuestions(string response)
    {
        try
        {
            var cleaned = AiService.CleanMarkdownFence(response);
            var match = Regex.Match(cleaned, @"\[.*\]", RegexOptions.Singleline);
            var json = match.Success ? match.Value : cleaned;
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static InterviewEvaluation? ParseEvaluation(string response)
    {
        try
        {
            var cleaned = AiService.CleanMarkdownFence(response);
            var match = Regex.Match(cleaned, @"\{.*\}", RegexOptions.Singleline);
            var json = match.Success ? match.Value : cleaned;
            return JsonSerializer.Deserialize<InterviewEvaluation>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GenerateDefaultQuestions(List<string> technologies)
    {
        var baseQuestions = new List<string>
        {
            "Conte-me sobre você e sua experiência profissional.",
            "Qual foi o projeto mais desafiador que você já trabalhou?",
            "Como você lida com prazos apertados e pressão no trabalho?",
            "Descreva uma situação onde você teve que trabalhar em equipe para resolver um problema.",
            "O que você sabe sobre nossa empresa?",
            "Por que você quer trabalhar conosco?",
            "Quais são suas principais conquistas profissionais?",
            "Como você se mantém atualizado com as novas tecnologias?"
        };

        var techQuestions = new List<string>();
        if (technologies.Count > 0)
        {
            var mainTech = technologies[0];
            techQuestions.Add($"Explique como você usa {mainTech} em seus projetos.");
            techQuestions.Add($"Quais são os principais desafios ao trabalhar com {mainTech}?");
            techQuestions.Add($"Conte-me sobre um projeto onde você usou {mainTech}.");
        }

        return techQuestions.Concat(baseQuestions).Take(10).ToList();
    }
}
