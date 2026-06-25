using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

/// <summary>Registers the NServiceBus endpoints health check.</summary>
public static class HealthChecksBuilderExtensions
{
    /// <summary>The default health check registration name.</summary>
    public const string DefaultName = "nservicebus-endpoints";

    /// <summary>
    /// Adds a health check that is healthy only when every NServiceBus endpoint in the
    /// process has completed warm-up and is processing messages. Map it the standard way,
    /// e.g. <c>app.MapHealthChecks("/health")</c>.
    /// </summary>
    public static IHealthChecksBuilder AddNServiceBusEndpoints(
        this IHealthChecksBuilder builder,
        string? name = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The readiness registry lives in the WarmUp package; ensure it is registered.
        builder.Services.AddNServiceBusWarmUp();

        return builder.Add(new HealthCheckRegistration(
            name ?? DefaultName,
            provider => new EndpointsHealthCheck(provider.GetRequiredService<IEndpointReadinessRegistry>()),
            failureStatus,
            tags));
    }
}
