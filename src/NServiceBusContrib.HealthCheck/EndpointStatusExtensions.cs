using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

static class EndpointStatusExtensions
{
    /// <summary>
    /// True when the endpoint tracks a heartbeat and it has aged past its staleness window.
    /// <paramref name="age"/> is how long ago the last heartbeat was seen.
    /// </summary>
    public static bool IsHeartbeatStale(this EndpointStatus endpoint, DateTimeOffset now, out TimeSpan age)
    {
        age = TimeSpan.Zero;
        if (endpoint.LastHeartbeat is not { } lastHeartbeat || endpoint.HeartbeatStaleAfter is not { } staleAfter)
        {
            return false;
        }

        age = now - lastHeartbeat;
        return age > staleAfter;
    }
}
