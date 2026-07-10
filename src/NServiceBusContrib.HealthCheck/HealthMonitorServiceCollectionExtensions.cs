using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

/// <summary>Registers the optional background endpoint-health monitor.</summary>
public static class HealthMonitorServiceCollectionExtensions
{
    /// <summary>
    /// Adds a background service that periodically checks every endpoint and logs a warning when one
    /// becomes unhealthy (stopped or heartbeat stale) and information when it recovers. Use this when
    /// you want health transitions logged even if nothing probes <c>/health</c>. Without it, the same
    /// transition logging still happens, but only when a health check runs (i.e. on a probe).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="interval">How often to poll. Defaults to 30 seconds.</param>
    public static IServiceCollection AddNServiceBusEndpointHealthMonitor(this IServiceCollection services, TimeSpan? interval = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var pollInterval = interval ?? TimeSpan.FromSeconds(30);
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), pollInterval, "The monitor interval must be positive.");
        }

        services.AddNServiceBusWarmUp();               // status registry + TimeProvider
        services.TryAddSingleton<EndpointHealthLog>();
        services.AddHostedService(provider => new EndpointHealthMonitor(
            provider.GetRequiredService<IEndpointStatusRegistry>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<EndpointHealthLog>(),
            pollInterval));

        return services;
    }
}
