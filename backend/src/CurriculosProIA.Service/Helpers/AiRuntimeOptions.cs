using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CurriculosProIA.Service.Helpers;

public static class AiRuntimeOptions
{
    /// <summary>
    /// Em Production o mock nunca é usado, mesmo com USE_MOCK_AI=true no .env.
    /// </summary>
    public static bool UseMockAi(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        if (hostEnvironment.IsProduction())
        {
            return false;
        }

        return configuration["USE_MOCK_AI"] is "true" or "1";
    }
}
