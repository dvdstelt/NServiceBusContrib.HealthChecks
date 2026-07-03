using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

/// <summary>Registers the NServiceBus endpoints health checks.</summary>
/// <remarks>
/// For a single <c>/health</c> URL (e.g. plain Docker <c>HEALTHCHECK</c>) use
/// <see cref="AddNServiceBus"/>. For the Docker/Kubernetes probe model use
/// <see cref="AddNServiceBusReadiness"/> + <see cref="AddNServiceBusLiveness"/>
/// and map them to separate URLs by tag. The only state that differs between readiness and
/// liveness is <c>Starting</c>: a warming-up endpoint is not ready but is alive.
/// </remarks>
public static class HealthChecksBuilderExtensions
{
    /// <summary>The default name of the combined (readiness) health check.</summary>
    public const string DefaultName = "nservicebus";

    /// <summary>The default name of the readiness health check.</summary>
    public const string ReadinessName = "nservicebus-ready";

    /// <summary>The default name of the liveness health check.</summary>
    public const string LivenessName = "nservicebus-live";

    /// <summary>The tag applied to the readiness health check, for <c>MapHealthChecks</c> filtering.</summary>
    public const string ReadinessTag = "ready";

    /// <summary>The tag applied to the liveness health check, for <c>MapHealthChecks</c> filtering.</summary>
    public const string LivenessTag = "live";

    /// <summary>
    /// Adds a single health check that is healthy only when every NServiceBus endpoint has
    /// completed warm-up and is processing. Equivalent to the readiness check
    /// (<see cref="AddNServiceBusReadiness"/>) but without a tag. Map it the standard
    /// way, e.g. <c>app.MapHealthChecks("/health")</c>. For Kubernetes, prefer the readiness +
    /// liveness pair.
    /// </summary>
    public static IHealthChecksBuilder AddNServiceBus(
        this IHealthChecksBuilder builder,
        string? name = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null) =>
        Add(builder, EndpointHealthKind.Readiness, name ?? DefaultName, failureStatus, tags);

    /// <summary>
    /// Adds a readiness health check (tagged <see cref="ReadinessTag"/>): healthy only when every
    /// endpoint has completed warm-up (and, where enabled, has a fresh heartbeat). A warming-up
    /// endpoint is reported not ready, so traffic is gated and — with a Docker
    /// <c>--start-period</c> — the container shows as <c>starting</c>.
    /// </summary>
    public static IHealthChecksBuilder AddNServiceBusReadiness(
        this IHealthChecksBuilder builder,
        string? name = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null) =>
        Add(builder, EndpointHealthKind.Readiness, name ?? ReadinessName, failureStatus, WithTag(ReadinessTag, tags));

    /// <summary>
    /// Adds a liveness health check (tagged <see cref="LivenessTag"/>): healthy unless an endpoint
    /// has stopped or its heartbeat has gone stale. A warming-up endpoint is reported alive, so a
    /// liveness probe does not restart the process during warm-up.
    /// </summary>
    public static IHealthChecksBuilder AddNServiceBusLiveness(
        this IHealthChecksBuilder builder,
        string? name = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null) =>
        Add(builder, EndpointHealthKind.Liveness, name ?? LivenessName, failureStatus, WithTag(LivenessTag, tags));

    static IHealthChecksBuilder Add(
        IHealthChecksBuilder builder,
        EndpointHealthKind kind,
        string name,
        HealthStatus? failureStatus,
        IEnumerable<string>? tags)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The status registry lives in the WarmUp package; ensure it is registered.
        builder.Services.AddNServiceBusWarmUp();

        // The transition logger is always on: the check logs when an endpoint becomes unhealthy or
        // recovers (deduped). The optional background monitor drives the same log without probes.
        builder.Services.TryAddSingleton<EndpointHealthLog>();

        return builder.Add(new HealthCheckRegistration(
            name,
            provider => new EndpointsHealthCheck(
                provider.GetRequiredService<IEndpointStatusRegistry>(),
                provider.GetRequiredService<TimeProvider>(),
                kind,
                provider.GetService<EndpointHealthLog>()),
            failureStatus,
            tags));
    }

    // Always include the canonical tag, plus any caller-supplied tags, so a user passing custom
    // tags does not accidentally drop the tag their MapHealthChecks predicate filters on.
    static IEnumerable<string> WithTag(string canonicalTag, IEnumerable<string>? tags) =>
        tags is null ? [canonicalTag] : new[] { canonicalTag }.Union(tags);
}
