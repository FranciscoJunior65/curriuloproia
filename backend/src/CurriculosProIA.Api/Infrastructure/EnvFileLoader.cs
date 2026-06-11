using DotNetEnv;

namespace CurriculosProIA.Api.Infrastructure;

/// <summary>
/// Carrega variáveis do .env (pasta da API no IIS, backend/ ou backend-node/ no desenvolvimento).
/// </summary>
public static class EnvFileLoader
{
    private static readonly string[] EnvFileNames = [".env", "app.env", "production.env"];

    public static string? LoadedPath { get; private set; }
    public static IReadOnlyList<string> LastSearchedPaths { get; private set; } = Array.Empty<string>();

    /// <summary>Carrega na inicialização (antes do WebApplication.CreateBuilder).</summary>
    public static string? Load() => LoadFromRoots(CollectSearchRoots(null));

    /// <summary>Segunda tentativa com ContentRoot do IIS/Plesk.</summary>
    public static string? TryLoadContentRoot(string? contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            return LoadedPath;
        }

        return LoadFromRoots(CollectSearchRoots(contentRootPath));
    }

    public static bool HasSupabaseEnvironmentVariables() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_URL")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY"));

    private static string? LoadFromRoots(IEnumerable<string> roots)
    {
        if (!string.IsNullOrEmpty(LoadedPath) && File.Exists(LoadedPath))
        {
            return LoadedPath;
        }

        var candidates = new List<string>();
        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var dir = Path.GetFullPath(root);
            foreach (var name in EnvFileNames)
            {
                candidates.Add(Path.Combine(dir, name));
            }
        }

        candidates.AddRange(CollectRepositoryCandidates());

        LastSearchedPaths = candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();

        var found = candidates
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetPriority)
            .ThenByDescending(p => p.Length)
            .FirstOrDefault();

        if (found == null)
        {
            if (HasSupabaseEnvironmentVariables())
            {
                Console.WriteLine("✅ [env] SUPABASE_* definidas nas variáveis de ambiente do sistema (sem arquivo .env).");
                return null;
            }

            Console.WriteLine("⚠️ [env] Nenhum arquivo de ambiente encontrado.");
            Console.WriteLine("   IIS/Plesk: coloque .env ou app.env na pasta do site (mesma pasta do .dll).");
            Console.WriteLine("   Dev: backend/.env ou backend-node/.env");
            Console.WriteLine("   BaseDirectory: " + AppContext.BaseDirectory);
            Console.WriteLine("   CurrentDirectory: " + Directory.GetCurrentDirectory());
            return null;
        }

        Env.Load(found, new LoadOptions(setEnvVars: true, clobberExistingVars: false, onlyExactPath: true));
        LoadedPath = found;

        Console.WriteLine($"✅ [env] Carregado: {found}");
        if (!HasSupabaseEnvironmentVariables())
        {
            Console.WriteLine("⚠️ [env] SUPABASE_URL ou SUPABASE_SERVICE_ROLE_KEY ausentes ou vazios no arquivo.");
        }

        return found;
    }

    private static IEnumerable<string> CollectSearchRoots(string? contentRootPath)
    {
        var list = new List<string>();

        if (!string.IsNullOrWhiteSpace(contentRootPath))
        {
            list.Add(contentRootPath);
        }

        list.Add(AppContext.BaseDirectory);

        var assemblyDir = Path.GetDirectoryName(typeof(EnvFileLoader).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDir))
        {
            list.Add(assemblyDir);
        }

        list.Add(Directory.GetCurrentDirectory());

        foreach (var start in list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var dir = Path.GetFullPath(start!);
            for (var depth = 0; depth < 6; depth++)
            {
                yield return dir;
                var parent = Directory.GetParent(dir)?.FullName;
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, dir, StringComparison.Ordinal))
                {
                    break;
                }

                dir = parent;
            }
        }
    }

    private static IEnumerable<string> CollectRepositoryCandidates()
    {
        var list = new List<string>();
        var repoRoot = FindRepositoryRoot();

        if (string.IsNullOrEmpty(repoRoot))
        {
            return list;
        }

        foreach (var name in EnvFileNames)
        {
            list.Add(Path.Combine(repoRoot, "backend", name));
            list.Add(Path.Combine(repoRoot, "backend-node", name));
            list.Add(Path.Combine(repoRoot, name));
        }

        return list;
    }

    private static string? FindRepositoryRoot()
    {
        var markers = new[]
        {
            "CurriculosProIA.sln",
            "CurriculosProIA.slnx",
            Path.Combine("backend", "ENV_EXAMPLE.env"),
            Path.Combine("backend-node", "package.json")
        };

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = Path.GetFullPath(start);
            for (var depth = 0; depth < 14; depth++)
            {
                if (markers.Any(m => File.Exists(Path.Combine(dir, m))))
                {
                    if (File.Exists(Path.Combine(dir, "backend", "ENV_EXAMPLE.env")))
                    {
                        return dir;
                    }

                    if (File.Exists(Path.Combine(dir, "ENV_EXAMPLE.env")))
                    {
                        return Directory.GetParent(dir)?.FullName ?? dir;
                    }

                    return dir;
                }

                var parent = Directory.GetParent(dir)?.FullName;
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, dir, StringComparison.Ordinal))
                {
                    break;
                }

                dir = parent;
            }
        }

        return null;
    }

    private static int GetPriority(string path)
    {
        var normalized = path.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);

        if (string.Equals(fileName, ".env", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "app.env", StringComparison.OrdinalIgnoreCase))
        {
            if (!normalized.Contains("/backend/", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("/backend-node/", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
        }

        if (normalized.EndsWith("/backend/.env", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (normalized.EndsWith("/backend/app.env", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (normalized.EndsWith("/backend-node/.env", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (normalized.EndsWith("/backend-node/app.env", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return 4;
    }
}
