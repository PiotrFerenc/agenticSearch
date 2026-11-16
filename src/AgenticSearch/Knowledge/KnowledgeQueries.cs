using System.Text.RegularExpressions;
using AgenticSearch.Common;
using AgenticSearch.Configuration;
using AgenticSearch.Mediator;
using FluentResults;

namespace AgenticSearch.Knowledge;

public sealed record KnowledgeHit(string File, int Score, IReadOnlyList<string> Snippets);

public sealed record SearchKnowledgeQuery(string Query) : IQuery<IReadOnlyList<KnowledgeHit>>;

public sealed record ReadKnowledgeFileQuery(string Path) : IQuery<string>;

public sealed class SearchKnowledgeHandler(KnowledgeBaseOptions options)
    : IQueryHandler<SearchKnowledgeQuery, IReadOnlyList<KnowledgeHit>>
{
    public Task<Result<IReadOnlyList<KnowledgeHit>>> Handle(SearchKnowledgeQuery request, CancellationToken ct)
    {
        var terms = Regex.Matches(request.Query.ToLowerInvariant(), @"[\p{L}\p{N}_-]{2,}")
            .Select(m => m.Value).Distinct().ToArray();
        if (terms.Length == 0)
            return Fail(new EmptyQueryError("Puste zapytanie. Podaj słowa kluczowe."));

        var hits = new List<KnowledgeHit>();
        foreach (var file in Directory.EnumerateFiles(options.Root, "*.md", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(options.Root, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            var score = terms.Count(t => rel.Contains(t, StringComparison.OrdinalIgnoreCase)) * 5;
            var snippets = new List<string>();

            for (var i = 0; i < lines.Length; i++)
            {
                var lower = lines[i].ToLowerInvariant();
                var matched = terms.Count(lower.Contains);
                if (matched == 0) continue;
                score += matched * (lines[i].StartsWith('#') ? 3 : 1);
                if (snippets.Count < 3) snippets.Add($"L{i + 1}: {Truncate(lines[i].Trim(), 160)}");
            }

            if (score > 0) hits.Add(new KnowledgeHit(rel, score, snippets));
        }

        return hits.Count == 0
            ? Fail(new NotFoundError("Brak wyników. Spróbuj synonimów, krótszych słów lub rdzeni wyrazów (np. 'faktur' zamiast 'faktura')."))
            : Task.FromResult(Result.Ok<IReadOnlyList<KnowledgeHit>>(hits.OrderByDescending(h => h.Score).ToList()));
    }

    private static Task<Result<IReadOnlyList<KnowledgeHit>>> Fail(IError error) =>
        Task.FromResult(Result.Fail<IReadOnlyList<KnowledgeHit>>(error));

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

public sealed class ReadKnowledgeFileHandler(KnowledgeBaseOptions options) : IQueryHandler<ReadKnowledgeFileQuery, string>
{
    private const int MaxReadChars = 12_000;

    public async Task<Result<string>> Handle(ReadKnowledgeFileQuery request, CancellationToken ct)
    {
        var path = SafePath.Resolve(options.Root, request.Path, ".md", "bazy wiedzy");
        if (path.IsFailed) return path.ToResult<string>();

        var text = await File.ReadAllTextAsync(path.Value, ct);
        return Result.Ok(text.Length <= MaxReadChars
            ? text
            : text[..MaxReadChars] + "\n\n[... obcięto, plik jest dłuższy ...]");
    }
}
