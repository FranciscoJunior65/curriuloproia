using System.Text.Json;

using CurriculosProIA.Domain.Dtos;

using CurriculosProIA.Repository.Interfaces;

using CurriculosProIA.Service.Interfaces;



namespace CurriculosProIA.Service.Implementations;



public class InterviewConfigService : IInterviewConfigService

{

    public const string ConfigKey = "interview_config";



    private static readonly JsonSerializerOptions JsonOptions = new()

    {

        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        PropertyNameCaseInsensitive = true

    };



    private readonly IAppSettingsRepository _settings;

    private InterviewConfigDto? _cached;



    public InterviewConfigService(IAppSettingsRepository settings) => _settings = settings;



    public async Task<InterviewConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)

    {

        if (_cached != null)

        {

            return _cached;

        }



        var json = await _settings.GetAppConfigValueAsync(ConfigKey, cancellationToken);

        if (!string.IsNullOrWhiteSpace(json))

        {

            try

            {

                _cached = JsonSerializer.Deserialize<InterviewConfigDto>(json, JsonOptions) ?? CreateDefault();

                return Normalize(_cached);

            }

            catch

            {

                // fallback

            }

        }



        _cached = CreateDefault();

        return _cached;

    }



    public async Task<InterviewConfigDto> SaveConfigAsync(

        InterviewConfigDto config,

        CancellationToken cancellationToken = default)

    {

        var normalized = Normalize(config);

        var json = JsonSerializer.Serialize(normalized, JsonOptions);

        await _settings.SetAppConfigValueAsync(ConfigKey, json, cancellationToken);

        _cached = normalized;

        return normalized;

    }



    public void ClearCache() => _cached = null;



    private static InterviewConfigDto CreateDefault() => new()

    {

        IntroductionPrompt = InterviewConfigDto.DefaultIntroductionPrompt,

        QuestionsPrompt = InterviewConfigDto.DefaultWrittenQuestionsPrompt,

        FeedbackPrompt = InterviewConfigDto.DefaultFeedbackPrompt,

        Phase1Minutes = 15,

        Phase2Minutes = 10,

        Phase3Minutes = 10,

        MaxVideoSpeechSeconds = 300,

        MaxSegmentSeconds = 45,

        IntroMaxSeconds = 22

    };



    private static string NormalizeQuestionsPrompt(string? prompt)

    {

        var trimmed = prompt?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed))

        {

            return InterviewConfigDto.DefaultWrittenQuestionsPrompt;

        }



        if (trimmed.Contains("acabou de falar", StringComparison.OrdinalIgnoreCase)

            || trimmed.Contains("baseadas no que ele disse e no currículo", StringComparison.OrdinalIgnoreCase)

            || trimmed.Contains("EXATAMENTE 2 perguntas", StringComparison.OrdinalIgnoreCase))

        {

            return InterviewConfigDto.DefaultWrittenQuestionsPrompt;

        }



        return trimmed;

    }



    private static string NormalizeIntroductionPrompt(string? prompt)

    {

        var trimmed = prompt?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed))

        {

            return InterviewConfigDto.DefaultIntroductionPrompt;

        }



        if (trimmed.Contains("{candidateName}", StringComparison.OrdinalIgnoreCase)

            || trimmed.Contains("Cumprimente o candidato", StringComparison.OrdinalIgnoreCase)

            || trimmed.Contains("nome + cargo", StringComparison.OrdinalIgnoreCase)

            || trimmed.Contains("Empresa/contexto: {company}", StringComparison.Ordinal))

        {

            return InterviewConfigDto.DefaultIntroductionPrompt;

        }



        return trimmed;

    }



    private static InterviewConfigDto Normalize(InterviewConfigDto config)

    {

        return new InterviewConfigDto

        {

            IntroductionPrompt = NormalizeIntroductionPrompt(config.IntroductionPrompt),

            QuestionsPrompt = NormalizeQuestionsPrompt(config.QuestionsPrompt),

            FeedbackPrompt = string.IsNullOrWhiteSpace(config.FeedbackPrompt)

                ? InterviewConfigDto.DefaultFeedbackPrompt

                : config.FeedbackPrompt.Trim(),

            Phase1Minutes = config.Phase1Minutes is > 0 and <= 60 ? config.Phase1Minutes : 15,

            Phase2Minutes = config.Phase2Minutes is > 0 and <= 60 ? config.Phase2Minutes : 10,

            Phase3Minutes = config.Phase3Minutes is > 0 and <= 60 ? config.Phase3Minutes : 10,

            MaxVideoSpeechSeconds = config.MaxVideoSpeechSeconds is > 0 and <= 600

                ? config.MaxVideoSpeechSeconds

                : 300,

            MaxSegmentSeconds = config.MaxSegmentSeconds is > 0 and <= 120 ? config.MaxSegmentSeconds : 45,

            IntroMaxSeconds = config.IntroMaxSeconds is > 0 and <= 60 ? config.IntroMaxSeconds : 22

        };

    }

}


