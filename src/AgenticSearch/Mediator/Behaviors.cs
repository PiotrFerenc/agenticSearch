using FluentResults;
using Microsoft.Extensions.Logging;

namespace AgenticSearch.Mediator;

/// <summary>Zapisuje w logu każde wywołanie narzędzia i jego wynik. Dzięki temu widać, co robią agenci.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResultBase
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        log.LogInformation("→ {Request}", request);
        var response = await next();
        if (response.IsSuccess) log.LogInformation("✓ {Name}", typeof(TRequest).Name);
        else log.LogWarning("✗ {Name}: {Errors}", typeof(TRequest).Name, string.Join("; ", response.Errors.Select(e => e.Message)));
        return response;
    }
}

/// <summary>Zamienia nieoczekiwane wyjątki w nieudany Result, żeby narzędzie zawsze zwróciło agentowi tekst.</summary>
public sealed class ExceptionHandlingBehavior<TRequest, TResponse>(ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : ResultBase<TResponse>, new()
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        try
        {
            return await next();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogError(e, "Nieoczekiwany błąd w {Name}", typeof(TRequest).Name);
            return new TResponse().WithError($"Błąd wewnętrzny: {e.Message}");
        }
    }
}
