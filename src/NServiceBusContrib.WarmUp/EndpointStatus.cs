namespace NServiceBusContrib.WarmUp;

/// <summary>A snapshot of one endpoint's status.</summary>
/// <param name="EndpointName">The endpoint name.</param>
/// <param name="State">The current readiness state.</param>
/// <param name="LastHeartbeat">When the last heartbeat was observed, or <c>null</c> if liveness is not tracked.</param>
/// <param name="HeartbeatStaleAfter">How long after <see cref="LastHeartbeat"/> the endpoint is considered stale, or <c>null</c> if liveness is not tracked.</param>
public readonly record struct EndpointStatus(
    string EndpointName,
    EndpointReadinessState State,
    DateTimeOffset? LastHeartbeat,
    TimeSpan? HeartbeatStaleAfter);
