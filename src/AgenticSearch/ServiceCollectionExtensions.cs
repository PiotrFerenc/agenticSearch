using System.ClientModel;
using AgenticSearch.Agents;
using AgenticSearch.Code;
using AgenticSearch.Configuration;
using AgenticSearch.Knowledge;
using AgenticSearch.Mediator;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace AgenticSearch;

/// <summary>Kompozycja kontenera zależności, podzielona na funkcjonalne moduły.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppLogging(this IServiceCollection services, AppSettings settings) =>
        services.AddLogging(b => b
            .SetMinimumLevel(settings.LogLevel)
            .AddFilter("Microsoft", LogLevel.Warning)
            .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; })
            // Logi na stderr, żeby nie mieszały się z odpowiedzią agenta na stdout.
            .Services.Configure<Microsoft.Extensions.Logging.Console.ConsoleLoggerOptions>(o => o.LogToStandardErrorThreshold = LogLevel.Trace));

    public static IServiceCollection AddLlm(this IServiceCollection services, AppSettings settings) =>
        // OpenRouter jest zgodny z API OpenAI, więc wystarczy zmienić endpoint.
        services.AddSingleton<IChatClient>(_ => new OpenAIClient(
                new ApiKeyCredential(settings.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri("https://openrouter.ai/api/v1") })
            .GetChatClient(settings.Model)
            .AsIChatClient());

    /// <summary>Mediator, handlery oraz zachowania pipeline'u (pierwsze zarejestrowane jest najbardziej zewnętrzne).</summary>
    public static IServiceCollection AddRequestPipeline(this IServiceCollection services) => services
        .AddMediator(typeof(ServiceCollectionExtensions).Assembly)
        .AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
        .AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>));

    public static IServiceCollection AddKnowledgeBase(this IServiceCollection services, AppSettings settings) => services
        .AddSingleton(settings.KnowledgeBase)
        .AddSingleton<KnowledgeBaseTools>();

    public static IServiceCollection AddCodeSearch(this IServiceCollection services, AppSettings settings)
    {
        if (settings.Code is null) return services;
        return services
            .AddSingleton(settings.Code)
            .AddSingleton<CodeWorkspace>()
            .AddSingleton<CodeTools>();
    }

    /// <summary>Agenci jako usługi kluczowane nazwą. Agent kodu jest rejestrowany tylko przy ustawionym CODE_DIR.</summary>
    public static IServiceCollection AddAgents(this IServiceCollection services, AppSettings settings)
    {
        services.AddKeyedSingleton<AIAgent>(AgentNames.Knowledge, (sp, _) =>
            KnowledgeAgent.Create(sp.GetRequiredService<IChatClient>(), sp.GetRequiredService<KnowledgeBaseTools>()));

        var hasCode = settings.Code is not null;
        if (hasCode)
            services.AddKeyedSingleton<AIAgent>(AgentNames.Code, (sp, _) =>
                CodeAgent.Create(sp.GetRequiredService<IChatClient>(), sp.GetRequiredService<CodeTools>()));

        return services.AddKeyedSingleton<AIAgent>(AgentNames.Support, (sp, _) =>
        {
            var tools = new List<AITool> { sp.GetRequiredKeyedService<AIAgent>(AgentNames.Knowledge).AsAIFunction() };
            if (hasCode) tools.Add(sp.GetRequiredKeyedService<AIAgent>(AgentNames.Code).AsAIFunction());
            return SupportAgent.Create(sp.GetRequiredService<IChatClient>(), tools, hasCode);
        });
    }
}
