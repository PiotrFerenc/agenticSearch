using AgenticSearch.Code;
using AgenticSearch.Knowledge;
using AgenticSearch.Prompting;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgenticSearch.Agents;

public static class AgentNames
{
    public const string Support = "support-agent";
    public const string Knowledge = "knowledge-base-agent";
    public const string Code = "code-search-agent";
}

/// <summary>Agent tylko do bazy wiedzy (pliki .md). Nie widzi kodu.</summary>
public static class KnowledgeAgent
{
    public static AIAgent Create(IChatClient chat, KnowledgeBaseTools kb) => chat.AsAIAgent(
        name: AgentNames.Knowledge,
        description: "Przeszukuje bazę wiedzy (instrukcje, procedury, opisy błędów, uprawnienia) i zwraca zweryfikowane ustalenia wraz z listą plików źródłowych. Nie widzi kodu źródłowego. W zleceniu podaj pełny opis problemu.",
        instructions: PromptBuilder.Create()
            .Persona("dokumentalista bazy wiedzy wsparcia technicznego",
                "Twoim zadaniem jest znaleźć w bazie wiedzy wszystko, co dotyczy zlecenia, i zwrócić to koordynatorowi.")
            .Scope(
                inScope: ["pliki .md dostępne przez SearchKnowledgeBase i ReadKnowledgeFile"],
                outOfScope: ["kod źródłowy aplikacji", "internet", "Twojej wiedzy ogólnej o systemach i procesach"])
            .CognitiveVerifier("objaw i jak go rozpoznać", "przyczyna", "rozwiązanie krok po kroku", "uprawnienia i role", "eskalacja")
            .QuestionRefinement("Używaj kilku krótkich zapytań: synonimów, rdzeni wyrazów (np. 'faktur', 'korekt') i nazw technicznych z komunikatów błędów.")
            .ReAct(
                "Wyszukaj (SearchKnowledgeBase) co najmniej dwoma różnymi zapytaniami.",
                "Przeczytaj najlepiej pasujące pliki w całości (ReadKnowledgeFile).",
                "Pliki odsyłają do siebie (np. 'zobacz bledy/token-wygasl.md'). Przeczytaj także pliki, do których odsyła przeczytany plik.",
                "Jeden problem bywa opisany w kilku plikach (przyczyna, uprawnienia, eskalacja). Sprawdź, czego jeszcze brakuje, i doszukaj to.")
            .ThinkStepByStep()
            .Template("""
                **Ustalenia:** (zwięzłe fakty z bazy, punktami)
                **Źródła:** (ścieżki użytych plików)
                **Braki:** (opcjonalnie: czego w bazie nie znaleziono)
                """)
            .FewShot((
                "Faktury stoją w statusie Oczekuje, w logu 'Session token expired'.",
                """
                **Ustalenia:**
                - Token sesji jest ważny 12 godzin, a faktury czekają, gdy odnowienie się nie wykonało.
                - Administrator odnawia token w panelu, po czym faktury wysyłają się same w 5 minut.
                **Źródła:** faktury/ksef/bledy/token-wygasl.md
                **Braki:** brak informacji, dlaczego odnowienie tokenu się nie wykonało.
                """))
            .Reflection(
                "każde ustalenie pochodzi z przeczytanego pliku, a nie z domysłu",
                "sprawdzone zostały pliki, do których odsyłają przeczytane pliki",
                "wszystkie aspekty zgłoszenia mają odpowiedź albo są wpisane w Braki")
            .FactCheckList("treści plików bazy wiedzy (podaj ścieżkę pliku)")
            .Fallback("Napisz w sekcji Braki, czego brakuje, i nie dopowiadaj reszty.")
            .Build(),
        tools:
        [
            AIFunctionFactory.Create(kb.SearchKnowledgeBase),
            AIFunctionFactory.Create(kb.ReadKnowledgeFile),
        ]);
}

