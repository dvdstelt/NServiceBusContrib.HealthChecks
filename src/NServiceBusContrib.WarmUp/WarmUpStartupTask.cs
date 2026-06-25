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
    IReadOnlyList<Func<IServiceProvider, CancellationToken, Task>> inlineActions,
    IServiceProvider serviceProvider) : FeatureStartupTask
{
    protected override async Task OnStart(IMessageSession session, CancellationToken cancellationToken)
    {
        var registry = serviceProvider.GetService<IEndpointStatusRegistry>();
        registry?.Report(endpointName, EndpointReadinessState.Starting);

        // Inline actions first (configured via EndpointConfiguration.WarmUp),
        // then any registered against the service collection for this endpoint.
        var diActions = serviceProvider.GetService<WarmUpTaskRegistry>()?.GetFor(endpointName) ?? [];

        foreach (var action in inlineActions)
        {
            await action(serviceProvider, cancellationToken);
        }

        foreach (var action in diActions)
        {
            await action(serviceProvider, cancellationToken);
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
