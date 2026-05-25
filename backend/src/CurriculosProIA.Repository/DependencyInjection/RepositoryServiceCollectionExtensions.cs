using CurriculosProIA.Repository.Implementations;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CurriculosProIA.Repository.DependencyInjection;

public static class RepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<SupabaseService>();
        services.AddScoped<ISupabaseConnectionTester>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<IUserProfileRepository>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<IPurchaseRepository>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<ICreditRepository>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<ICouponRepository>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<ICouponAdminRepository>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<IAppSettingsRepository>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<IAnalysisRepository>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<ICurriculoRepository>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<IInterviewRepository>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<IJobSiteRepository>(sp => sp.GetRequiredService<SupabaseService>());
        services.AddScoped<IAppDataStore>(sp => sp.GetRequiredService<SupabaseService>());
        return services;
    }
}
