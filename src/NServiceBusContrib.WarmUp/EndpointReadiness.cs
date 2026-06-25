namespace NServiceBusContrib.WarmUp;

/// <summary>A snapshot of one endpoint's readiness.</summary>
/// <param name="EndpointName">The endpoint name.</param>
/// <param name="State">The current readiness state.</param>
public readonly record struct EndpointReadiness(string EndpointName, EndpointReadinessState State);
