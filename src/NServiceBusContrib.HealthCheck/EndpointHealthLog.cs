using Microsoft.Extensions.Logging;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Remembers each endpoint's last-observed health and logs <em>transitions</em>: a warning when an
/// endpoint becomes unhealthy (stopped, or its heartbeat has gone stale) and an information message
/// when it recovers. Deduped, so it logs once per change no matter how often it is evaluated —
/// driven by the health check on each probe, and/or by the optional background monitor.
/// </summary>
sealed class EndpointHealthLog(ILogger<EndpointHealthLog> logger)
{
    readonly Lock gate = new();
    readonly Dictionary<string, bool> unhealthy = [];

    public void Evaluate(IReadOnlyCollection<EndpointStatus> endpoints, DateTimeOffset now)
    {
        lock (gate)
        {
            foreach (var endpoint in endpoints)
            {
                var (isUnhealthy, reason) = Assess(endpoint, now);
                var wasUnhealthy = unhealthy.TryGetValue(endpoint.EndpointName, out var previous) && previous;

                if (isUnhealthy && !wasUnhealthy)
                {
                    logger.LogWarning("NServiceBus endpoint '{EndpointName}' is unhealthy: {Reason}.", endpoint.EndpointName, reason);
                }
                else if (!isUnhealthy && wasUnhealthy)
                {
                    logger.LogInformation("NServiceBus endpoint '{EndpointName}' recovered.", endpoint.EndpointName);
                }

                unhealthy[endpoint.EndpointName] = isUnhealthy;
            }
        }
    }

    // "Unhealthy" here is a genuinely bad state, not warm-up: an endpoint that stopped, or whose
    // heartbeat went stale after it became Ready. Starting is never unhealthy, even with a stale
    // seeded heartbeat: the pump is not open during warm-up, so heartbeats cannot be processed.
    static (bool Unhealthy, string Reason) Assess(EndpointStatus endpoint, DateTimeOffset now)
    {
        if (endpoint.State == EndpointReadinessState.Stopped)
        {
            return (true, "stopped");
        }

        if (endpoint.State == EndpointReadinessState.Ready && endpoint.IsHeartbeatStale(now, out var age))
        {
            return (true, $"heartbeat stale (last seen {age.TotalSeconds:F0}s ago)");
        }

        return (false, "");
    }
}
