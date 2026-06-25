using NServiceBus;
using NServiceBus.Features;

namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Enables endpoint heartbeat liveness: registers a startup task that periodically sends a
/// heartbeat to the endpoint's own queue. Enabled explicitly from
/// <see cref="HeartbeatConfigurationExtensions.EnableEndpointHeartbeat"/>.
/// </summary>
class EndpointHeartbeatFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var endpointName = context.Settings.EndpointName();
        var options = context.Settings.TryGet<HeartbeatOptions>(out var configured) ? configured : new HeartbeatOptions();

        context.RegisterStartupTask(serviceProvider =>
            new EndpointHeartbeatStartupTask(endpointName, options, serviceProvider));
    }
}
