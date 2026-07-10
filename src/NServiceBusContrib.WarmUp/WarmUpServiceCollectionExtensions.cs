using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NServiceBusContrib.WarmUp;

/// <summary>Registers warm-up infrastructure against the host service collection.</summary>
public static class WarmUpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the endpoint status registry as a host singleton. Safe to call multiple times.
    /// Warm-up tasks themselves are configured per endpoint via
    /// <see cref="WarmUpConfigurationExtensions.WarmUp(NServiceBus.EndpointConfiguration, System.Action{WarmUpOptions})"/>.
    /// </summary>
    public static IServiceCollection AddNServiceBusWarmUp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IEndpointStatusRegistry, EndpointStatusRegistry>();
        return services;
    }
}
