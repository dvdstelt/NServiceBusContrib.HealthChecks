using NServiceBus;
using NServiceBus.Features;

namespace NServiceBusContrib.WarmUp;

/// <summary>
/// Enables warm-up: registers a <see cref="FeatureStartupTask"/> that runs the
/// configured warm-up actions before the message pump opens, and reports the
/// endpoint's readiness to <see cref="IEndpointStatusRegistry"/> when present.
/// </summary>
/// <remarks>
/// Not enabled by default; <see cref="WarmUpConfigurationExtensions.WarmUp(EndpointConfiguration)"/>
/// enables it explicitly. This works whether or not assembly scanning is enabled, which
/// matters for multi-endpoint hosts that disable scanning.
/// </remarks>
class WarmUpFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var endpointName = context.Settings.EndpointName();
        var inlineActions = context.Settings.TryGet<WarmUpOptions>(out var options)
            ? options.Actions
            : [];

        context.RegisterStartupTask(serviceProvider =>
            new WarmUpStartupTask(endpointName, inlineActions, serviceProvider));
    }
}
