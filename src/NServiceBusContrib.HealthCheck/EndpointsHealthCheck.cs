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

        var result = kind == EndpointHealthKind.Liveness
            ? EvaluateLiveness(endpoints, now, context)
            : EvaluateReadiness(endpoints, now, context);

        return Task.FromResult(result);
    }

    // Readiness: every endpoint must have completed warm-up (Ready) and, where heartbeat
    // liveness is enabled, have a fresh heartbeat. A still-starting endpoint is not ready.
    static HealthCheckResult EvaluateReadiness(IReadOnlyCollection<EndpointStatus> endpoints, DateTimeOffset now, HealthCheckContext context)
    {
        if (endpoints.Count == 0)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                description: "No NServiceBus endpoints have started yet.");
        }

        var data = new Dictionary<string, object>(endpoints.Count);
        var problems = new List<string>();

        foreach (var endpoint in endpoints)
        {
            if (endpoint.State != EndpointReadinessState.Ready)
            {
                problems.Add($"{endpoint.EndpointName} is {endpoint.State}");
                data[endpoint.EndpointName] = endpoint.State.ToString();
            }
            else if (endpoint.IsHeartbeatStale(now, out var age))
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
            : HealthCheckResult.Healthy($"All {endpoints.Count} endpoint(s) ready.", data);
    }

    // Liveness: an endpoint is dead only if it Stopped or its heartbeat went stale. Starting and
    // Ready (with a fresh or absent heartbeat) are alive. No endpoints yet is alive too — the
    // process is still booting, not wedged; the readiness/startup probe covers never-starting.
    static HealthCheckResult EvaluateLiveness(IReadOnlyCollection<EndpointStatus> endpoints, DateTimeOffset now, HealthCheckContext context)
    {
        var data = new Dictionary<string, object>(endpoints.Count);
        var problems = new List<string>();

        foreach (var endpoint in endpoints)
        {
            if (endpoint.State == EndpointReadinessState.Stopped)
            {
                problems.Add($"{endpoint.EndpointName} is Stopped");
                data[endpoint.EndpointName] = endpoint.State.ToString();
            }
            else if (endpoint.IsHeartbeatStale(now, out var age))
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
            : HealthCheckResult.Healthy($"All {endpoints.Count} endpoint(s) live.", data);
    }
}
