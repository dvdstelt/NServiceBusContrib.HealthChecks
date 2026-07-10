using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus;
using NServiceBus.Features;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Periodically sends an <see cref="EndpointHeartbeat"/> to the endpoint's own queue. The
/// initial heartbeat is seeded at start so the endpoint is immediately live; thereafter only
/// the handler processing a heartbeat keeps it fresh, so a dead pump goes stale.
/// </summary>
sealed class EndpointHeartbeatStartupTask(
    string endpointName,
    HeartbeatSettings settings,
    IServiceProvider serviceProvider) : FeatureStartupTask
{
    readonly ILogger? logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<EndpointHeartbeatStartupTask>();
    readonly CancellationTokenSource stopping = new();
    Task? loop;

    protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken)
    {
        var registry = serviceProvider.GetService<IEndpointStatusRegistry>();
        if (registry is null)
        {
            // Nothing consumes heartbeats without a status registry (no NServiceBusContrib health
            // checks registered), so don't send any. Register the checks — or call
            // AddNServiceBusWarmUp() — to activate liveness.
            logger?.LogWarning(
                "Liveness heartbeat is enabled for endpoint '{EndpointName}' but no status registry is registered; heartbeats will not be sent.",
                endpointName);
            return Task.CompletedTask;
        }

        // Seed a baseline so the endpoint is live from the moment it starts; if the pump
        // never processes the heartbeats it sends, this baseline goes stale on its own.
        registry.ReportHeartbeat(endpointName, settings.ResolvedStaleAfter);

        loop = RunAsync(session, stopping.Token);
        return Task.CompletedTask;
    }

    protected override async Task OnStop(IMessageSession session, CancellationToken cancellationToken)
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        if (loop is not null)
        {
            try
            {
                // WaitAsync ties the wait to the host's shutdown token, so a send that ignores
                // cancellation cannot block shutdown indefinitely.
                await loop.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        stopping.Dispose();
    }

    async Task RunAsync(IMessageSession session, CancellationToken cancellationToken)
    {
        var message = new EndpointHeartbeat { EndpointName = endpointName, StaleAfterTicks = settings.ResolvedStaleAfter.Ticks };
        var timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

        using var timer = new PeriodicTimer(settings.ResolvedInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                // Send to this endpoint's own queue (like SendLocal) but stamp a header so the
                // audit filter can keep the heartbeat out of the audit queue.
                var options = new SendOptions();
                options.RouteToThisEndpoint();
                options.SetHeader(EndpointHeartbeat.HeaderKey, bool.TrueString);
                await session.Send(message, options, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // A failed heartbeat send is not fatal; the endpoint will go stale if it keeps failing.
                logger?.LogWarning(ex, "Failed to send heartbeat for endpoint '{EndpointName}'.", endpointName);
            }
        }
    }
}
