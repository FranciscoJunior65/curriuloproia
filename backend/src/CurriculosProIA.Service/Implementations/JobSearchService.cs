using System.Text.Json;
using System.Text.RegularExpressions;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;
using HtmlAgilityPack;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class JobSearchService : IJobSearchService
{
    private readonly IJobSitesService _jobSites;
    private readonly IAiService _aiService;
    private readonly IInterviewRepository _interviews;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JobSearchService> _logger;

    private static readonly string[] TechPatterns =
    [
        "javascript", "typescript", "python", "java", "c#", "c++", "php", "ruby", "go", "rust",
        "react", "angular", "vue", "node.js", "express", "django", "flask", "spring", "laravel",
        "sql", "mysql", "postgresql", "mongodb", "redis", "aws", "azure", "docker", "kubernetes", "git"
    ];

    public JobSearchService(
        IJobSitesService jobSites,
        IAiService aiService,
        IInterviewRepository interviews,
        IHttpClientFactory httpClientFactory,
        ILogger<JobSearchService> logger)
    {
        _jobSites = jobSites;
        _aiService = aiService;
        _interviews = interviews;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<JobSearchResult> SearchJobsBySiteAsync(
        string siteId,
        AnalysisInput analysis,
        string location = "Brasil",
        string? resumeText = null,
        string? userId = null,
        string? resumeId = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(resumeText))
        {
            return await SearchJobsAdvancedAsync(siteId, resumeText, analysis, location, userId, resumeId, cancellationToken);
        }

        var site = await _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken)
            ?? throw new InvalidOperationException("Site de vagas não encontrado");

        var searchTerms = ExtractSearchTerms(analysis);
        var siteName = (site.Nome ?? "").ToLowerInvariant();

        if (siteName.Contains("linkedin"))
        {
            return await SearchLinkedInJobsAsync(searchTerms, location, cancellationToken);
        }

        if (siteName.Contains("catho"))
        {
            return await SearchCathoJobsAsync(searchTerms, location, cancellationToken);
        }

        if (siteName.Contains("indeed"))
        {
            return await SearchIndeedJobsAsync(searchTerms, location, cancellationToken);
        }

        return SearchGenericJobs(site.Nome ?? siteId, searchTerms, location);
    }

    private async Task<JobSearchResult> SearchJobsAdvancedAsync(
        string siteId,
        string resumeText,
        AnalysisInput analysis,
        string location,
        string? userId,
        string? resumeId,
        CancellationToken cancellationToken)
    {
        var site = await _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken)
            ?? throw new InvalidOperationException("Site de vagas não encontrado");

        var keywords = await GenerateSearchKeywordsWithAiAsync(resumeText, analysis, site, cancellationToken);
        var combinations = GenerateSearchCombinations(keywords, 8);
        var allJobs = new List<JobListing>();
        var siteName = (site.Nome ?? "").ToLowerInvariant();

        foreach (var combination in combinations)
        {
            try
            {
                JobSearchResult searchResults;
                if (siteName.Contains("catho"))
                {
                    searchResults = await SearchCathoJobsAsync(combination, location, cancellationToken);
                }
                else if (siteName.Contains("indeed"))
                {
                    searchResults = await SearchIndeedJobsAsync(combination, location, cancellationToken);
                }
                else if (siteName.Contains("linkedin"))
                {
                    searchResults = await SearchLinkedInJobsAsync(combination, location, cancellationToken);
                }
                else
                {
                    searchResults = SearchGenericJobs(site.Nome ?? siteId, combination, location);
                }

                foreach (var job in searchResults.Jobs)
                {
                    var isDuplicate = allJobs.Any(j =>
                        j.Url == job.Url || (j.Title == job.Title && j.Company == job.Company));
                    if (isDuplicate)
                    {
                        continue;
                    }

                    try
                    {
                        var details = await ExtractJobDetailsAsync(job.Url, site.Nome ?? "", cancellationToken);
                        job.Description = details.Description;
                        job.Requirements = details.Requirements;
                        job.Salary = details.Salary;
                        job.ContractType = details.ContractType;
                        job.ExperienceLevel = details.ExperienceLevel;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erro ao extrair detalhes da vaga {Url}", job.Url);
                    }

                    var compatibility = CalculateCompatibilityScore(job, analysis, keywords);
                    job.CompatibilityScore = compatibility.Score;
                    job.MatchedKeywords = compatibility.MatchedKeywords;
                    allJobs.Add(job);
                }

                await Task.Delay(1000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro na busca com combinação {Terms}", string.Join(' ', combination));
            }
        }

        var uniqueJobs = allJobs
            .GroupBy(j => $"{j.Url}_{j.Title}_{j.Company}")
            .Select(g => g.First())
            .OrderByDescending(j => j.CompatibilityScore ?? 0)
            .ToList();

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(resumeId) && uniqueJobs.Count > 0)
        {
            try
            {
                await _interviews.SaveFoundJobsAsync(userId, resumeId, siteId, uniqueJobs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar vagas no banco");
            }
        }

        return new JobSearchResult
        {
            Site = site.Nome ?? "",
            Url = site.UrlBase ?? "",
            Jobs = uniqueJobs.Take(50).ToList(),
            TotalFound = uniqueJobs.Count,
            SearchKeywords = keywords,
            SearchCombinations = combinations.Count,
            Message = $"{uniqueJobs.Count} vagas encontradas após {combinations.Count} buscas"
        };
    }

    private async Task<List<string>> GenerateSearchKeywordsWithAiAsync(
        string resumeText,
        AnalysisInput analysis,
        SiteVagasRow site,
        CancellationToken cancellationToken)
    {
        try
        {
            var characteristics = site.Caracteristicas != null
                ? JsonSerializer.Serialize(site.Caracteristicas, new JsonSerializerOptions { WriteIndented = true })
                : "{}";
            var siteKeywords = site.PalavrasChavePadrao != null ? string.Join(", ", site.PalavrasChavePadrao) : "Nenhuma";

            var prompt = $"""
                Você é um especialista em recrutamento e busca de vagas. Analise o currículo e a análise fornecida para gerar palavras-chave otimizadas para busca de vagas no site {site.Nome}.

                CURRÍCULO:
                {resumeText[..Math.Min(resumeText.Length, 2000)]}

                ANÁLISE DO CURRÍCULO:
                - Habilidades: {(analysis.Habilidades != null ? string.Join(", ", analysis.Habilidades) : "Não especificado")}
                - Experiência: {analysis.Experiencia ?? "Não especificado"}
                - Pontos Fortes: {(analysis.PontosFortes != null ? string.Join(", ", analysis.PontosFortes.Take(5)) : "Não especificado")}
                - Área de Atuação: {analysis.AreaAtuacao ?? "Não especificado"}

                CARACTERÍSTICAS DO SITE {site.Nome}:
                {characteristics}

                PALAVRAS-CHAVE PADRÃO DO SITE:
                {siteKeywords}

                INSTRUÇÕES:
                1. Gere 15-20 palavras-chave relevantes para busca de vagas
                2. Retorne APENAS um array JSON de strings, sem explicações adicionais

                FORMATO DE RESPOSTA (JSON array):
                ["palavra-chave 1", "palavra-chave 2", "palavra-chave 3", ...]
                """;

            var response = await _aiService.GenerateTextAsync(prompt, 0.7, 1000, cancellationToken);
            var cleaned = AiService.CleanMarkdownFence(response);
            var match = Regex.Match(cleaned, @"\[.*\]", RegexOptions.Singleline);
            if (match.Success)
            {
                var keywords = JsonSerializer.Deserialize<List<string>>(match.Value);
                if (keywords is { Count: > 0 })
                {
                    return keywords;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao gerar palavras-chave com IA");
        }

        return ExtractFallbackKeywords(analysis);
    }

    private static List<string> ExtractFallbackKeywords(AnalysisInput analysis)
    {
        var keywords = new List<string>();
        if (analysis.Habilidades != null)
        {
            keywords.AddRange(analysis.Habilidades);
        }

        if (!string.IsNullOrEmpty(analysis.AreaAtuacao))
        {
            keywords.Add(analysis.AreaAtuacao);
        }

        keywords.AddRange(["desenvolvedor", "programador", "analista", "engenheiro", "tecnologia", "software"]);
        return keywords.Distinct(StringComparer.OrdinalIgnoreCase).Take(15).ToList();
    }

    private static List<List<string>> GenerateSearchCombinations(List<string> keywords, int maxCombinations = 10)
    {
        var combinations = new List<List<string>>();
        var topKeywords = keywords.Take(8).ToList();

        foreach (var keyword in topKeywords)
        {
            combinations.Add([keyword]);
        }

        for (var i = 0; i < Math.Min(5, topKeywords.Count - 1); i++)
        {
            for (var j = i + 1; j < Math.Min(i + 3, topKeywords.Count); j++)
            {
                combinations.Add([topKeywords[i], topKeywords[j]]);
            }
        }

        if (combinations.Count < maxCombinations && topKeywords.Count >= 3)
        {
            for (var i = 0; i < Math.Min(3, topKeywords.Count - 2); i++)
            {
                combinations.Add([topKeywords[i], topKeywords[i + 1], topKeywords[i + 2]]);
            }
        }

        return combinations.Take(maxCombinations).ToList();
    }

    private static (int Score, List<string> MatchedKeywords) CalculateCompatibilityScore(
        JobListing job,
        AnalysisInput analysis,
        List<string> keywords)
    {
        var score = 0;
        var matched = new List<string>();
        var jobText = $"{job.Title} {job.Company} {job.Description} {string.Join(' ', job.Requirements ?? [])}".ToLowerInvariant();

        foreach (var keyword in keywords)
        {
            if (jobText.Contains(keyword.ToLowerInvariant()))
            {
                score += 10;
                matched.Add(keyword);
            }
        }

        if (analysis.Habilidades != null)
        {
            foreach (var habilidade in analysis.Habilidades)
            {
                if (jobText.Contains(habilidade.ToLowerInvariant()))
                {
                    score += 15;
                }
            }
        }

        if (!string.IsNullOrEmpty(analysis.AreaAtuacao) &&
            jobText.Contains(analysis.AreaAtuacao.ToLowerInvariant()))
        {
            score += 20;
        }

        return (Math.Min(100, score), matched.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    public static List<string> ExtractSearchTerms(AnalysisInput analysis)
    {
        var terms = new List<string>();

        if (analysis.Habilidades != null)
        {
            terms.AddRange(analysis.Habilidades);
        }

        if (!string.IsNullOrEmpty(analysis.Experiencia))
        {
            terms.AddRange(ExtractTechKeywords(analysis.Experiencia));
        }

        if (analysis.PontosFortes != null)
        {
            foreach (var ponto in analysis.PontosFortes.Where(p => p.Length < 50))
            {
                terms.AddRange(ExtractKeywordsFromText(ponto));
            }
        }

        return terms.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
    }

    private static List<string> ExtractTechKeywords(string text)
    {
        var keywords = new List<string>();
        foreach (var tech in TechPatterns)
        {
            if (text.Contains(tech, StringComparison.OrdinalIgnoreCase))
            {
                keywords.Add(tech);
            }
        }

        return keywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ExtractKeywordsFromText(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "de", "da", "do", "em", "para", "com", "por", "a", "o", "e", "é", "são", "foi", "ser", "ter", "mais", "muito", "bem", "pode", "deve"
        };

        return text.ToLowerInvariant()
            .Replace(",", " ")
            .Replace(".", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && !stopWords.Contains(w))
            .Take(5)
            .ToList();
    }

    private Task<JobSearchResult> SearchLinkedInJobsAsync(
        List<string> searchTerms,
        string location,
        CancellationToken cancellationToken)
    {
        var query = string.Join(" OR ", searchTerms);
        var searchUrl = $"https://www.linkedin.com/jobs/search/?keywords={Uri.EscapeDataString(query)}&location={Uri.EscapeDataString(location)}";

        return Task.FromResult(new JobSearchResult
        {
            Site = "LinkedIn",
            Url = searchUrl,
            Jobs = new List<JobListing>(),
            Message = "Busca no LinkedIn requer autenticação ou API. Retornando URL de busca.",
            SearchTerms = searchTerms
        });
    }

    private async Task<JobSearchResult> SearchCathoJobsAsync(
        List<string> searchTerms,
        string location,
        CancellationToken cancellationToken)
    {
        var query = string.Join(' ', searchTerms);
        var searchUrl = $"https://www.catho.com.br/vagas/?q={Uri.EscapeDataString(query)}&localizacao={Uri.EscapeDataString(location)}";

        try
        {
            var html = await FetchHtmlAsync(searchUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var jobs = new List<JobListing>();

            var nodes = doc.DocumentNode.SelectNodes("//*[contains(@class,'job-card') or contains(@class,'vaga-item') or contains(@data-testid,'job')]");
            if (nodes != null)
            {
                var index = 0;
                foreach (var element in nodes)
                {
                    if (index++ >= 10)
                    {
                        break;
                    }

                    var title = element.SelectSingleNode(".//h2 | .//h3 | .//*[contains(@class,'job-title')] | .//*[contains(@class,'vaga-titulo')]")?.InnerText?.Trim();
                    if (string.IsNullOrEmpty(title))
                    {
                        continue;
                    }

                    var company = element.SelectSingleNode(".//*[contains(@class,'company')] | .//*[contains(@class,'empresa')]")?.InnerText?.Trim();
                    var jobLocation = element.SelectSingleNode(".//*[contains(@class,'location')] | .//*[contains(@class,'localizacao')]")?.InnerText?.Trim();
                    var link = element.SelectSingleNode(".//a")?.GetAttributeValue("href", searchUrl) ?? searchUrl;
                    if (!link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        link = $"https://www.catho.com.br{link}";
                    }

                    jobs.Add(new JobListing
                    {
                        Title = title,
                        Company = string.IsNullOrWhiteSpace(company) ? "Não informado" : company,
                        Location = string.IsNullOrWhiteSpace(jobLocation) ? "Não informado" : jobLocation,
                        Url = link,
                        Site = "Catho"
                    });
                }
            }

            return new JobSearchResult
            {
                Site = "Catho",
                Url = searchUrl,
                Jobs = jobs,
                Message = jobs.Count > 0 ? $"{jobs.Count} vagas encontradas" : "Nenhuma vaga encontrada na busca automatizada",
                SearchTerms = searchTerms
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro no scraping do Catho");
            return new JobSearchResult
            {
                Site = "Catho",
                Url = searchUrl,
                Jobs = new List<JobListing>(),
                Message = "Não foi possível fazer scraping automático. Use o link fornecido para buscar manualmente.",
                SearchTerms = searchTerms
            };
        }
    }

    private async Task<JobSearchResult> SearchIndeedJobsAsync(
        List<string> searchTerms,
        string location,
        CancellationToken cancellationToken)
    {
        var query = string.Join(' ', searchTerms);
        var searchUrl = $"https://br.indeed.com/jobs?q={Uri.EscapeDataString(query)}&l={Uri.EscapeDataString(location)}";

        try
        {
            var html = await FetchHtmlAsync(searchUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var jobs = new List<JobListing>();

            var nodes = doc.DocumentNode.SelectNodes("//*[contains(@class,'job_seen_beacon')] | //*[contains(@class,'jobsearch-SerpJobCard')]");
            if (nodes != null)
            {
                var index = 0;
                foreach (var element in nodes)
                {
                    if (index++ >= 10)
                    {
                        break;
                    }

                    var title = element.SelectSingleNode(".//h2//a | .//*[contains(@class,'jobTitle')]//a")?.InnerText?.Trim();
                    if (string.IsNullOrEmpty(title))
                    {
                        continue;
                    }

                    var company = element.SelectSingleNode(".//*[contains(@class,'companyName')] | .//*[contains(@class,'company')]")?.InnerText?.Trim();
                    var jobLocation = element.SelectSingleNode(".//*[contains(@class,'companyLocation')] | .//*[contains(@class,'location')]")?.InnerText?.Trim();
                    var link = element.SelectSingleNode(".//h2//a | .//*[contains(@class,'jobTitle')]//a")?.GetAttributeValue("href", searchUrl) ?? searchUrl;
                    if (!link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        link = $"https://br.indeed.com{link}";
                    }

                    jobs.Add(new JobListing
                    {
                        Title = title,
                        Company = string.IsNullOrWhiteSpace(company) ? "Não informado" : company,
                        Location = string.IsNullOrWhiteSpace(jobLocation) ? "Não informado" : jobLocation,
                        Url = link,
                        Site = "Indeed"
                    });
                }
            }

            return new JobSearchResult
            {
                Site = "Indeed",
                Url = searchUrl,
                Jobs = jobs,
                Message = jobs.Count > 0 ? $"{jobs.Count} vagas encontradas" : "Nenhuma vaga encontrada",
                SearchTerms = searchTerms
            };
        }
        catch
        {
            return new JobSearchResult
            {
                Site = "Indeed",
                Url = searchUrl,
                Jobs = new List<JobListing>(),
                Message = "Use o link fornecido para buscar manualmente.",
                SearchTerms = searchTerms
            };
        }
    }

    private static JobSearchResult SearchGenericJobs(string siteName, List<string> searchTerms, string location)
    {
        var query = string.Join(' ', searchTerms);
        var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString($"{query} vagas {siteName} {location}")}";

        return new JobSearchResult
        {
            Site = siteName,
            Url = searchUrl,
            Jobs = new List<JobListing>(),
            Message = $"Busca genérica para {siteName}. Use o link fornecido.",
            SearchTerms = searchTerms
        };
    }

    private async Task<(string Description, List<string> Requirements, string Salary, string ContractType, string ExperienceLevel)> ExtractJobDetailsAsync(
        string jobUrl,
        string siteName,
        CancellationToken cancellationToken)
    {
        var html = await FetchHtmlAsync(jobUrl, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var description = "";
        var salary = "";

        if (siteName.Contains("catho", StringComparison.OrdinalIgnoreCase))
        {
            description = doc.DocumentNode.SelectSingleNode("//*[contains(@class,'job-description')] | //*[contains(@class,'descricao-vaga')]")?.InnerText?.Trim() ?? "";
            salary = doc.DocumentNode.SelectSingleNode("//*[contains(@class,'salary')] | //*[contains(@class,'salario')]")?.InnerText?.Trim() ?? "";
        }
        else if (siteName.Contains("indeed", StringComparison.OrdinalIgnoreCase))
        {
            description = doc.DocumentNode.SelectSingleNode("//*[@id='jobDescriptionText'] | //*[contains(@class,'jobsearch-jobDescriptionText')]")?.InnerText?.Trim() ?? "";
            salary = doc.DocumentNode.SelectSingleNode("//*[contains(@class,'salaryText')]")?.InnerText?.Trim() ?? "";
        }
        else
        {
            description = doc.DocumentNode.SelectSingleNode("//*[contains(@class,'description')]")?.InnerText?.Trim() ?? "";
        }

        var requirements = new List<string>();
        foreach (var li in doc.DocumentNode.SelectNodes("//ul//li | //ol//li") ?? Enumerable.Empty<HtmlNode>())
        {
            var text = li.InnerText.Trim().ToLowerInvariant();
            if (text.Contains("requisito") || text.Contains("exigência") || text.Contains("necessário"))
            {
                requirements.Add(li.InnerText.Trim());
            }
        }

        return (description, requirements, salary, "", "");
    }

    private async Task<string> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        var response = await client.GetAsync(url, cts.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cts.Token);
    }
}
