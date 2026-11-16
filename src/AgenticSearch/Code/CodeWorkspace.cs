using AgenticSearch.Common;
using AgenticSearch.Configuration;
using FluentResults;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace AgenticSearch.Code;

public sealed record ResolvedSymbol(ISymbol Symbol, Solution Solution);

/// <summary>
/// Solucja Roslyna dla katalogu z kodem. Ładowana leniwie przy pierwszym użyciu, w jednym egzemplarzu na aplikację.
/// Z plikiem .sln/.csproj używa MSBuild (pełne referencje), a dla luźnych plików .cs prostego projektu.
/// </summary>
public sealed class CodeWorkspace(CodeOptions options) : IDisposable
{
    private readonly Lazy<Task<Solution>> _solution = new(() => LoadAsync(options.Root));
    private static Workspace? s_workspace;

    public string Root => options.Root;

    public Task<Solution> GetSolutionAsync() => _solution.Value;

    public Result<string> ResolveSource(string path) => SafePath.Resolve(Root, path, ".cs", "katalogu kodu");

    public async Task<Result<ResolvedSymbol>> ResolveSymbolAsync(string path, int line, int column)
    {
        var full = ResolveSource(path);
        if (full.IsFailed) return full.ToResult<ResolvedSymbol>();

        var solution = await GetSolutionAsync();
        var doc = solution.Projects.SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(d.FilePath, full.Value, StringComparison.Ordinal));
        if (doc is null)
            return Result.Fail<ResolvedSymbol>(new NotFoundError("Plik nie należy do żadnego projektu w załadowanej solucji (np. jest w bin/ lub obj/)."));

        var text = await doc.GetTextAsync();
        if (line < 1 || line > text.Lines.Count)
            return Result.Fail<ResolvedSymbol>(new InvalidPathError($"Plik ma {text.Lines.Count} linii."));
        var l = text.Lines[line - 1];
        var position = l.Start + Math.Clamp(column - 1, 0, l.Span.Length);

        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(doc, position);
        return symbol is null
            ? Result.Fail<ResolvedSymbol>(new NotFoundError("Brak symbolu w tym miejscu. Kolumna ma wskazywać nazwę symbolu."))
            : Result.Ok(new ResolvedSymbol(symbol, solution));
    }

    public string Relative(Location l) => Path.GetRelativePath(Root, l.SourceTree!.FilePath).Replace('\\', '/');

    public static int LineOf(Location l) => l.GetLineSpan().StartLinePosition.Line + 1;

    private static async Task<Solution> LoadAsync(string root)
    {
        var sln = Directory.EnumerateFiles(root, "*.sln*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase));
        var projects = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f)).ToList();

        if (sln is null && projects.Count == 0) return LoadAdhoc(root);

        if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();
        return await OpenWithMsBuildAsync(sln, projects);
    }

    // Osobna metoda, żeby typy MSBuild nie były ładowane przed MSBuildLocator.RegisterDefaults().
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<Solution> OpenWithMsBuildAsync(string? sln, List<string> projects)
    {
        var ws = MSBuildWorkspace.Create();
        s_workspace = ws;
        if (sln is not null) return await ws.OpenSolutionAsync(sln);
        foreach (var p in projects) await ws.OpenProjectAsync(p);
        return ws.CurrentSolution;
    }

    private static Solution LoadAdhoc(string root)
    {
        var ws = new AdhocWorkspace();
        s_workspace = ws;
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));
        var project = ws.AddProject("code", LanguageNames.CSharp).WithMetadataReferences(refs);
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(f => !IsBuildOutput(f)))
            project = project.AddDocument(Path.GetFileName(file), File.ReadAllText(file), filePath: file).Project;
        return project.Solution;
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj");

    public void Dispose() => s_workspace?.Dispose();
}
