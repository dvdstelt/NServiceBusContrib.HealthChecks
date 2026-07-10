namespace NServiceBusContrib.HealthCheck;

/// <summary>Which question an endpoints health check answers.</summary>
enum EndpointHealthKind
{
    /// <summary>"Has it finished starting and can it serve?" A warming-up endpoint is not ready.</summary>
    Readiness,

    /// <summary>"Is it alive, or should it be restarted?" A warming-up endpoint is alive.</summary>
    Liveness
}
