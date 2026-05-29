using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Service.Helpers;
using CurriculosProIA.Service.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class JobBoardScraperService : IJobBoardScraperService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JobBoardScraperService> _logger;

    public JobBoardScraperService(IHttpClientFactory httpClientFactory, ILogger<JobBoardScraperService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<JobListing>> SearchIndeedAsync(
        IReadOnlyList<string> searchTerms,
        string location = "Brasil",
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var query = string.Join(' ', searchTerms);
        var searchUrl = $"https://br.indeed.com/jobs?q={Uri.EscapeDataString(query)}&l={Uri.EscapeDataString(location)}";

        try
        {
            var html = await FetchHtmlAsync(searchUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var jobs = new List<JobListing>();

            var nodes = doc.DocumentNode.SelectNodes(
                "//*[contains(@class,'job_seen_beacon')] | //*[contains(@class,'jobsearch-SerpJobCard')] | //div[@data-jk]");
            if (nodes == null)
            {
                return jobs;
            }

            foreach (var element in nodes.Take(maxResults))
            {
                var title = element.SelectSingleNode(".//h2//a | .//*[contains(@class,'jobTitle')]//a | .//a[contains(@class,'jcs-JobTitle')]")?.InnerText?.Trim();
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                var company = element.SelectSingleNode(".//*[contains(@class,'companyName')] | .//*[contains(@data-testid,'company-name')]")?.InnerText?.Trim();
                var jobLocation = element.SelectSingleNode(".//*[contains(@class,'companyLocation')] | .//*[contains(@data-testid,'text-location')]")?.InnerText?.Trim();
                var link = element.SelectSingleNode(".//h2//a | .//*[contains(@class,'jobTitle')]//a")?.GetAttributeValue("href", "") ?? "";
                if (!string.IsNullOrEmpty(link) && !link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    link = $"https://br.indeed.com{link}";
                }

                jobs.Add(new JobListing
                {
                    Title = HtmlEntity.DeEntitize(title),
                    Company = string.IsNullOrWhiteSpace(company) ? "Não informado" : HtmlEntity.DeEntitize(company),
                    Location = string.IsNullOrWhiteSpace(jobLocation) ? location : HtmlEntity.DeEntitize(jobLocation),
                    Url = string.IsNullOrEmpty(link) ? searchUrl : link,
                    Site = "Indeed"
                });
            }

            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scraping Indeed falhou para {Query}", query);
            return new List<JobListing>();
        }
    }

    public async Task<List<JobListing>> SearchCathoAsync(
        IReadOnlyList<string> searchTerms,
        string location = "Brasil",
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var query = string.Join(' ', searchTerms);
        var searchUrl = $"https://www.catho.com.br/vagas/?q={Uri.EscapeDataString(query)}&localizacao={Uri.EscapeDataString(location)}";

        try
        {
            var html = await FetchHtmlAsync(searchUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var jobs = new List<JobListing>();

            var nodes = doc.DocumentNode.SelectNodes(
                "//*[contains(@class,'job-card')] | //*[contains(@class,'vaga-item')] | //article[contains(@class,'sc-')]");
            if (nodes == null)
            {
                return jobs;
            }

            foreach (var element in nodes.Take(maxResults))
            {
                var title = element.SelectSingleNode(".//h2 | .//h3 | .//*[contains(@class,'job-title')] | .//*[contains(@class,'vaga-titulo')]")?.InnerText?.Trim();
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                var company = element.SelectSingleNode(".//*[contains(@class,'company')] | .//*[contains(@class,'empresa')]")?.InnerText?.Trim();
                var jobLocation = element.SelectSingleNode(".//*[contains(@class,'location')] | .//*[contains(@class,'localizacao')]")?.InnerText?.Trim();
                var link = element.SelectSingleNode(".//a")?.GetAttributeValue("href", "") ?? "";
                if (!string.IsNullOrEmpty(link) && !link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    link = $"https://www.catho.com.br{link}";
                }

                jobs.Add(new JobListing
                {
                    Title = HtmlEntity.DeEntitize(title),
                    Company = string.IsNullOrWhiteSpace(company) ? "Não informado" : HtmlEntity.DeEntitize(company),
                    Location = string.IsNullOrWhiteSpace(jobLocation) ? location : HtmlEntity.DeEntitize(jobLocation),
                    Url = string.IsNullOrEmpty(link) ? searchUrl : link,
                    Site = "Catho"
                });
            }

            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scraping Catho falhou para {Query}", query);
            return new List<JobListing>();
        }
    }

    public async Task EnrichJobDetailsAsync(JobListing job, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(job.Url) || job.Url.StartsWith("https://www.google.com", StringComparison.OrdinalIgnoreCase))
        {
            FinalizeContactHints(job);
            return;
        }

        if (!string.IsNullOrWhiteSpace(job.Description) && job.Description.Length > 200)
        {
            FinalizeContactHints(job);
            return;
        }

        try
        {
            var siteName = job.Site ?? "";
            var html = await FetchHtmlAsync(job.Url, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            if (siteName.Contains("catho", StringComparison.OrdinalIgnoreCase))
            {
                job.Description ??= doc.DocumentNode.SelectSingleNode(
                    "//*[contains(@class,'job-description')] | //*[contains(@class,'descricao-vaga')] | //*[contains(@class,'Description')]")?.InnerText?.Trim();
                job.Salary ??= doc.DocumentNode.SelectSingleNode(
                    "//*[contains(@class,'salary')] | //*[contains(@class,'salario')]")?.InnerText?.Trim();
            }
            else if (siteName.Contains("indeed", StringComparison.OrdinalIgnoreCase))
            {
                job.Description ??= doc.DocumentNode.SelectSingleNode(
                    "//*[@id='jobDescriptionText'] | //*[contains(@class,'jobsearch-jobDescriptionText')]")?.InnerText?.Trim();
                job.Salary ??= doc.DocumentNode.SelectSingleNode("//*[contains(@class,'salaryText')]")?.InnerText?.Trim();
            }
            else
            {
                job.Description ??= doc.DocumentNode.SelectSingleNode(
                    "//*[contains(@class,'description')] | //article")?.InnerText?.Trim();
            }

            if (job.Description != null)
            {
                job.Description = HtmlEntity.DeEntitize(job.Description);
                if (job.Description.Length > 4000)
                {
                    job.Description = job.Description[..4000] + "…";
                }
            }

            job.Requirements ??= new List<string>();
            foreach (var li in doc.DocumentNode.SelectNodes("//ul//li | //ol//li") ?? Enumerable.Empty<HtmlNode>())
            {
                var text = li.InnerText.Trim();
                if (text.Length < 15 || text.Length > 500)
                {
                    continue;
                }

                var lower = text.ToLowerInvariant();
                if (lower.Contains("requisito") || lower.Contains("exigência") || lower.Contains("necessário") ||
                    lower.Contains("desejável") || lower.Contains("obrigatório"))
                {
                    job.Requirements.Add(HtmlEntity.DeEntitize(text));
                }

                if (job.Requirements.Count >= 8)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Não foi possível enriquecer vaga {Url}", job.Url);
        }

        FinalizeContactHints(job);
    }

    private static void FinalizeContactHints(JobListing job)
    {
        var fromText = JobContactExtractor.ExtractFromText(
            $"{job.Description} {job.Title} {job.Company}");
        job.ContactHints = fromText.Count > 0 ? fromText : job.ContactHints;

        if (job.ApplyChannels == null || job.ApplyChannels.Count == 0)
        {
            job.ApplyChannels = new List<JobApplyChannelDto>
            {
                new()
                {
                    Portal = job.Site ?? "Portal de emprego",
                    Link = job.Url
                }
            };
        }
    }

    private async Task<string> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "pt-BR,pt;q=0.9");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(12));
        var response = await client.GetAsync(url, cts.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cts.Token);
    }
}
