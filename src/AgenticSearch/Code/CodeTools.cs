using System.ComponentModel;
using System.Text;
using AgenticSearch.Common;
using AgenticSearch.Mediator;

namespace AgenticSearch.Code;

/// <summary>Narzędzia agenta kodu. To tylko adapter: wysyła zapytanie do mediatora i formatuje wynik jako tekst dla modelu.</summary>
public sealed class CodeTools(IMediator mediator)
{
    private const int MaxResults = 25;

    [Description("Szuka symboli (klas, interfejsów, metod, właściwości) w kodzie C# po nazwie lub jej fragmencie. Zwraca rodzaj symbolu, plik i linię.")]
    public async Task<string> FindSymbol(
        [Description("Nazwa lub fragment nazwy, np. 'KsefTokenRefresh' albo 'Invoice'")] string name)
    {
        var result = await mediator.Send(new FindSymbolQuery(name));
        return result.ToToolText(symbols =>
        {
            var sb = new StringBuilder();
            foreach (var s in symbols.Take(MaxResults)) sb.AppendLine($"- {s.Kind} {s.Name} — {s.File}:{s.Line}");
            if (symbols.Count > MaxResults) sb.AppendLine($"... i {symbols.Count - MaxResults} kolejnych, zawęź zapytanie.");
            return sb.ToString();
        });
    }

    [Description("Znajduje definicję symbolu użytego w podanym miejscu pliku (numery linii i kolumn liczone od 1, tak jak w wynikach FindSymbol i ReadSourceFile).")]
    public async Task<string> GoToDefinition(
        [Description("Ścieżka względna pliku .cs")] string path,
        [Description("Numer linii (od 1)")] int line,
        [Description("Numer kolumny (od 1)")] int column) =>
        (await mediator.Send(new GoToDefinitionQuery(path, line, column))).ToToolText(FormatLocations);

    [Description("Znajduje wszystkie miejsca w kodzie, które używają symbolu z podanego miejsca pliku (kto wywołuje metodę, gdzie jest używana klasa).")]
    public async Task<string> FindReferences(
        [Description("Ścieżka względna pliku .cs")] string path,
        [Description("Numer linii (od 1)")] int line,
        [Description("Numer kolumny (od 1)")] int column) =>
        (await mediator.Send(new FindReferencesQuery(path, line, column))).ToToolText(FormatLocations);

    [Description("Zwraca treść pliku .cs z numerami linii. Opcjonalnie tylko zakres linii.")]
    public async Task<string> ReadSourceFile(
        [Description("Ścieżka względna, np. 'Services/KsefTokenRefresh.cs'")] string path,
        [Description("Pierwsza linia (od 1), opcjonalnie")] int? fromLine = null,
        [Description("Ostatnia linia, opcjonalnie")] int? toLine = null) =>
        (await mediator.Send(new ReadSourceFileQuery(path, fromLine, toLine))).ToToolText(text => text);

    private static string FormatLocations(IReadOnlyList<CodeLocation> locations)
    {
        var sb = new StringBuilder();
        foreach (var l in locations.Take(MaxResults)) sb.AppendLine($"- {l.File}:{l.Line}: {l.Text}");
        if (locations.Count > MaxResults) sb.AppendLine($"... i {locations.Count - MaxResults} kolejnych.");
        return sb.ToString();
    }
}
