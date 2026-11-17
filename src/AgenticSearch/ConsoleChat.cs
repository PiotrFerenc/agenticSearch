using AgenticSearch.Agents;
using AgenticSearch.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticSearch;

/// <summary>Pętla rozmowy w konsoli z agentem głównym.</summary>
public sealed class ConsoleChat(
    [FromKeyedServices(AgentNames.Support)] AIAgent agent,
    AppSettings settings)
{
    public async Task RunAsync()
    {
        Console.WriteLine($"agenticSearch | model: {settings.Model} | baza wiedzy: {settings.KnowledgeBase.Root}" +
                          (settings.Code is null ? "" : $" | kod: {settings.Code.Root}"));
        Console.WriteLine("Opisz problem (pusta linia kończy).\n");

        var session = await agent.CreateSessionAsync();
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) break;

            try
            {
                await foreach (var update in agent.RunStreamingAsync(input, session))
                    Console.Write(update.Text);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Np. 401 (zły klucz API), 429 (limit darmowego modelu), brak sieci.
                Console.Error.WriteLine($"\nBłąd wywołania modelu: {FirstLine(e.Message)}");
                Console.Error.WriteLine("Sprawdź klucz API, nazwę modelu i limity w OpenRouter, po czym spróbuj ponownie.");
            }
            Console.WriteLine("\n");
        }
    }

    private static string FirstLine(string s) => s.Split('\n', 2)[0].Trim();
}
