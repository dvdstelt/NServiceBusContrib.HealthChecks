namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// The tracking message an endpoint sends to its own queue. Processing it proves the
/// message pump is alive and refreshes the endpoint's heartbeat in the status registry.
/// </summary>
public class EndpointHeartbeat
{
    /// <summary>The name of the endpoint that sent the heartbeat.</summary>
    public string EndpointName { get; init; } = string.Empty;

    /// <summary>How long the heartbeat stays valid, in ticks.</summary>
    public long StaleAfterTicks { get; init; }
}
