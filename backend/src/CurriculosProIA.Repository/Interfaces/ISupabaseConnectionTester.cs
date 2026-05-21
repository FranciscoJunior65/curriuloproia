namespace CurriculosProIA.Repository.Interfaces;

public sealed record SupabaseConnectionTestResult(
    bool Configured,
    bool Success,
    string Message,
    string? Warning = null,
    string? Error = null,
    int? ProfileCount = null);

public interface ISupabaseConnectionTester
{
    bool IsConfigured { get; }
    SupabaseConnectionTestResult GetConfigurationStatus();
    Task<SupabaseConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
