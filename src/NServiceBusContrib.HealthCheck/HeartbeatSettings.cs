namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Configures the liveness heartbeat, in the NServiceBus fluent settings style
/// (like <c>Recoverability().Delayed(...)</c>).
/// </summary>
public sealed class HeartbeatSettings
{
    TimeSpan interval = TimeSpan.FromSeconds(15);
    TimeSpan? staleAfter;

    /// <summary>
    /// Sets how often the endpoint sends a heartbeat to its own queue. Defaults to 15 seconds.
    /// </summary>
    public HeartbeatSettings Interval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Heartbeat interval must be positive.");
        }

        this.interval = interval;
        return this;
    }

    /// <summary>
    /// Sets how long a heartbeat stays valid before the endpoint is considered unhealthy. Should be
    /// longer than the interval to tolerate a missed beat. Defaults to three intervals.
    /// </summary>
    public HeartbeatSettings StaleAfter(TimeSpan staleAfter)
    {
        if (staleAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfter), staleAfter, "Heartbeat staleness window must be positive.");
        }

        this.staleAfter = staleAfter;
        return this;
    }

    internal TimeSpan ResolvedInterval => interval;

    internal TimeSpan ResolvedStaleAfter => staleAfter ?? TimeSpan.FromTicks(interval.Ticks * 3);
}
