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
    public static EndpointConfiguration EnableEndpointHeartbeat(
        this EndpointConfiguration endpointConfiguration,
        Action<HeartbeatOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpointConfiguration);

        var options = new HeartbeatOptions();
        configure?.Invoke(options);
        endpointConfiguration.GetSettings().Set(options);

        endpointConfiguration.EnableFeature<EndpointHeartbeatFeature>();

        // EndpointHeartbeatHandler is a [Handler] (NServiceBus 10.2 source-generated handler), so
        // it does not implement IHandleMessages and is invisible to assembly scanning. The source
        // generator intercepts this AddHandler<T>() call and rewrites it to the generated, trim-safe
        // registration. Registering here means heartbeats work regardless of the user's scanning
        // setting, with no risk of double registration.
        endpointConfiguration.AddHandler<EndpointHeartbeatHandler>();

        return endpointConfiguration;
    }
}
