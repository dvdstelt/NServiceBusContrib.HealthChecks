using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;

namespace NServiceBusContrib.WarmUp;

/// <summary>Configures warm-up on an <see cref="EndpointConfiguration"/>.</summary>
public static class WarmUpConfigurationExtensions
{
    /// <summary>
    /// Enables warm-up for the endpoint and registers the supplied actions. They run,
    /// in order, before the endpoint begins processing messages. Can be called more
    /// than once; actions accumulate.
    /// </summary>
    public static EndpointConfiguration WarmUp(this EndpointConfiguration endpointConfiguration, Action<WarmUpOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(endpointConfiguration);
        ArgumentNullException.ThrowIfNull(configure);

        var settings = endpointConfiguration.GetSettings();
        if (!settings.TryGet<WarmUpOptions>(out var options))
        {
            options = new WarmUpOptions();
            settings.Set(options);
        }

        configure(options);
        endpointConfiguration.EnableFeature<WarmUpFeature>();
        return endpointConfiguration;
    }

    /// <summary>
    /// Enables warm-up for the endpoint without configuring any warm-up actions. Use this to opt
    /// an endpoint into readiness tracking (and the health checks built on it) when there is
    /// nothing to warm up.
    /// </summary>
    public static EndpointConfiguration WarmUp(this EndpointConfiguration endpointConfiguration)
    {
        ArgumentNullException.ThrowIfNull(endpointConfiguration);
        endpointConfiguration.EnableFeature<WarmUpFeature>();
        return endpointConfiguration;
    }
}
