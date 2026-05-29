using System.Text.Json;
using System.Text.Json.Serialization;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Service.Helpers;
using CurriculosProIA.Service.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class GoogleJobsSearchService : IGoogleJobsSearchService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJobBoardScraperService _scraper;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleJobsSearchService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GoogleJobsSearchService(
        IHttpClientFactory httpClientFactory,
        IJobBoardScraperService scraper,
        IConfiguration configuration,
        ILogger<GoogleJobsSearchService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scraper = scraper;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<JobSearchResult> SearchAsync(
        IReadOnlyList<string> searchTerms,
        string location = "Brasil",
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var query = string.Join(' ', searchTerms.Where(t => !string.IsNullOrWhiteSpace(t))).Trim();
        if (string.IsNullOrEmpty(query))
        {
            query = "vagas emprego";
        }

        var allJobs = new List<JobListing>();
        var apiKey = _configuration["SERPAPI_KEY"]?.Trim();

        if (!string.IsNullOrEmpty(apiKey))
        {
            try
            {
                var fromGoogle = await FetchFromSerpApiAsync(query, location, maxResults, apiKey, cancellationToken);
                allJobs.AddRange(fromGoogle);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SerpAPI Google Jobs falhou para query: {Query}", query);
            }
        }

        if (allJobs.Count < maxResults)
        {
            var remaining = maxResults - allJobs.Count;
            var fromIndeed = await _scraper.SearchIndeedAsync(searchTerms, location, Math.Min(10, remaining), cancellationToken);
            MergeJobs(allJobs, fromIndeed);

            if (allJobs.Count < maxResults)
            {
                remaining = maxResults - allJobs.Count;
                var fromCatho = await _scraper.SearchCathoAsync(searchTerms, location, Math.Min(10, remaining), cancellationToken);
                MergeJobs(allJobs, fromCatho);
            }
        }

        var enrichLimit = Math.Min(allJobs.Count, 12);
        for (var i = 0; i < enrichLimit; i++)
        {
            await _scraper.EnrichJobDetailsAsync(allJobs[i], cancellationToken);
        }

        if (allJobs.Count == 0)
        {
            return new JobSearchResult
            {
                Site = "Vagas agregadas",
                Url = string.Empty,
                Jobs = new List<JobListing>(),
                Message = string.IsNullOrEmpty(apiKey)
                    ? "Nenhuma vaga encontrada nesta busca. Configure SERPAPI_KEY no servidor para ampliar resultados do Google, ou tente novamente em instantes."
                    : "Nenhuma vaga encontrada para este perfil. Tente ajustar o currículo ou buscar novamente.",
                SearchTerms = searchTerms.ToList()
            };
        }

        return new JobSearchResult
        {
            Site = "Vagas agregadas",
            Url = string.Empty,
            Jobs = allJobs.Take(maxResults).ToList(),
            Message = $"{allJobs.Count} vagas encontradas (Google e portais de emprego)",
            SearchTerms = searchTerms.ToList()
        };
    }

    private static void MergeJobs(List<JobListing> target, IEnumerable<JobListing> incoming)
    {
        foreach (var job in incoming)
        {
            var duplicate = target.Any(j =>
                (!string.IsNullOrEmpty(j.Url) && j.Url == job.Url) ||
                (j.Title == job.Title && j.Company == job.Company));
            if (!duplicate)
            {
                target.Add(job);
            }
        }
    }

    private async Task<List<JobListing>> FetchFromSerpApiAsync(
        string query,
        string location,
        int maxResults,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var googleLocation = MapLocationForSerpApi(location);
        var url =
            "https://serpapi.com/search.json?" +
            $"engine=google_jobs&q={Uri.EscapeDataString(query)}&hl=pt&gl=br" +
            $"&location={Uri.EscapeDataString(googleLocation)}&api_key={Uri.EscapeDataString(apiKey)}";

        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<SerpApiGoogleJobsResponse>(json, JsonOptions);
        if (parsed?.JobsResults == null || parsed.JobsResults.Count == 0)
        {
            return new List<JobListing>();
        }

        var jobs = new List<JobListing>();
        foreach (var item in parsed.JobsResults.Take(maxResults))
        {
            if (string.IsNullOrWhiteSpace(item.Title))
            {
                continue;
            }

            var applyChannels = (item.ApplyOptions ?? new List<SerpApiApplyOption>())
                .Where(o => !string.IsNullOrWhiteSpace(o.Link))
                .Select(o => new JobApplyChannelDto
                {
                    Portal = string.IsNullOrWhiteSpace(o.Title) ? (item.Via ?? "Candidatura") : o.Title!.Trim(),
                    Link = o.Link!.Trim()
                })
                .ToList();

            var primaryUrl = applyChannels.FirstOrDefault()?.Link ?? item.ShareLink ?? string.Empty;

            var description = item.Description?.Trim();
            var job = new JobListing
            {
                Title = item.Title.Trim(),
                Company = string.IsNullOrWhiteSpace(item.CompanyName) ? "Não informado" : item.CompanyName.Trim(),
                Location = string.IsNullOrWhiteSpace(item.Location) ? location : item.Location.Trim(),
                Url = primaryUrl,
                Site = string.IsNullOrWhiteSpace(item.Via) ? "Google Vagas" : item.Via.Trim(),
                Description = description,
                Salary = item.DetectedExtensions?.Salary,
                PostedAt = item.DetectedExtensions?.PostedAt,
                ApplyChannels = applyChannels.Count > 0 ? applyChannels : null,
                ContactHints = JobContactExtractor.ExtractFromText(description)
            };

            if (job.ApplyChannels == null && !string.IsNullOrEmpty(primaryUrl))
            {
                job.ApplyChannels = new List<JobApplyChannelDto>
                {
                    new() { Portal = job.Site ?? "Candidatura", Link = primaryUrl }
                };
            }

            jobs.Add(job);
        }

        return jobs;
    }

    private static string MapLocationForSerpApi(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return "Brazil";
        }

        var lower = location.ToLowerInvariant();
        return lower.Contains("brasil") || lower.Contains("brazil") ? "Brazil" : location;
    }

    public static string BuildGoogleJobsUrl(string query, string location)
    {
        var q = string.IsNullOrWhiteSpace(query) ? "vagas emprego" : $"{query} vagas";
        if (!string.IsNullOrWhiteSpace(location) &&
            !location.Contains("brasil", StringComparison.OrdinalIgnoreCase))
        {
            q += $" {location}";
        }

        return $"https://www.google.com/search?q={Uri.EscapeDataString(q)}&ibp=htl;jobs&hl=pt-BR&gl=BR";
    }

    private sealed class SerpApiGoogleJobsResponse
    {
        [JsonPropertyName("jobs_results")]
        public List<SerpApiJobResult>? JobsResults { get; set; }
    }

    private sealed class SerpApiJobResult
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("company_name")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("via")]
        public string? Via { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("share_link")]
        public string? ShareLink { get; set; }

        [JsonPropertyName("apply_options")]
        public List<SerpApiApplyOption>? ApplyOptions { get; set; }

        [JsonPropertyName("detected_extensions")]
        public SerpApiDetectedExtensions? DetectedExtensions { get; set; }
    }

    private sealed class SerpApiApplyOption
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }
    }

    private sealed class SerpApiDetectedExtensions
    {
        [JsonPropertyName("salary")]
        public string? Salary { get; set; }

        [JsonPropertyName("posted_at")]
        public string? PostedAt { get; set; }
    }
}
