using System.ClientModel;
using AgenticSearch;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

// Konfiguracja przez zmienne środowiskowe:
//   OPENROUTER_API_KEY  (wymagana, zwykły klucz API, nie management key)
//   OPENROUTER_MODEL    (opcjonalna, domyślnie darmowy model)
//   KNOWLEDGE_BASE_DIR  (opcjonalna, domyślnie ./knowledge-base)
var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Brak zmiennej OPENROUTER_API_KEY.");
    return 1;
}

var model = Environment.GetEnvironmentVariable("OPENROUTER_MODEL") ?? "nvidia/nemotron-3.5-lightning:free";
var kbDir = Environment.GetEnvironmentVariable("KNOWLEDGE_BASE_DIR") ?? FindKnowledgeBase();
if (!Directory.Exists(kbDir))
{
    Console.Error.WriteLine($"Nie znaleziono bazy wiedzy: {kbDir}");
    return 1;
}

// OpenRouter jest zgodny z API OpenAI, więc wystarczy zmienić endpoint.
var openAi = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri("https://openrouter.ai/api/v1") });
IChatClient chatClient = openAi.GetChatClient(model).AsIChatClient();

var kb = new KnowledgeBaseTools(kbDir);

AIAgent agent = chatClient.AsHarnessAgent(new HarnessAgentOptions
{
    Name = "support-agent",
    HarnessInstructions = "Używaj narzędzi świadomie. Raportuj tylko zweryfikowane fakty.",
    ChatOptions = new ChatOptions
    {
        Instructions = """
            Jesteś asystentem pomagającym rozwiązywać problemy z aplikacją zgłaszane przez biznes.
            Zasady:
            1. Zawsze zacznij od przeszukania bazy wiedzy (SearchKnowledgeBase). Próbuj kilku zapytań i synonimów.
            2. Przeczytaj najlepiej pasujące pliki (ReadKnowledgeFile), zanim odpowiesz.
            3. Pliki odsyłają do siebie (np. "zobacz bledy/token-wygasl.md"). Czytaj także pliki, do których
               odsyła przeczytany plik, jeśli mogą być potrzebne do pełnej odpowiedzi.
            4. Jeden problem bywa opisany w kilku plikach (np. przyczyna w jednym, uprawnienia w drugim,
               eskalacja w trzecim). Zanim odpowiesz, zastanów się, czego jeszcze brakuje, i doszukaj to.
               Połącz informacje w jedną spójną odpowiedź, a nie wymieniaj plików po kolei.
            5. Odpowiadaj po polsku, prostym językiem zrozumiałym dla biznesu, krok po kroku.
            6. Na końcu podaj źródła: ścieżki wszystkich użytych plików.
            7. Jeśli bazy wiedzy nie ma odpowiedzi lub tylko jej części, powiedz to wprost i wskaż, jakich
               informacji brakuje do zgłoszenia problemu zespołowi IT. Nie zgaduj.
            """,
        Tools =
        [
            AIFunctionFactory.Create(kb.SearchKnowledgeBase),
            AIFunctionFactory.Create(kb.ReadKnowledgeFile),
        ],
    },
    // OpenRouter nie obsługuje hostowanego wyszukiwania w sieci.
    DisableWebSearch = true,
    MaxContextWindowTokens = 128_000,
    MaxOutputTokens = 4_096,
});

Console.WriteLine($"agenticSearch | model: {model} | baza wiedzy: {kbDir}");
Console.WriteLine("Opisz problem (pusta linia kończy).\n");

var session = await agent.CreateSessionAsync();
while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) break;

    await foreach (var update in agent.RunStreamingAsync(input, session))
        Console.Write(update.Text);
    Console.WriteLine("\n");
}
return 0;

static string FindKnowledgeBase()
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
