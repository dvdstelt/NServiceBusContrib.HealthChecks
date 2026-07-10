using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.Features;

namespace NServiceBusContrib.WarmUp;

/// <summary>
/// Runs the endpoint's warm-up actions during <see cref="Feature"/> start, before
/// the message pump opens, and tracks readiness in the registry (when registered).
/// </summary>
sealed class WarmUpStartupTask(
    string endpointName,
    IReadOnlyList<Func<IServiceProvider, CancellationToken, Task>> actions,
    IServiceProvider serviceProvider) : FeatureStartupTask
{
    protected override async Task OnStart(IMessageSession session, CancellationToken cancellationToken)
    {
        var registry = serviceProvider.GetService<IEndpointStatusRegistry>();
        registry?.Report(endpointName, EndpointReadinessState.Starting);

        foreach (var action in actions)
        {
            await action(serviceProvider, cancellationToken).ConfigureAwait(false);
        }

        registry?.Report(endpointName, EndpointReadinessState.Ready);
    }

    protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken)
    {
        serviceProvider.GetService<IEndpointStatusRegistry>()
            ?.Report(endpointName, EndpointReadinessState.Stopped);
        return Task.CompletedTask;
    }
}
