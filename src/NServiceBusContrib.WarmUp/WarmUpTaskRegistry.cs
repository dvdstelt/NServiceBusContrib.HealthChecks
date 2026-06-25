using System.Collections.Concurrent;

namespace NServiceBusContrib.WarmUp;

/// <summary>
/// Host-level singleton holding warm-up actions registered against the service
/// collection (keyed by endpoint name). The warm-up feature pulls the actions
/// for its own endpoint at start time and runs them after any actions configured
/// inline via <c>EndpointConfiguration.WarmUp(...)</c>.
/// </summary>
sealed class WarmUpTaskRegistry
{
    readonly ConcurrentDictionary<string, List<Func<IServiceProvider, CancellationToken, Task>>> byEndpoint = new();

    public void Add(string endpointName, Func<IServiceProvider, CancellationToken, Task> action)
    {
        var list = byEndpoint.GetOrAdd(endpointName, _ => []);
        lock (list)
        {
            list.Add(action);
        }
    }

    public IReadOnlyList<Func<IServiceProvider, CancellationToken, Task>> GetFor(string endpointName)
    {
        if (!byEndpoint.TryGetValue(endpointName, out var list))
        {
            return [];
        }

        lock (list)
        {
            return list.ToArray();
        }
    }
}
