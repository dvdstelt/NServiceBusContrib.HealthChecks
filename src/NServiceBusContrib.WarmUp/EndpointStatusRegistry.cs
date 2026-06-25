using System.Collections.Concurrent;

namespace NServiceBusContrib.WarmUp;

/// <summary>Thread-safe default <see cref="IEndpointStatusRegistry"/>.</summary>
sealed class EndpointStatusRegistry(TimeProvider timeProvider) : IEndpointStatusRegistry
{
    sealed class Entry
    {
        public EndpointReadinessState State;
        public DateTimeOffset? LastHeartbeat;
        public TimeSpan? HeartbeatStaleAfter;
    }

    readonly ConcurrentDictionary<string, Entry> entries = new();

    public void Report(string endpointName, EndpointReadinessState state)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpointName);
        var entry = entries.GetOrAdd(endpointName, _ => new Entry());
        lock (entry)
        {
            entry.State = state;
        }
    }

    public void ReportHeartbeat(string endpointName, TimeSpan staleAfter)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpointName);
        var now = timeProvider.GetUtcNow();
        var entry = entries.GetOrAdd(endpointName, _ => new Entry());
        lock (entry)
        {
            entry.LastHeartbeat = now;
            entry.HeartbeatStaleAfter = staleAfter;
        }
    }

    public IReadOnlyCollection<EndpointStatus> GetAll() =>
        entries.Select(kvp =>
        {
            lock (kvp.Value)
            {
                return new EndpointStatus(kvp.Key, kvp.Value.State, kvp.Value.LastHeartbeat, kvp.Value.HeartbeatStaleAfter);
            }
        }).ToArray();
}
