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

            await foreach (var update in agent.RunStreamingAsync(input, session))
                Console.Write(update.Text);
            Console.WriteLine("\n");
        }
    }
}
