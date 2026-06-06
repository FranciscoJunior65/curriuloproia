using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Service.Interfaces;
using edge_tts_net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class SimliService : ISimliService
{
    private const string DefaultFaceId = "tmp9i8bbq7c";
    private const string DefaultEdgeFemaleVoice = "pt-BR-FranciscaNeural";
    private const string DefaultEdgeMaleVoice = "pt-BR-AntonioNeural";
    private static readonly Regex PlaceholderKeyRegex = new(
        @"(sua-chave|seu-|your-|placeholder|example)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SimliService> _logger;

    public SimliService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<SimliService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public SimliConfigDto GetConfig()
    {
        var apiKey = GetApiKey();
        var faceIds = BuildFaceIdMap();

        return new SimliConfigDto
        {
            Enabled = !string.IsNullOrEmpty(apiKey),
            TransportMode = (_configuration["SIMLI_TRANSPORT"] ?? "livekit").Trim().ToLowerInvariant(),
            DefaultFaceId = faceIds.GetValueOrDefault("default", DefaultFaceId),
            FaceIdsByPersona = faceIds
                .Where(kv => !string.Equals(kv.Key, "default", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)
        };
    }

    public async Task<SimliSessionResponseDto> CreateSessionAsync(
        string? faceId,
        string? personaInitials,
        CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey()
            ?? throw new InvalidOperationException("Simli não configurado. Defina SIMLI_API_KEY no .env.");

        var resolvedFaceId = ResolveFaceId(faceId, personaInitials);
        var client = _httpClientFactory.CreateClient("Simli");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.simli.ai/compose/token");
        request.Headers.Add("x-simli-api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            faceId = resolvedFaceId,
            apiVersion = "v2",
            handleSilence = false,
            maxSessionLength = GetIntConfig("SIMLI_MAX_SESSION_LENGTH", 3600),
            maxIdleTime = GetIntConfig("SIMLI_MAX_IDLE_TIME", 300),
            audioInputFormat = "pcm16"
        });

        var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Simli token falhou ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException(ParseSimliError(body) ?? "Não foi possível iniciar sessão Simli.");
        }

        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.TryGetProperty("session_token", out var tokenEl)
            ? tokenEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Resposta inválida da Simli (session_token ausente).");
        }

        return new SimliSessionResponseDto
        {
            SessionToken = token,
            FaceId = resolvedFaceId
        };
    }

    public async Task<byte[]> SynthesizeSpeechMp3Async(
        string text,
        string? voice,
        CancellationToken cancellationToken = default)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException("Texto vazio para síntese de voz.");
        }

        var provider = (_configuration["TTS_PROVIDER"] ?? "edge").Trim().ToLowerInvariant();

        if (provider == "openai" && TryGetOpenAiKey(out var openAiKey))
        {
            return await SynthesizeWithOpenAiAsync(trimmed, voice, openAiKey, cancellationToken);
        }

        if (provider != "google")
        {
            try
            {
                return await SynthesizeWithEdgeTtsAsync(trimmed, voice, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Edge TTS falhou; tentando fallback.");
            }
        }

        if (TryGetOpenAiKey(out var openAiFallback))
        {
            return await SynthesizeWithOpenAiAsync(trimmed, voice, openAiFallback, cancellationToken);
        }

        return await SynthesizeWithGoogleTranslateTtsAsync(trimmed, cancellationToken);
    }

    private async Task<byte[]> SynthesizeWithEdgeTtsAsync(
        string text,
        string? voice,
        CancellationToken cancellationToken)
    {
        var edgeVoice = ResolveEdgeVoice(voice);
        var rate = NormalizeEdgeRate(_configuration["EDGE_TTS_RATE"]);
        var pitch = (_configuration["EDGE_TTS_PITCH"] ?? "+0Hz").Trim();
        var volume = (_configuration["EDGE_TTS_VOLUME"] ?? "+0%").Trim();

        var option = new TTSOption(
            voice: edgeVoice,
            pitch: pitch,
            rate: rate,
            volume: volume);

        using var output = new MemoryStream();
        var edge = new EdgeTTSNet();

        await edge.TTS(text, meta =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (meta.Type == TTSMetadataType.Audio && meta.Data is { Length: > 0 } data)
            {
                output.Write(data, 0, data.Length);
            }
        }, option);

        if (output.Length == 0)
        {
            throw new InvalidOperationException("Edge TTS não retornou áudio.");
        }

        return output.ToArray();
    }

    private static string NormalizeEdgeRate(string? rate)
    {
        var trimmed = rate?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return "-4%";
        }

        if (trimmed.StartsWith('+') || trimmed.StartsWith('-'))
        {
            return trimmed;
        }

        return $"+{trimmed}";
    }

    private string ResolveEdgeVoice(string? voice)
    {
        var normalized = voice?.Trim().ToLowerInvariant();
        var isMale = normalized is "onyx" or "echo" or "fable" or "alloy" or "male" or "m";
        var configKey = isMale ? "EDGE_TTS_VOICE_MALE" : "EDGE_TTS_VOICE_FEMALE";
        var fallback = isMale ? DefaultEdgeMaleVoice : DefaultEdgeFemaleVoice;
        var configured = _configuration[configKey]?.Trim();
        return string.IsNullOrEmpty(configured) ? fallback : configured;
    }

    private async Task<byte[]> SynthesizeWithOpenAiAsync(
        string text,
        string? voice,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("Simli");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model = _configuration["OPENAI_TTS_MODEL"] ?? "tts-1",
            input = text,
            voice = voice ?? _configuration["OPENAI_TTS_VOICE"] ?? "nova",
            response_format = "mp3"
        });

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("OpenAI TTS falhou ({Status}): {Body}", (int)response.StatusCode, err);
            throw new InvalidOperationException("Falha ao gerar áudio com OpenAI TTS.");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<byte[]> SynthesizeWithGoogleTranslateTtsAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("Simli");
        var chunks = SplitForTts(text, 180);
        using var output = new MemoryStream();

        foreach (var chunk in chunks)
        {
            var url =
                "https://translate.google.com/translate_tts?" +
                $"ie=UTF-8&client=tw-ob&tl=pt-BR&q={Uri.EscapeDataString(chunk)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Referer", "https://translate.google.com/");

            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("Falha ao gerar áudio para avatar Simli.");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            await output.WriteAsync(bytes, cancellationToken);
        }

        return output.ToArray();
    }

    private string ResolveFaceId(string? faceId, string? personaInitials)
    {
        if (!string.IsNullOrWhiteSpace(faceId))
        {
            return faceId.Trim();
        }

        var map = BuildFaceIdMap();
        var initials = personaInitials?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(initials) && map.TryGetValue(initials, out var mapped))
        {
            return mapped;
        }

        return map.GetValueOrDefault("default", DefaultFaceId);
    }

    private Dictionary<string, string> BuildFaceIdMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = (_configuration["SIMLI_FACE_ID"] ?? DefaultFaceId).Trim()
        };

        AddFace(map, "AR", _configuration["SIMLI_FACE_ID_AR"] ?? _configuration["SIMLI_FACE_ID_FEMALE"]);
        AddFace(map, "MC", _configuration["SIMLI_FACE_ID_MC"] ?? _configuration["SIMLI_FACE_ID_FEMALE"]);
        AddFace(map, "CM", _configuration["SIMLI_FACE_ID_CM"] ?? _configuration["SIMLI_FACE_ID_MALE"]);

        return map;
    }

    private static void AddFace(Dictionary<string, string> map, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            map[key] = value.Trim();
        }
    }

    private string? GetApiKey()
    {
        var key = _configuration["SIMLI_API_KEY"]?.Trim();
        return string.IsNullOrEmpty(key) ? null : key;
    }

    private bool TryGetOpenAiKey(out string apiKey)
    {
        apiKey = _configuration["OPENAI_API_KEY"]?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(apiKey) || PlaceholderKeyRegex.IsMatch(apiKey))
        {
            apiKey = string.Empty;
            return false;
        }

        return true;
    }

    private int GetIntConfig(string key, int fallback)
    {
        return int.TryParse(_configuration[key], out var value) && value > 0 ? value : fallback;
    }

    private static List<string> SplitForTts(string text, int maxLen)
    {
        var parts = Regex.Split(text, @"(?<=[.!?…])\s+")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (parts.Count == 0)
        {
            parts = [text.Trim()];
        }

        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length > maxLen)
            {
                FlushCurrent();
                for (var i = 0; i < part.Length; i += maxLen)
                {
                    chunks.Add(part.Substring(i, Math.Min(maxLen, part.Length - i)));
                }
                continue;
            }

            if (current.Length + part.Length + 1 > maxLen)
            {
                FlushCurrent();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }
            current.Append(part);
        }

        FlushCurrent();
        return chunks.Count > 0 ? chunks : [text];

        void FlushCurrent()
        {
            if (current.Length > 0)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }
        }
    }

    private static string? ParseSimliError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.ValueKind == JsonValueKind.String
                    ? detail.GetString()
                    : detail.ToString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
