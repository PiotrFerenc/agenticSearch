using System.Text;
using AgenticSearch.Common;
using AgenticSearch.Mediator;
using FluentResults;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AgenticSearch.Code;

public sealed record CodeSymbolInfo(string Kind, string Name, string File, int Line);
public sealed record CodeLocation(string File, int Line, string Text);

public sealed record FindSymbolQuery(string Name) : IQuery<IReadOnlyList<CodeSymbolInfo>>;
public sealed record GoToDefinitionQuery(string Path, int Line, int Column) : IQuery<IReadOnlyList<CodeLocation>>;
public sealed record FindReferencesQuery(string Path, int Line, int Column) : IQuery<IReadOnlyList<CodeLocation>>;
public sealed record ReadSourceFileQuery(string Path, int? FromLine, int? ToLine) : IQuery<string>;

public sealed class FindSymbolHandler(CodeWorkspace workspace) : IQueryHandler<FindSymbolQuery, IReadOnlyList<CodeSymbolInfo>>
{
    public async Task<Result<IReadOnlyList<CodeSymbolInfo>>> Handle(FindSymbolQuery request, CancellationToken ct)
    {
        var solution = await workspace.GetSolutionAsync();
        var found = (await SymbolFinder.FindSourceDeclarationsWithPatternAsync(solution, request.Name, SymbolFilter.TypeAndMember, ct))
            .Where(s => s.Locations.Any(l => l.IsInSource) && !s.IsImplicitlyDeclared)
            .Select(s =>
            {
                var loc = s.Locations.First(l => l.IsInSource);
                var container = s.ContainingType?.Name ?? s.ContainingNamespace?.ToDisplayString();
                var name = string.IsNullOrEmpty(container) ? s.Name : $"{container}.{s.Name}";
                return new CodeSymbolInfo(KindName(s), name, workspace.Relative(loc), CodeWorkspace.LineOf(loc));
            })
            .ToList();

        return found.Count == 0
            ? Result.Fail<IReadOnlyList<CodeSymbolInfo>>(new NotFoundError("Brak symboli. Spróbuj krótszego fragmentu nazwy lub innej formy (np. bez przedrostka)."))
            : Result.Ok<IReadOnlyList<CodeSymbolInfo>>(found);
    }

    private static string KindName(ISymbol s) => s switch
    {
        INamedTypeSymbol { TypeKind: TypeKind.Interface } => "interfejs",
        INamedTypeSymbol { TypeKind: TypeKind.Enum } => "enum",
        INamedTypeSymbol { TypeKind: TypeKind.Struct } => "struct",
        INamedTypeSymbol => "klasa",
        IMethodSymbol { MethodKind: MethodKind.Constructor } => "konstruktor",
        IMethodSymbol => "metoda",
        IPropertySymbol => "właściwość",
        IFieldSymbol => "pole",
        IEventSymbol => "zdarzenie",
        _ => "symbol",
    };
}

public sealed class GoToDefinitionHandler(CodeWorkspace workspace) : IQueryHandler<GoToDefinitionQuery, IReadOnlyList<CodeLocation>>
{
    public async Task<Result<IReadOnlyList<CodeLocation>>> Handle(GoToDefinitionQuery request, CancellationToken ct)
    {
        var resolved = await workspace.ResolveSymbolAsync(request.Path, request.Line, request.Column);
        if (resolved.IsFailed) return Result.Fail<IReadOnlyList<CodeLocation>>(resolved.Errors);

        var locations = resolved.Value.Symbol.Locations.Where(l => l.IsInSource).ToList();
        return locations.Count == 0
            ? Result.Fail<IReadOnlyList<CodeLocation>>(new NotFoundError(
                $"Symbol {resolved.Value.Symbol.ToDisplayString()} jest zdefiniowany poza kodem źródłowym (biblioteka)."))
            : Result.Ok(CodeLocations.From(workspace, locations));
    }
}

public sealed class FindReferencesHandler(CodeWorkspace workspace) : IQueryHandler<FindReferencesQuery, IReadOnlyList<CodeLocation>>
{
    public async Task<Result<IReadOnlyList<CodeLocation>>> Handle(FindReferencesQuery request, CancellationToken ct)
    {
        var resolved = await workspace.ResolveSymbolAsync(request.Path, request.Line, request.Column);
        if (resolved.IsFailed) return Result.Fail<IReadOnlyList<CodeLocation>>(resolved.Errors);

        var refs = await SymbolFinder.FindReferencesAsync(resolved.Value.Symbol, resolved.Value.Solution, ct);
        var locations = refs.SelectMany(r => r.Definition.Locations.Where(l => l.IsInSource))
            .Concat(refs.SelectMany(r => r.Locations.Select(l => l.Location)))
            .Where(l => l.IsInSource)
            .DistinctBy(l => (l.SourceTree!.FilePath, l.SourceSpan.Start))
            .OrderBy(l => l.SourceTree!.FilePath).ThenBy(l => l.SourceSpan.Start)
            .ToList();

        return locations.Count == 0
            ? Result.Fail<IReadOnlyList<CodeLocation>>(new NotFoundError("Brak użyć tego symbolu."))
            : Result.Ok(CodeLocations.From(workspace, locations));
    }
}

public sealed class ReadSourceFileHandler(CodeWorkspace workspace) : IQueryHandler<ReadSourceFileQuery, string>
{
    private const int MaxReadChars = 12_000;

    public async Task<Result<string>> Handle(ReadSourceFileQuery request, CancellationToken ct)
    {
        var path = workspace.ResolveSource(request.Path);
        if (path.IsFailed) return path.ToResult<string>();

        var lines = await File.ReadAllLinesAsync(path.Value, ct);
        var from = Math.Max(1, request.FromLine ?? 1);
        var to = Math.Min(lines.Length, request.ToLine ?? lines.Length);
        var sb = new StringBuilder();
        for (var i = from; i <= to; i++)
        {
            sb.AppendLine($"{i,4}: {lines[i - 1]}");
            if (sb.Length > MaxReadChars)
            {
                sb.AppendLine("[... obcięto, podaj węższy zakres linii ...]");
                break;
            }
        }
        return Result.Ok(sb.ToString());
    }
}

internal static class CodeLocations
{
    public static IReadOnlyList<CodeLocation> From(CodeWorkspace workspace, IEnumerable<Location> locations) =>
        locations.Select(l => new CodeLocation(
            workspace.Relative(l),
            CodeWorkspace.LineOf(l),
            l.SourceTree!.GetText().Lines[CodeWorkspace.LineOf(l) - 1].ToString().Trim())).ToList();
}
