using Microsoft.Extensions.Hosting;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Optional background service that periodically evaluates endpoint health and logs transitions via
/// <see cref="EndpointHealthLog"/>. Unlike the in-check logging, it runs even when nothing is
/// probing <c>/health</c>, so a hung endpoint (stale heartbeat) is detected on its own.
/// Enabled with <see cref="HealthMonitorServiceCollectionExtensions.AddNServiceBusEndpointHealthMonitor"/>.
/// </summary>
sealed class EndpointHealthMonitor(
    IEndpointStatusRegistry registry,
    TimeProvider timeProvider,
    EndpointHealthLog healthLog,
    TimeSpan interval) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval, timeProvider);
        try
        {
            do
            {
                healthLog.Evaluate(registry.GetAll(), timeProvider.GetUtcNow());
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
