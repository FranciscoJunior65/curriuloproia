using CurriculosProIA.Service.Implementations;
using CurriculosProIA.Service.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CurriculosProIA.Service.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddHttpClient("Gemini");
        services.AddHttpClient("Groq");
        services.AddHttpClient("MercadoPago");
        services.AddHttpClient("Cakto");
        services.AddHttpClient("Kiwify");
        services.AddHttpClient("Simli");

        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IEmailService, EmailService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<IPaymentCheckoutService, PaymentCheckoutService>();
        services.AddScoped<IPaymentFulfillmentService, PaymentFulfillmentService>();
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<IMercadoPagoService, MercadoPagoService>();
        services.AddScoped<ICaktoService, CaktoService>();
        services.AddScoped<IKiwifyService, KiwifyService>();
        services.AddScoped<IPaymentProviderService, PaymentProviderService>();
        services.AddScoped<IJobSitesService, JobSitesService>();
        services.AddScoped<IResumeKeywordService, ResumeKeywordService>();
        services.AddScoped<IResumeGeneratorService, ResumeGeneratorService>();
        services.AddScoped<ICoverLetterService, CoverLetterService>();
        services.AddScoped<IJobBoardScraperService, JobBoardScraperService>();
        services.AddScoped<IGoogleJobsSearchService, GoogleJobsSearchService>();
        services.AddScoped<IJobSearchService, JobSearchService>();
        services.AddScoped<IInterviewSimulationService, InterviewSimulationService>();
        services.AddScoped<IVoiceInterviewService, VoiceInterviewService>();
        services.AddScoped<IStructuredInterviewService, StructuredInterviewService>();
        services.AddScoped<IInterviewConfigService, InterviewConfigService>();
        services.AddScoped<ISimliService, SimliService>();

        return services;
    }
}
