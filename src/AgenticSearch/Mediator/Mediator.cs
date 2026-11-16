using System.Collections.Concurrent;
using System.Reflection;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticSearch.Mediator;

/// <summary>Żądanie obsługiwane przez dokładnie jeden handler.</summary>
public interface IRequest<out TResponse>;

/// <summary>Zapytanie zwracające wynik w postaci Result&lt;T&gt; (bez efektów ubocznych).</summary>
public interface IQuery<T> : IRequest<Result<T>>;

public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, T> : IRequestHandler<TQuery, Result<T>> where TQuery : IQuery<T>;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>Element pipeline'u wokół handlera (logowanie, obsługa wyjątków itp.).</summary>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);
}

public interface IMediator
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}

/// <summary>Minimalny mediator: znajduje handler w kontenerze i owija go zarejestrowanymi zachowaniami.</summary>
public sealed class Mediator(IServiceProvider services) : IMediator
{
    private static readonly ConcurrentDictionary<(Type Request, Type Response), object> Wrappers = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var wrapper = (HandlerWrapper<TResponse>)Wrappers.GetOrAdd((request.GetType(), typeof(TResponse)), static key =>
            Activator.CreateInstance(typeof(HandlerWrapper<,>).MakeGenericType(key.Request, key.Response))!);
        return wrapper.Handle(request, services, ct);
    }

    private abstract class HandlerWrapper<TResponse>
    {
        public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider sp, CancellationToken ct);
    }

    private sealed class HandlerWrapper<TRequest, TResponse> : HandlerWrapper<TResponse> where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider sp, CancellationToken ct)
        {
            var typed = (TRequest)request;
            var handler = sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle(typed, ct);

            // Pierwsze zarejestrowane zachowanie ma być najbardziej zewnętrzne.
            foreach (var behavior in sp.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
            {
                var next = pipeline;
                var b = behavior;
                pipeline = () => b.Handle(typed, next, ct);
            }
            return pipeline();
        }
    }
}

public static class MediatorRegistration
{
    /// <summary>Rejestruje mediatora oraz wszystkie handlery znalezione w podanych assembly.</summary>
    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddSingleton<IMediator, Mediator>();

        var handlerType = typeof(IRequestHandler<,>);
        foreach (var type in assemblies.SelectMany(a => a.GetTypes()).Where(t => t is { IsClass: true, IsAbstract: false }))
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerType))
                services.AddTransient(iface, type);

        return services;
    }
}
