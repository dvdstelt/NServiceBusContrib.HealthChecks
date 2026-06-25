namespace NServiceBusContrib.WarmUp;

/// <summary>
/// Tracks the readiness of every NServiceBus endpoint in the host. Registered as
/// a host-level singleton, so a process hosting multiple endpoints exposes them
/// all through one registry. Consumed by NServiceBusContrib.HealthCheck.
/// </summary>
public interface IEndpointReadinessRegistry
{
    /// <summary>Records the current state of an endpoint, adding it if not seen before.</summary>
    void Report(string endpointName, EndpointReadinessState state);

    /// <summary>Returns a snapshot of all tracked endpoints.</summary>
    IReadOnlyCollection<EndpointReadiness> GetAll();
}
