using CurriculosProIA.App.Implementations;
using CurriculosProIA.App.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CurriculosProIA.App.DependencyInjection;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAuthAppService, AuthAppService>();
        services.AddScoped<IAnalyzeAppService, AnalyzeAppService>();
        services.AddScoped<IAdminAppService, AdminAppService>();
        services.AddScoped<IPurchaseAppService, PurchaseAppService>();
        services.AddScoped<IPaymentWebhookAppService, PaymentWebhookAppService>();
        services.AddScoped<ISimliAppService, SimliAppService>();
        return services;
    }
}
