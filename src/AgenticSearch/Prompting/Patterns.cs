using System.Text;

namespace AgenticSearch.Prompting;

/// <summary>Jeden wzorzec promptu, czyli samodzielna sekcja instrukcji z nazwą.</summary>
public interface IPromptPattern
{
    string Name { get; }
    string Render();
}

internal static class Fmt
{
    public static string Numbered(IEnumerable<string> items)
    {
        var sb = new StringBuilder();
        var i = 1;
        foreach (var item in items) sb.AppendLine($"{i++}. {item}");
        return sb.ToString().TrimEnd();
    }

    public static string Bullets(IEnumerable<string> items) =>
        string.Join("\n", items.Select(i => $"- {i}"));
}

/// <summary>Persona: model przyjmuje rolę i misję.</summary>
public sealed record PersonaPattern(string Role, string Mission) : IPromptPattern
{
    public string Name => "Rola (Persona)";
    public string Render() => $"Działaj jako {Role}. {Mission}";
}

/// <summary>Audience Persona: kto czyta odpowiedź, więc jak ma brzmieć.</summary>
public sealed record AudiencePattern(string Audience, string Style) : IPromptPattern
{
    public string Name => "Odbiorca (Audience Persona)";
    public string Render() => $"Odbiorca odpowiedzi: {Audience}. {Style}";
}

/// <summary>Context Manager: co wolno brać pod uwagę, a co jest poza zakresem.</summary>
public sealed record ContextManagerPattern(IReadOnlyList<string> InScope, IReadOnlyList<string> OutOfScope) : IPromptPattern
{
    public string Name => "Zakres (Context Manager)";
    public string Render() =>
        $"Bierz pod uwagę wyłącznie:\n{Fmt.Bullets(InScope)}\nIgnoruj i nie korzystaj z:\n{Fmt.Bullets(OutOfScope)}";
}

/// <summary>Reguły ogólne, numerowane.</summary>
public sealed record RulesPattern(IReadOnlyList<string> Rules) : IPromptPattern
{
    public string Name => "Zasady";
    public string Render() => Fmt.Numbered(Rules);
}

/// <summary>ReAct: pętla Myśl, Działanie, Obserwacja z konkretnymi krokami użycia narzędzi.</summary>
public sealed record ReActPattern(IReadOnlyList<string> Steps) : IPromptPattern
{
    public string Name => "Sposób pracy (ReAct)";
    public string Render() =>
        "Pracuj w pętli: zastanów się, co jeszcze musisz ustalić, wywołaj narzędzie, oceń wynik i dopiero wtedy " +
        "zdecyduj o następnym kroku. Nie odpowiadaj z pamięci, dopóki narzędzia nie potwierdziły faktów.\n" +
        Fmt.Numbered(Steps);
}

/// <summary>Chain of Thought w wersji ukrytej: rozumowanie krok po kroku, ale w odpowiedzi tylko wnioski.</summary>
public sealed record ThinkStepByStepPattern : IPromptPattern
{
    public string Name => "Rozumowanie (Chain of Thought)";
    public string Render() =>
        "Rozumuj krok po kroku, zanim odpowiesz. Nie wypisuj toku rozumowania, w odpowiedzi podaj tylko wnioski i dowody.";
}

/// <summary>Cognitive Verifier: rozbicie pytania na podpytania i złożenie odpowiedzi z ich wyników.</summary>
public sealed record CognitiveVerifierPattern(IReadOnlyList<string> Aspects) : IPromptPattern
{
    public string Name => "Weryfikacja poznawcza (Cognitive Verifier)";
    public string Render() =>
        "Zanim zaczniesz szukać, rozbij zgłoszenie na 2 do 4 podpytań. Typowe aspekty:\n" +
        Fmt.Bullets(Aspects) +
        "\nOdpowiedz na każde podpytanie osobno i dopiero z tych odpowiedzi złóż jedną spójną całość.";
}

/// <summary>Question Refinement: ulepszanie własnych zapytań (synonimy, nazwy techniczne) przed ich użyciem.</summary>
public sealed record QuestionRefinementPattern(string Hint) : IPromptPattern
{
    public string Name => "Doprecyzowanie zapytań (Question Refinement)";
    public string Render() =>
        "Nie używaj zgłoszenia dosłownie jako zapytania. Sformułuj własne, lepsze zapytania. " + Hint;
}

/// <summary>Flipped Interaction: gdy brakuje danych, model pyta użytkownika zamiast zgadywać.</summary>
public sealed record FlippedInteractionPattern(int MaxQuestions, string WhenToAsk) : IPromptPattern
{
    public string Name => "Pytania zwrotne (Flipped Interaction)";
    public string Render() =>
        $"{WhenToAsk} Zadaj wtedy użytkownikowi najwyżej {MaxQuestions} konkretne pytania, zakończ odpowiedź i poczekaj na " +
        "odpowiedź. W przeciwnym razie nie dopytuj, tylko działaj.";
}

/// <summary>Template: sztywny układ odpowiedzi.</summary>
public sealed record TemplatePattern(string Template) : IPromptPattern
{
    public string Name => "Format odpowiedzi (Template)";
    public string Render() =>
        "Zachowaj dokładnie ten układ odpowiedzi. Pomiń sekcję tylko wtedy, gdy jest oznaczona jako opcjonalna i nie dotyczy:\n" + Template;
}

/// <summary>Few-shot: przykłady oczekiwanego zachowania.</summary>
public sealed record FewShotPattern(IReadOnlyList<(string Input, string Output)> Examples) : IPromptPattern
{
    public string Name => "Przykłady (Few-shot)";
    public string Render()
    {
        var sb = new StringBuilder("Przykłady pokazują styl i układ, nie treść. Nie kopiuj faktów z przykładów.\n");
        var i = 1;
        foreach (var (input, output) in Examples)
            sb.AppendLine($"\nPrzykład {i++}\nWejście: {input}\nOdpowiedź:\n{output}");
        return sb.ToString().TrimEnd();
    }
}

/// <summary>Reflection: samokontrola odpowiedzi przed wysłaniem.</summary>
public sealed record ReflectionPattern(IReadOnlyList<string> Checks) : IPromptPattern
{
    public string Name => "Samokontrola (Reflection)";
    public string Render() =>
        "Przed wysłaniem odpowiedzi sprawdź po cichu poniższą listę. Jeśli coś się nie zgadza, popraw odpowiedź lub " +
        "wywołaj kolejne narzędzie. Nie pokazuj tej listy w odpowiedzi.\n" + Fmt.Bullets(Checks.Select(c => $"Czy {c}"));
}

/// <summary>Fact Check List: każde twierdzenie ma mieć źródło.</summary>
public sealed record FactCheckListPattern(string SourceKind) : IPromptPattern
{
    public string Name => "Źródła (Fact Check List)";
    public string Render() =>
        $"Każde istotne twierdzenie musi wynikać z {SourceKind} i mieć wskazane źródło. " +
        "Twierdzenia bez źródła usuń z odpowiedzi.";
}

/// <summary>Fallback / Refusal: co powiedzieć, gdy nie da się odpowiedzieć. Zakaz zgadywania.</summary>
public sealed record FallbackPattern(string WhenMissing) : IPromptPattern
{
    public string Name => "Brak danych (Fallback)";
    public string Render() =>
        $"Gdy nie znajdziesz potwierdzenia, nie zgaduj i nie uzupełniaj wiedzą ogólną. {WhenMissing}";
}
