using AgenticSearch;
using AgenticSearch.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Composition root: cała konfiguracja zależności w jednym miejscu.
var settings = AppSettings.Load();
if (settings.IsFailed)
{
    foreach (var error in settings.Errors) Console.Error.WriteLine(error.Message);
    return 1;
}

var services = new ServiceCollection()
    .AddSingleton(settings.Value)
    .AddAppLogging(settings.Value)
    .AddLlm(settings.Value)
    .AddRequestPipeline()
    .AddKnowledgeBase(settings.Value)
    .AddCodeSearch(settings.Value)
    .AddAgents(settings.Value)
    .AddSingleton<ConsoleChat>();

await using var provider = services.BuildServiceProvider();
await provider.GetRequiredService<ConsoleChat>().RunAsync();
return 0;
