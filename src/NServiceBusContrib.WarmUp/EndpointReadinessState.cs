namespace NServiceBusContrib.WarmUp;

/// <summary>The readiness of a single NServiceBus endpoint within the host.</summary>
public enum EndpointReadinessState
{
    /// <summary>Warm-up is running. The message pump has not opened yet.</summary>
    Starting,

    /// <summary>Warm-up completed. The endpoint is processing messages.</summary>
    Ready,

    /// <summary>The endpoint has stopped, either by graceful shutdown or after a fault.</summary>
    Stopped
}
