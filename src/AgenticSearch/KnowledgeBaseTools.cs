using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace AgenticSearch;

/// <summary>Narzędzia agenta do pracy na bazie wiedzy zapisanej jako pliki .md.</summary>
public sealed class KnowledgeBaseTools
{
    private const int MaxResults = 8;
    private const int MaxReadChars = 12_000;

    private readonly string _root;

    public KnowledgeBaseTools(string root) => _root = Path.GetFullPath(root);

    [Description("Przeszukuje bazę wiedzy (pliki .md) po słowach kluczowych. Zwraca listę najlepiej pasujących plików z fragmentami i numerami linii. Użyj kilku krótkich zapytań i synonimów zamiast jednego długiego zdania.")]
    public string SearchKnowledgeBase(
        [Description("Słowa kluczowe oddzielone spacjami, np. 'faktura korekta błąd'")] string query)
    {
        var terms = Regex.Matches(query.ToLowerInvariant(), @"[\p{L}\p{N}_-]{2,}")
            .Select(m => m.Value).Distinct().ToArray();
        if (terms.Length == 0) return "Puste zapytanie. Podaj słowa kluczowe.";

        var hits = new List<(string File, int Score, List<string> Snippets)>();
        foreach (var file in Directory.EnumerateFiles(_root, "*.md", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(_root, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            var score = 0;
            var snippets = new List<string>();

            foreach (var t in terms.Where(t => rel.Contains(t, StringComparison.OrdinalIgnoreCase)))
                score += 5;

            for (var i = 0; i < lines.Length; i++)
            {
                var lower = lines[i].ToLowerInvariant();
                var matched = terms.Count(t => lower.Contains(t));
                if (matched == 0) continue;
                score += matched * (lines[i].StartsWith('#') ? 3 : 1);
                if (snippets.Count < 3)
                    snippets.Add($"  L{i + 1}: {Truncate(lines[i].Trim(), 160)}");
            }

            if (score > 0) hits.Add((rel, score, snippets));
        }

        if (hits.Count == 0)
            return "Brak wyników. Spróbuj synonimów, krótszych słów lub rdzeni wyrazów (np. 'faktur' zamiast 'faktura').";

        var sb = new StringBuilder();
        foreach (var h in hits.OrderByDescending(h => h.Score).Take(MaxResults))
        {
            sb.AppendLine($"- {h.File} (trafność {h.Score})");
            foreach (var s in h.Snippets) sb.AppendLine(s);
        }
        sb.AppendLine("Użyj ReadKnowledgeFile, aby przeczytać cały plik.");
        return sb.ToString();
    }

    [Description("Zwraca pełną treść jednego pliku .md z bazy wiedzy. Ścieżka jest względna, tak jak w wynikach SearchKnowledgeBase.")]
    public string ReadKnowledgeFile(
        [Description("Ścieżka względna, np. 'faktury/korekta.md'")] string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path));
        // Ochrona przed wyjściem poza katalog bazy wiedzy (../)
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !full.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return "Niedozwolona ścieżka. Podaj plik .md z bazy wiedzy.";
        if (!File.Exists(full)) return "Plik nie istnieje. Sprawdź ścieżkę w wynikach wyszukiwania.";

        var text = File.ReadAllText(full);
        return text.Length <= MaxReadChars
            ? text
            : text[..MaxReadChars] + "\n\n[... obcięto, plik jest dłuższy ...]";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
