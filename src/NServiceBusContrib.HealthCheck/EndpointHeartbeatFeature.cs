using NServiceBus;
using NServiceBus.Features;

namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Enables liveness heartbeats: registers a startup task that periodically sends a heartbeat to
/// the endpoint's own queue. Enabled explicitly from
/// <see cref="HeartbeatConfigurationExtensions.EnableLivenessHeartbeat"/>.
/// </summary>
class EndpointHeartbeatFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var endpointName = context.Settings.EndpointName();
        var settings = context.Settings.TryGet<HeartbeatSettings>(out var configured) ? configured : new HeartbeatSettings();

        context.RegisterStartupTask(serviceProvider =>
            new EndpointHeartbeatStartupTask(endpointName, settings, serviceProvider));
    }
}
