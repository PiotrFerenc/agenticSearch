using System.ComponentModel;
using System.Text;
using AgenticSearch.Common;
using AgenticSearch.Mediator;

namespace AgenticSearch.Knowledge;

/// <summary>Narzędzia agenta bazy wiedzy. To tylko adapter: wysyła zapytanie do mediatora i formatuje wynik jako tekst dla modelu.</summary>
public sealed class KnowledgeBaseTools(IMediator mediator)
{
    private const int MaxResults = 8;

    [Description("Przeszukuje bazę wiedzy (pliki .md) po słowach kluczowych. Zwraca listę najlepiej pasujących plików z fragmentami i numerami linii. Użyj kilku krótkich zapytań i synonimów zamiast jednego długiego zdania.")]
    public async Task<string> SearchKnowledgeBase(
        [Description("Słowa kluczowe oddzielone spacjami, np. 'faktura korekta błąd'")] string query)
    {
        var result = await mediator.Send(new SearchKnowledgeQuery(query));
        return result.ToToolText(hits =>
        {
            var sb = new StringBuilder();
            foreach (var h in hits.Take(MaxResults))
            {
                sb.AppendLine($"- {h.File} (trafność {h.Score})");
                foreach (var s in h.Snippets) sb.AppendLine($"  {s}");
            }
            sb.AppendLine("Użyj ReadKnowledgeFile, aby przeczytać cały plik.");
            return sb.ToString();
        });
    }

    [Description("Zwraca pełną treść jednego pliku .md z bazy wiedzy. Ścieżka jest względna, tak jak w wynikach SearchKnowledgeBase.")]
    public async Task<string> ReadKnowledgeFile(
        [Description("Ścieżka względna, np. 'faktury/korekta.md'")] string path)
    {
        var result = await mediator.Send(new ReadKnowledgeFileQuery(path));
        return result.ToToolText(text => text);
    }
}
