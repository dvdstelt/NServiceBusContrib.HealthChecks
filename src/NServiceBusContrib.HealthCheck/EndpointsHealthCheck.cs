using Microsoft.Extensions.Diagnostics.HealthChecks;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Reports the aggregated status of every NServiceBus endpoint in the process, as either a
/// readiness or a liveness check. The only state that differs between the two is
/// <see cref="EndpointReadinessState.Starting"/>: a warming-up endpoint is not <em>ready</em>
/// but is <em>alive</em>.
/// </summary>
sealed class EndpointsHealthCheck(
    IEndpointStatusRegistry registry,
    TimeProvider timeProvider,
    EndpointHealthKind kind,
    EndpointHealthLog? healthLog = null) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var endpoints = registry.GetAll();
        var now = timeProvider.GetUtcNow();

        // Log health transitions (warning on unhealthy, information on recovery), deduped.
        healthLog?.Evaluate(endpoints, now);

        return Task.FromResult(Evaluate(endpoints, now, context));
    }

    // Readiness: every endpoint must have completed warm-up (Ready) and, where heartbeat liveness
    // is enabled, have a fresh heartbeat. A still-starting endpoint is not ready.
    // Liveness: an endpoint is dead only if it Stopped or, once Ready, its heartbeat went stale.
    // Starting is alive regardless of any seeded heartbeat: the pump is not open yet, so heartbeats
    // cannot be processed, and a long warm-up must never trip a liveness probe into a restart.
    HealthCheckResult Evaluate(IReadOnlyCollection<EndpointStatus> endpoints, DateTimeOffset now, HealthCheckContext context)
    {
        if (endpoints.Count == 0)
        {
            // No endpoint has reported status: either the process is still booting, or no endpoint
            // enabled WarmUp(). Booting is alive but not ready.
            return kind == EndpointHealthKind.Liveness
                ? HealthCheckResult.Healthy("No NServiceBus endpoints have reported status yet; the process is starting.")
                : new HealthCheckResult(
                    context.Registration.FailureStatus,
                    description: "No NServiceBus endpoints have reported status yet. Endpoints opt into tracking by enabling WarmUp() on their EndpointConfiguration.");
        }

        var data = new Dictionary<string, object>(endpoints.Count);
        var problems = new List<string>();

        foreach (var endpoint in endpoints)
        {
            if (IsProblemState(endpoint.State))
            {
                problems.Add($"{endpoint.EndpointName} is {endpoint.State}");
                data[endpoint.EndpointName] = endpoint.State.ToString();
            }
            else if (endpoint.State == EndpointReadinessState.Ready && endpoint.IsHeartbeatStale(now, out var age))
            {
                problems.Add($"{endpoint.EndpointName} heartbeat is stale (last seen {age.TotalSeconds:F0}s ago)");
                data[endpoint.EndpointName] = "Stale";
            }
            else
            {
                data[endpoint.EndpointName] = endpoint.State.ToString();
            }
        }

        return problems.Count > 0
            ? new HealthCheckResult(context.Registration.FailureStatus, string.Join(", ", problems), data: data)
            : HealthCheckResult.Healthy(
                $"All {endpoints.Count} endpoint(s) {(kind == EndpointHealthKind.Liveness ? "live" : "ready")}.",
                data);
    }

    bool IsProblemState(EndpointReadinessState state) =>
        kind == EndpointHealthKind.Liveness
            ? state == EndpointReadinessState.Stopped
            : state != EndpointReadinessState.Ready;
}
