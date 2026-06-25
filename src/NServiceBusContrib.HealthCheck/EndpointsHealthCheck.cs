using Microsoft.Extensions.Diagnostics.HealthChecks;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Reports the aggregated readiness of every NServiceBus endpoint in the process.
/// Healthy only when all tracked endpoints have completed warm-up and are processing.
/// </summary>
sealed class EndpointsHealthCheck(IEndpointReadinessRegistry registry) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var endpoints = registry.GetAll();
        var data = endpoints.ToDictionary(e => e.EndpointName, e => (object)e.State.ToString());

        if (endpoints.Count == 0)
        {
            return Task.FromResult(new HealthCheckResult(
                context.Registration.FailureStatus,
                description: "No NServiceBus endpoints have started yet."));
        }

        var notReady = endpoints.Where(e => e.State != EndpointReadinessState.Ready).ToArray();
        if (notReady.Length > 0)
        {
            var description = string.Join(", ", notReady.Select(e => $"{e.EndpointName} is {e.State}"));
            return Task.FromResult(new HealthCheckResult(
                context.Registration.FailureStatus,
                description: description,
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"All {endpoints.Count} endpoint(s) ready.",
            data));
    }
}
