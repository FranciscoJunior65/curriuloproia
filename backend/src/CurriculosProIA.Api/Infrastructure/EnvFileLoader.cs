using DotNetEnv;

namespace CurriculosProIA.Api.Infrastructure;

/// <summary>
/// Carrega variáveis do .env (backend ou backend-node), independente do diretório de execução.
/// </summary>
public static class EnvFileLoader
{
    public static string? LoadedPath { get; private set; }
    public static IReadOnlyList<string> LastSearchedPaths { get; private set; } = Array.Empty<string>();

    public static string? Load()
    {
        if (!string.IsNullOrEmpty(LoadedPath) && File.Exists(LoadedPath))
        {
            return LoadedPath;
        }

        var candidates = CollectCandidates().ToList();
        LastSearchedPaths = candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
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

            Console.WriteLine("⚠️ [env] Nenhum .env encontrado.");
            Console.WriteLine("   Crie: backend/.env (copie de ENV_EXAMPLE.env ou do backend-node/.env)");
            Console.WriteLine("   Raiz esperada do repo: " + (FindRepositoryRoot() ?? "(não detectada)"));
            return null;
        }

        Env.Load(found, new LoadOptions(setEnvVars: true, clobberExistingVars: false, onlyExactPath: true));
        LoadedPath = found;

        Console.WriteLine($"✅ [env] .env carregado de: {found}");
        if (!HasSupabaseEnvironmentVariables())
        {
            Console.WriteLine("⚠️ [env] SUPABASE_URL ou SUPABASE_SERVICE_ROLE_KEY ausentes ou vazios no .env");
        }

        return found;
    }

    public static bool HasSupabaseEnvironmentVariables() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_URL")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY"));

    private static IEnumerable<string> CollectCandidates()
    {
        var list = new List<string>();
        var repoRoot = FindRepositoryRoot();

        if (!string.IsNullOrEmpty(repoRoot))
        {
            list.Add(Path.Combine(repoRoot, "backend", ".env"));
            list.Add(Path.Combine(repoRoot, "backend-node", ".env"));
            list.Add(Path.Combine(repoRoot, ".env"));
        }

        var starts = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(typeof(EnvFileLoader).Assembly.Location) ?? ""
        };

        foreach (var start in starts.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var dir = Path.GetFullPath(start!);
            for (var depth = 0; depth < 14; depth++)
            {
                list.Add(Path.Combine(dir, ".env"));
                list.Add(Path.Combine(dir, "backend", ".env"));
                list.Add(Path.Combine(dir, "backend-node", ".env"));

                var parent = Directory.GetParent(dir)?.FullName;
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, dir, StringComparison.Ordinal))
                {
                    break;
                }

                dir = parent;
            }
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

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
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
        if (normalized.EndsWith("/backend/.env", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (normalized.EndsWith("/backend-node/.env", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }
}
