using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NServiceBusContrib.WarmUp;

/// <summary>
/// Registers warm-up infrastructure and tasks against the host service collection.
/// </summary>
/// <remarks>
/// Tasks registered here run for the named endpoint only if warm-up is enabled on
/// that endpoint, either via <c>EndpointConfiguration.WarmUp()</c> or, in hosts with
/// assembly scanning enabled, by default.
/// </remarks>
public static class WarmUpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the endpoint status registry and warm-up task registry as host
    /// singletons. Safe to call multiple times.
    /// </summary>
    public static IServiceCollection AddNServiceBusWarmUp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IEndpointStatusRegistry, EndpointStatusRegistry>();
        GetOrAddTaskRegistry(services);
        return services;
    }

    /// <summary>
    /// Registers a warm-up task of type <typeparamref name="T"/> for the named endpoint.
    /// </summary>
    public static IServiceCollection AddNServiceBusWarmUpTask<T>(this IServiceCollection services, string endpointName)
        where T : class, IWarmUpTask
    {
        ArgumentException.ThrowIfNullOrEmpty(endpointName);

        services.AddNServiceBusWarmUp();
        services.TryAddTransient<T>();
        GetOrAddTaskRegistry(services).Add(endpointName,
            (provider, cancellationToken) => ActivatorUtilities.GetServiceOrCreateInstance<T>(provider).WarmUpAsync(cancellationToken));
        return services;
    }

    /// <summary>Registers a warm-up action for the named endpoint.</summary>
    public static IServiceCollection AddNServiceBusWarmUp(this IServiceCollection services, string endpointName,
        Func<IServiceProvider, CancellationToken, Task> action)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpointName);
        ArgumentNullException.ThrowIfNull(action);

        services.AddNServiceBusWarmUp();
        GetOrAddTaskRegistry(services).Add(endpointName, action);
        return services;
    }

    static WarmUpTaskRegistry GetOrAddTaskRegistry(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(WarmUpTaskRegistry));
        if (descriptor?.ImplementationInstance is WarmUpTaskRegistry existing)
        {
            return existing;
        }

        var registry = new WarmUpTaskRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
