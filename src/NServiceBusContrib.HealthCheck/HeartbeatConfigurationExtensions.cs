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

        // Always register the handler explicitly so heartbeats are handled whether or not the
        // user enables assembly scanning, and regardless of when they toggle it. When scanning
        // is also on, NServiceBus discovers the same handler, but registration is deduplicated
        // by (handler type, message type), so the heartbeat is never handled twice.
        endpointConfiguration.AddHandler<EndpointHeartbeatHandler>();

        return endpointConfiguration;
    }
}