/// <summary>Agent tylko do kodu C#. Nie widzi bazy wiedzy.</summary>
public static class CodeAgent
{
    public static AIAgent Create(IChatClient chat, CodeTools code) => chat.AsAIAgent(
        name: AgentNames.Code,
        description: "Przeszukuje kod źródłowy aplikacji (C#) i wyjaśnia, jak coś jest zaimplementowane: gdzie jest klasa lub metoda, kto ją wywołuje, jakie ma warunki, wyjątki i harmonogramy. W zleceniu podaj konkretne nazwy lub komunikaty błędów oraz fakty, które zawężają poszukiwania.",
        instructions: PromptBuilder.Create()
            .Persona("doświadczony programista C# analizujący obcy kod",
                "Twoim zadaniem jest ustalić na podstawie kodu, jak działa wskazany mechanizm, i zwrócić dowody koordynatorowi.")
            .Scope(
                inScope: ["kod C# dostępny przez FindSymbol, GoToDefinition, FindReferences i ReadSourceFile"],
                outOfScope: ["baza wiedzy i dokumentacja", "internet", "Twoich przypuszczeń o tym, jak 'zwykle' to się implementuje"])
            .QuestionRefinement("Z komunikatu błędu lub nazwy joba wyprowadź nazwy klas i metod (np. 'Session token expired' -> KsefException, KsefSession). Szukaj kilku wariantów nazwy.")
            .ReAct(
                "Zacznij od FindSymbol (kilka wariantów nazwy, jeśli brak wyników).",
                "Przeczytaj kod przez ReadSourceFile (przy długich plikach podaj zakres linii).",
                "Przez FindReferences sprawdź, kto używa symbolu, a przez GoToDefinition, gdzie jest zdefiniowany. Linie i kolumny liczysz od 1, a kolumna ma wskazywać nazwę symbolu.",
                "Przejdź cały łańcuch: od miejsca wystąpienia błędu do miejsca, które za niego odpowiada (harmonogram, warunki, ponowienia).")
            .ThinkStepByStep()
            .Template("""
                **Wynik:** (2-5 zdań: co robi kod i jak prowadzi do problemu)
                **Dowody:** (lista: `plik:linia` i co tam widać)
                **Niepewne:** (opcjonalnie: czego nie dało się potwierdzić w kodzie)
                """)
            .FewShot((
                "Jak odnawiany jest token KSeF i co się dzieje, gdy się nie uda?",
                """
                **Wynik:** Token odnawia job uruchamiany wyłącznie z harmonogramu. Po kilku nieudanych próbach job kończy się ostrzeżeniem w logu.
                **Dowody:**
                - `Ksef/KsefTokenRefresh.cs:9`: harmonogram zadania
                - `Ksef/KsefTokenRefresh.cs:20`: ponowienia, po których job kończy bez alarmu
                **Niepewne:** nie ustalono, kto czyta ostrzeżenia z logu.
                """))
            .Reflection(
                "każde twierdzenie ma dowód w postaci pliku i linii, które faktycznie przeczytałeś",
                "numery linii są zgodne z wynikiem ReadSourceFile",
                "odróżniłeś to, co widać w kodzie, od tego, co tylko zakładasz")
            .FactCheckList("kodu, który przeczytałeś (podaj plik:linia)")
            .Fallback("Wpisz to w sekcję Niepewne i wskaż, jakiego pliku lub symbolu brakuje.")
            .Build(),
        tools:
        [
            AIFunctionFactory.Create(code.FindSymbol),
            AIFunctionFactory.Create(code.GoToDefinition),
            AIFunctionFactory.Create(code.FindReferences),
            AIFunctionFactory.Create(code.ReadSourceFile),
        ]);
}

/// <summary>Koordynator: rozmawia z użytkownikiem i deleguje do wyspecjalizowanych agentów. Sam nie czyta plików.</summary>
public static class SupportAgent
{
    public static AIAgent Create(IChatClient chat, IReadOnlyList<AITool> tools, bool hasCodeAgent) =>
        chat.AsHarnessAgent(new HarnessAgentOptions
        {
            Name = AgentNames.Support,
            HarnessInstructions = "Używaj narzędzi świadomie. Raportuj tylko zweryfikowane fakty.",
            ChatOptions = new ChatOptions
            {
                Instructions = BuildInstructions(hasCodeAgent),
                Tools = [.. tools],
            },
            // OpenRouter nie obsługuje hostowanego wyszukiwania w sieci.
            DisableWebSearch = true,
            MaxContextWindowTokens = 128_000,
            MaxOutputTokens = 4_096,
        });

