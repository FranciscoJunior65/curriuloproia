using DotNetEnv;

namespace CurriculosProIA.Api.Infrastructure;

/// <summary>
/// Carrega variáveis de backend/.env — o mesmo arquivo no localhost e no servidor (IIS/Plesk).
/// </summary>
public static class EnvFileLoader
{
    private const string EnvFileName = ".env";

    public static string? LoadedPath { get; private set; }
    public static IReadOnlyList<string> LastSearchedPaths { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Carrega após <see cref="WebApplication.CreateBuilder"/> — prioriza ContentRoot (pasta do site no IIS/Plesk).
    /// </summary>
    public static void Configure(WebApplicationBuilder builder)
    {
        var roots = new List<string>();

        if (!string.IsNullOrWhiteSpace(builder.Environment.ContentRootPath))
        {
            roots.Add(builder.Environment.ContentRootPath);
        }

        roots.Add(AppContext.BaseDirectory);

        var assemblyDir = Path.GetDirectoryName(typeof(EnvFileLoader).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDir))
        {
            roots.Add(assemblyDir);
        }

        roots.Add(Directory.GetCurrentDirectory());

        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TryLoadFromDirectory(root, preferFileOverEmptyEnv: true))
            {
                break;
            }
        }

        if (LoadedPath == null)
        {
            TryLoadFromRepositoryRoots();
        }

        builder.Configuration.AddEnvironmentVariables();

        LogStartupStatus();
    }

    public static bool HasSupabaseEnvironmentVariables() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_URL")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY"));

    public static object GetDiagnostics(string? contentRoot = null) => new
    {
        loadedPath = LoadedPath,
        loadedFileExists = LoadedPath != null && File.Exists(LoadedPath),
        contentRoot = contentRoot ?? Directory.GetCurrentDirectory(),
        appBaseDirectory = AppContext.BaseDirectory,
        hasSupabase = HasSupabaseEnvironmentVariables(),
        hasMercadoPagoTestToken = HasNonEmptyEnv("MERCADOPAGO_ACCESS_TOKEN_TEST"),
        hasMercadoPagoProductionToken = HasNonEmptyEnv("MERCADOPAGO_ACCESS_TOKEN_PRODUCTION"),
        mercadoPagoMode = Environment.GetEnvironmentVariable("MERCADOPAGO_MODE") ?? "(não definido)",
        paymentProvider = Environment.GetEnvironmentVariable("PAYMENT_PROVIDER") ?? "(não definido)",
        searchedPaths = LastSearchedPaths.Take(12)
    };

    private static bool TryLoadFromDirectory(string root, bool preferFileOverEmptyEnv)
    {
        var dir = Path.GetFullPath(root);
        var path = Path.Combine(dir, EnvFileName);
        LastSearchedPaths = [path];

        if (!File.Exists(path))
        {
            return false;
        }

        ApplyEnvFile(path, preferFileOverEmptyEnv);
        LoadedPath = Path.GetFullPath(path);
        return true;
    }

    private static void TryLoadFromRepositoryRoots()
    {
        var candidates = new List<string>();
        foreach (var root in CollectSearchRoots(null))
        {
            var dir = Path.GetFullPath(root);
            candidates.Add(Path.Combine(dir, EnvFileName));
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
            return;
        }

        ApplyEnvFile(found, preferFileOverEmptyEnv: false);
        LoadedPath = found;
    }

    private static void ApplyEnvFile(string path, bool preferFileOverEmptyEnv)
    {
        if (preferFileOverEmptyEnv)
        {
            // Pasta do site (IIS/Plesk): valores do arquivo prevalecem sobre variáveis vazias do painel.
            foreach (var (key, value) in ParseEnvEntries(path))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var existing = Environment.GetEnvironmentVariable(key);
                if (string.IsNullOrWhiteSpace(existing))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }

        // DotNetEnv: não sobrescreve variáveis já definidas no sistema (Plesk com valores reais).
        Env.Load(path, new LoadOptions(setEnvVars: true, clobberExistingVars: false, onlyExactPath: true));

        // Segunda passagem: garante chaves vazias no sistema preenchidas pelo arquivo.
        if (preferFileOverEmptyEnv)
        {
            foreach (var (key, value) in ParseEnvEntries(path))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var current = Environment.GetEnvironmentVariable(key);
                if (string.IsNullOrWhiteSpace(current))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }

        Console.WriteLine($"✅ [env] Carregado: {path}");
    }

    private static IEnumerable<(string Key, string Value)> ParseEnvEntries(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                yield return (key, value);
            }
        }
    }

    private static void LogStartupStatus()
    {
        if (LoadedPath == null)
        {
            if (HasSupabaseEnvironmentVariables())
            {
                Console.WriteLine("✅ [env] Variáveis do sistema (sem arquivo .env).");
            }
            else
            {
                Console.WriteLine("⚠️ [env] backend/.env não encontrado.");
                Console.WriteLine("   Crie backend/.env (copie ENV_EXAMPLE.env) — mesmo arquivo no localhost e no servidor.");
                Console.WriteLine("   BaseDirectory: " + AppContext.BaseDirectory);
                Console.WriteLine("   CurrentDirectory: " + Directory.GetCurrentDirectory());
            }
        }

        if (!HasSupabaseEnvironmentVariables())
        {
            Console.WriteLine("⚠️ [env] SUPABASE_URL ou SUPABASE_SERVICE_ROLE_KEY ausentes.");
        }

        var mpMode = Environment.GetEnvironmentVariable("MERCADOPAGO_MODE") ?? "test";
        var mpKey = string.Equals(mpMode, "production", StringComparison.OrdinalIgnoreCase)
            ? "MERCADOPAGO_ACCESS_TOKEN_PRODUCTION"
            : "MERCADOPAGO_ACCESS_TOKEN_TEST";

        if (!HasNonEmptyEnv(mpKey) && !HasNonEmptyEnv("MERCADOPAGO_ACCESS_TOKEN"))
        {
            Console.WriteLine($"⚠️ [env] {mpKey} ausente (modo Mercado Pago: {mpMode}).");
        }
    }

    private static bool HasNonEmptyEnv(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return !string.IsNullOrWhiteSpace(value);
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

        foreach (var name in new[] { EnvFileName })
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
