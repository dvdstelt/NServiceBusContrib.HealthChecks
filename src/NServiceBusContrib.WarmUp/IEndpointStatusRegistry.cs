namespace NServiceBusContrib.WarmUp;

/// <summary>
/// Tracks the status of every NServiceBus endpoint in the host: its readiness
/// (driven by warm-up) and, optionally, its last heartbeat (driven by a liveness
/// mechanism such as NServiceBusContrib.HealthCheck). Registered as a host-level
/// singleton, so a process hosting multiple endpoints exposes them all through one
/// registry.
/// </summary>
public interface IEndpointStatusRegistry
{
    /// <summary>Records the readiness state of an endpoint, adding it if not seen before.</summary>
    void Report(string endpointName, EndpointReadinessState state);

    /// <summary>
    /// Records that a heartbeat was just observed for an endpoint, stamped with the
    /// current time. <paramref name="staleAfter"/> is how long the heartbeat stays valid
    /// before the endpoint should be considered unhealthy.
    /// </summary>
    void ReportHeartbeat(string endpointName, TimeSpan staleAfter);

    /// <summary>Returns a snapshot of all tracked endpoints.</summary>
    IReadOnlyCollection<EndpointStatus> GetAll();
}
