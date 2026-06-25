using Microsoft.Extensions.Diagnostics.HealthChecks;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Reports the aggregated status of every NServiceBus endpoint in the process. Healthy only
/// when all tracked endpoints have completed warm-up and, where heartbeat liveness is enabled,
/// have a fresh heartbeat.
/// </summary>
sealed class EndpointsHealthCheck(IEndpointStatusRegistry registry, TimeProvider timeProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var endpoints = registry.GetAll();

        if (endpoints.Count == 0)
        {
            return Task.FromResult(new HealthCheckResult(
                context.Registration.FailureStatus,
                description: "No NServiceBus endpoints have started yet."));
        }

        var now = timeProvider.GetUtcNow();
        var data = new Dictionary<string, object>(endpoints.Count);
        var problems = new List<string>();

        foreach (var endpoint in endpoints)
        {
            if (endpoint.State != EndpointReadinessState.Ready)
            {
                problems.Add($"{endpoint.EndpointName} is {endpoint.State}");
                data[endpoint.EndpointName] = endpoint.State.ToString();
                continue;
            }

            if (IsHeartbeatStale(endpoint, now, out var age))
            {
                problems.Add($"{endpoint.EndpointName} heartbeat is stale (last seen {age.TotalSeconds:F0}s ago)");
                data[endpoint.EndpointName] = "Stale";
                continue;
            }

            data[endpoint.EndpointName] = endpoint.State.ToString();
        }

        if (problems.Count > 0)
        {
            return Task.FromResult(new HealthCheckResult(
                context.Registration.FailureStatus,
                description: string.Join(", ", problems),
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"All {endpoints.Count} endpoint(s) ready.", data));
    }

    static bool IsHeartbeatStale(EndpointStatus endpoint, DateTimeOffset now, out TimeSpan age)
    {
        age = TimeSpan.Zero;
        if (endpoint.LastHeartbeat is not { } lastHeartbeat || endpoint.HeartbeatStaleAfter is not { } staleAfter)
        {
            return false;
        }

        age = now - lastHeartbeat;
        return age > staleAfter;
    }
}
