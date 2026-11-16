using FluentResults;

namespace AgenticSearch.Common;

// Typowane błędy: pozwalają odróżnić przyczyny bez parsowania komunikatów.
public sealed class EmptyQueryError(string message) : Error(message);
public sealed class NotFoundError(string message) : Error(message);
public sealed class InvalidPathError(string message) : Error(message);
public sealed class ConfigurationError(string message) : Error(message);

public static class ResultExtensions
{
    /// <summary>Zamienia wynik na tekst dla modelu. Komunikaty błędów są pisane tak, by podpowiadały agentowi kolejny krok.</summary>
    public static string ToToolText<T>(this Result<T> result, Func<T, string> format) =>
        result.IsSuccess ? format(result.Value) : string.Join(" ", result.Errors.Select(e => e.Message));
}

/// <summary>Bezpieczne rozwiązywanie ścieżek względnych (blokuje wyjście poza katalog główny).</summary>
public static class SafePath
{
    public static Result<string> Resolve(string root, string path, string extension, string place)
    {
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !full.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            return Result.Fail<string>(new InvalidPathError($"Niedozwolona ścieżka. Podaj plik {extension} z {place}."));

        return File.Exists(full)
            ? Result.Ok(full)
            : Result.Fail<string>(new NotFoundError("Plik nie istnieje. Sprawdź ścieżkę w wynikach wyszukiwania."));
    }
}