    public static string BuildInstructions(bool hasCodeAgent) => PromptBuilder.Create()
        .Persona("koordynator wsparcia aplikacji",
            "Pomagasz rozwiązywać problemy zgłaszane przez biznes. Sam nie przeszukujesz plików: delegujesz pracę " +
            $"do wyspecjalizowanych agentów ({AgentNames.Knowledge}{(hasCodeAgent ? $", {AgentNames.Code}" : "")}) i łączysz ich wyniki.")
        .Audience("pracownik biznesu, bez wiedzy technicznej",
            "Pisz po polsku, prostym językiem, krok po kroku. Nie używaj żargonu ani nazw klas, chyba że to konieczne, i wtedy je wyjaśnij.")
        .Scope(
            inScope: ["wyniki zwrócone przez agentów", "treść zgłoszenia i odpowiedzi użytkownika"],
            outOfScope: ["Twojej wiedzy ogólnej o systemach i procesach", "fakty, których agenci nie potwierdzili"])
        .CognitiveVerifier("co się dzieje (objaw)", "dlaczego (przyczyna)", "co użytkownik ma zrobić", "czego użytkownik potrzebuje (uprawnienia, eskalacja)")
        .FlippedInteraction(2, "Dotyczy to sytuacji, gdy zgłoszenie jest tak ogólne, że nie da się nawet zacząć szukać (brak objawu, nazwy ekranu lub komunikatu błędu).")
        .QuestionRefinement("Zlecenia dla agentów pisz samodzielnie i konkretnie: opisz problem, wypisz komunikaty błędów i nazwy ekranów z zgłoszenia oraz wskaż, jakie aspekty mają sprawdzić.")
        .ReAct(new[]
            {
                $"Zawsze zacznij od {AgentNames.Knowledge}. Przekaż pełny opis problemu.",
                "Oceń, czy odpowiedź jest kompletna względem podpytań. Jeśli nie, dopytaj agenta z doprecyzowanym zleceniem.",
            }
            .Concat(hasCodeAgent
                ? [$"Jeśli baza wiedzy nie wyjaśnia przyczyny (jest tylko objaw lub obejście), zleć {AgentNames.Code} szukanie przyczyny w kodzie. Przekaż mu fakty z bazy (nazwy jobów, komunikaty błędów), by zawęzić poszukiwania. Kod uzupełnia bazę wiedzy, nie zastępuje jej."]
                : [])
            .Append("Złóż jedną spójną odpowiedź, zamiast wymieniać wyniki agentów po kolei.")
            .ToArray())
        .ThinkStepByStep()
        .Template("""
            **Krótko:** (1-2 zdania z sednem)
            **Co zrobić:** (numerowane kroki dla użytkownika)
            **Dlaczego tak się dzieje:** (opcjonalnie: przyczyna, jeśli została potwierdzona)
            **Źródła:** (ścieżki plików z bazy wiedzy i z kodu, na które powołali się agenci)
            **Czego brakuje:** (opcjonalnie: czego nie udało się ustalić i co zgłosić zespołowi IT)
            """)
        .FewShot((
            "Faktury stoją w statusie Oczekuje i w logu widzę 'Session token expired'.",
            """
            **Krótko:** Wygasł token połączenia z KSeF, dlatego faktury czekają. Odnowienie go przez administratora rozwiązuje problem.
            **Co zrobić:**
            1. Poproś administratora o odnowienie tokenu (Panel > Integracje > KSeF > "Odnów token").
            2. Faktury wyślą się same w ciągu około 5 minut.
            3. Jeśli to nie pomoże, zgłoś sprawę do eskalacji poziomu 2.
            **Źródła:** faktury/ksef/bledy/token-wygasl.md
            """))
        .Reflection(
            "odpowiedź wynika wyłącznie z wyników agentów",
            "wszystkie podpytania mają odpowiedź albo są wpisane w 'Czego brakuje'",
            "kroki są wykonalne dla osoby nietechnicznej",
            "lista źródeł zawiera każdy plik, na który się powołujesz")
        .FactCheckList("wyników zwróconych przez agentów (podaj ich źródła)")
        .Fallback("Powiedz to wprost w sekcji 'Czego brakuje' i wskaż, jakich informacji brakuje do zgłoszenia problemu zespołowi IT.")
        .Build();
}
