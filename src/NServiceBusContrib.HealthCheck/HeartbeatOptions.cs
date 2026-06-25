namespace NServiceBusContrib.HealthCheck;

/// <summary>Configures endpoint heartbeat liveness.</summary>
public sealed class HeartbeatOptions
{
    TimeSpan interval = TimeSpan.FromSeconds(15);
    TimeSpan? staleAfter;

    /// <summary>
    /// How often the endpoint sends a heartbeat message to its own queue. Defaults to 15 seconds.
    /// </summary>
    public TimeSpan Interval
    {
        get => interval;
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Heartbeat interval must be positive.");
            }

            interval = value;
        }
    }

    /// <summary>
    /// How long a heartbeat stays valid before the endpoint is considered unhealthy. Must be
    /// longer than <see cref="Interval"/> to tolerate a missed beat. Defaults to three intervals.
    /// </summary>
    public TimeSpan StaleAfter
    {
        get => staleAfter ?? TimeSpan.FromTicks(interval.Ticks * 3);
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Heartbeat staleness window must be positive.");
            }

            staleAfter = value;
        }
    }
}
