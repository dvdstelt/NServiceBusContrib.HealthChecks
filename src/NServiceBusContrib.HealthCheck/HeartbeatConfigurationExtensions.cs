using System.Diagnostics.CodeAnalysis;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;

namespace NServiceBusContrib.HealthCheck;

/// <summary>Configures heartbeat liveness on an <see cref="EndpointConfiguration"/>.</summary>
public static class HeartbeatConfigurationExtensions
{
    /// <summary>
    /// Enables heartbeat liveness for the endpoint. The endpoint periodically sends a heartbeat
    /// message to its own queue; processing it keeps the endpoint's liveness fresh in the status
    /// registry. If the pump stops processing, the heartbeat goes stale and the health check
    /// reports the endpoint unhealthy.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "The heartbeat handler type is referenced directly and is preserved.")]
    public static EndpointConfiguration EnableEndpointHeartbeat(
        this EndpointConfiguration endpointConfiguration,
        Action<HeartbeatOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpointConfiguration);

        var options = new HeartbeatOptions();
        configure?.Invoke(options);
        endpointConfiguration.GetSettings().Set(options);

        endpointConfiguration.EnableFeature<EndpointHeartbeatFeature>();

        // With scanning disabled (the multi-endpoint case) the handler is not discovered, so
        // register it explicitly. With scanning enabled it is found automatically; registering
        // it again would make every heartbeat be handled twice.
        if (endpointConfiguration.AssemblyScanner().Disable)
        {
            endpointConfiguration.AddHandler<EndpointHeartbeatHandler>();
        }

        return endpointConfiguration;
    }
}
