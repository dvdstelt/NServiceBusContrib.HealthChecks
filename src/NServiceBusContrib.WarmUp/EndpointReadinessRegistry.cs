using System.Collections.Concurrent;

namespace NServiceBusContrib.WarmUp;

/// <summary>Thread-safe default <see cref="IEndpointReadinessRegistry"/>.</summary>
sealed class EndpointReadinessRegistry : IEndpointReadinessRegistry
{
    readonly ConcurrentDictionary<string, EndpointReadinessState> states = new();

    public void Report(string endpointName, EndpointReadinessState state)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpointName);
        states[endpointName] = state;
    }

    public IReadOnlyCollection<EndpointReadiness> GetAll() =>
        states.Select(kvp => new EndpointReadiness(kvp.Key, kvp.Value)).ToArray();
}
