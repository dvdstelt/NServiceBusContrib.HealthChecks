using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;

namespace NServiceBusContrib.HealthCheck;

/// <summary>Configures liveness heartbeats on an <see cref="EndpointConfiguration"/>.</summary>
public static class HeartbeatConfigurationExtensions
{
    /// <summary>
    /// Enables the liveness heartbeat for the endpoint: it periodically sends a heartbeat message
    /// to its own queue, and processing that message keeps the endpoint's liveness fresh in the
    /// status registry. If the pump stops processing, the heartbeat goes stale and
    /// <c>AddNServiceBusLiveness()</c> reports the endpoint unhealthy. This is the per-endpoint
    /// liveness source; <c>WarmUp(...)</c> is the matching readiness source.
    /// </summary>
    public static EndpointConfiguration EnableLivenessHeartbeat(
        this EndpointConfiguration endpointConfiguration,
        Action<HeartbeatSettings>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpointConfiguration);

        var settings = new HeartbeatSettings();
        configure?.Invoke(settings);

        if (settings.ResolvedStaleAfter <= settings.ResolvedInterval)
        {
            // A staleness window at or below the send interval guarantees flapping: every heartbeat
            // goes stale before the next one arrives, so health would oscillate on every beat.
            throw new ArgumentException(
                $"StaleAfter ({settings.ResolvedStaleAfter}) must be longer than Interval ({settings.ResolvedInterval}), " +
                "otherwise the heartbeat goes stale between beats and the endpoint's health flaps on every interval. " +
                "Leave StaleAfter unset to default to three intervals.",
                nameof(configure));
        }

        endpointConfiguration.GetSettings().Set(settings);

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
