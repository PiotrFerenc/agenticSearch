using AgenticSearch.Common;
using FluentResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgenticSearch.Configuration;

public sealed record KnowledgeBaseOptions(string Root);
public sealed record CodeOptions(string Root);

/// <summary>
/// Ustawienia aplikacji. Źródła, od najsłabszego do najsilniejszego:
///   1. appsettings.json                 (domyślne, w repo)
///   2. appsettings.local.json           (lokalne, poza gitem; tu można trzymać ApiKey)
///   3. zmienne środowiskowe             (OPENROUTER_API_KEY, OPENROUTER_MODEL, KNOWLEDGE_BASE_DIR, CODE_DIR, LOG_LEVEL)
/// Ścieżki względne są liczone od katalogu roboczego. Pusta KnowledgeBaseDir oznacza szukanie ./knowledge-base w górę drzewa,
/// a pusta CodeDir wyłącza agenta przeszukującego kod.
/// </summary>
public sealed record AppSettings(
    string ApiKey, string Model, LogLevel LogLevel, KnowledgeBaseOptions KnowledgeBase, CodeOptions? Code)
{
    public const string Section = "AgenticSearch";

    private static readonly (string Env, string Key)[] EnvironmentOverrides =
    [
        ("OPENROUTER_API_KEY", "ApiKey"),
        ("OPENROUTER_MODEL", "Model"),
        ("KNOWLEDGE_BASE_DIR", "KnowledgeBaseDir"),
        ("CODE_DIR", "CodeDir"),
        ("LOG_LEVEL", "LogLevel"),
    ];

    public static Result<AppSettings> Load()
    {
        var overrides = EnvironmentOverrides
            .Select(o => (o.Key, Value: Environment.GetEnvironmentVariable(o.Env)))
            .Where(o => !string.IsNullOrWhiteSpace(o.Value))
            .Select(o => KeyValuePair.Create<string, string?>($"{Section}:{o.Key}", o.Value));

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddInMemoryCollection(overrides)
            .Build()
            .GetSection(Section);

        return From(config);
    }

    public static Result<AppSettings> From(IConfiguration config)
    {
        var errors = new List<IError>();

        var apiKey = config["ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            errors.Add(new ConfigurationError("Brak klucza API. Ustaw zmienną OPENROUTER_API_KEY albo ApiKey w appsettings.local.json."));

        var model = Fallback(config["Model"], "nvidia/nemotron-3.5-lightning:free");

        var logLevel = LogLevel.Information;
        if (!string.IsNullOrWhiteSpace(config["LogLevel"]) && !Enum.TryParse(config["LogLevel"], true, out logLevel))
            errors.Add(new ConfigurationError($"Nieprawidłowy LogLevel: {config["LogLevel"]}."));

        var kbDir = string.IsNullOrWhiteSpace(config["KnowledgeBaseDir"]) ? FindKnowledgeBase() : config["KnowledgeBaseDir"]!;
        if (!Directory.Exists(kbDir))
            errors.Add(new ConfigurationError($"Nie znaleziono bazy wiedzy: {kbDir}"));

        CodeOptions? code = null;
        var codeDir = config["CodeDir"];
        if (!string.IsNullOrWhiteSpace(codeDir))
        {
            if (Directory.Exists(codeDir)) code = new CodeOptions(Path.GetFullPath(codeDir));
            else errors.Add(new ConfigurationError($"Nie znaleziono katalogu kodu: {codeDir}"));
        }

        return errors.Count > 0
            ? Result.Fail<AppSettings>(errors)
            : Result.Ok(new AppSettings(apiKey!, model, logLevel, new KnowledgeBaseOptions(Path.GetFullPath(kbDir)), code));
    }

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string FindKnowledgeBase()
    {
        // Szukaj ./knowledge-base w górę od katalogu roboczego i katalogu aplikacji.
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
            {
                var candidate = Path.Combine(d.FullName, "knowledge-base");
                if (Directory.Exists(candidate)) return candidate;
            }
        return Path.Combine(Directory.GetCurrentDirectory(), "knowledge-base");
    }
}
