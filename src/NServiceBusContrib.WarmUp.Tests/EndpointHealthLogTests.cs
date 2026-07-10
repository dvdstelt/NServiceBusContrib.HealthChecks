using Microsoft.Extensions.Logging;
using NServiceBusContrib.HealthCheck;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.WarmUp.Tests;

public class EndpointHealthLogTests
{
    static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    static EndpointStatus Endpoint(string name, EndpointReadinessState state, DateTimeOffset? heartbeat = null, TimeSpan? staleAfter = null) =>
        new(name, state, heartbeat, staleAfter);

    [Fact]
    public void Warns_when_an_endpoint_becomes_unhealthy()
    {
        var logger = new ListLogger<EndpointHealthLog>();
        var log = new EndpointHealthLog(logger);

        log.Evaluate([Endpoint("Sales", EndpointReadinessState.Ready)], Now);      // healthy: nothing
        log.Evaluate([Endpoint("Sales", EndpointReadinessState.Stopped)], Now);    // -> warning

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Sales", entry.Message);
        Assert.Contains("stopped", entry.Message);
    }

    [Fact]
    public void Warns_once_then_logs_recovery()
    {
        var logger = new ListLogger<EndpointHealthLog>();
        var log = new EndpointHealthLog(logger);

        log.Evaluate([Endpoint("Sales", EndpointReadinessState.Stopped)], Now);   // warning
        log.Evaluate([Endpoint("Sales", EndpointReadinessState.Stopped)], Now);   // no duplicate
        log.Evaluate([Endpoint("Sales", EndpointReadinessState.Ready)], Now);     // recovery -> information

        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
        Assert.Equal(LogLevel.Information, logger.Entries[1].Level);
        Assert.Contains("recovered", logger.Entries[1].Message);
    }

    [Fact]
    public void Warns_when_the_heartbeat_goes_stale()
    {
        var logger = new ListLogger<EndpointHealthLog>();
        var log = new EndpointHealthLog(logger);
        var endpoint = Endpoint("Sales", EndpointReadinessState.Ready, heartbeat: Now, staleAfter: TimeSpan.FromSeconds(30));

        log.Evaluate([endpoint], Now.AddSeconds(20));   // within window: healthy
        log.Evaluate([endpoint], Now.AddSeconds(31));   // stale -> warning

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("stale", entry.Message);
    }

    [Fact]
    public void Does_not_warn_for_starting_or_ready()
    {
        var logger = new ListLogger<EndpointHealthLog>();
        var log = new EndpointHealthLog(logger);

        log.Evaluate([Endpoint("Sales", EndpointReadinessState.Starting)], Now);
        log.Evaluate([Endpoint("Sales", EndpointReadinessState.Ready)], Now);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Does_not_warn_for_starting_with_a_stale_seeded_heartbeat()
    {
        // A slow warm-up is normal, not an incident: staleness only counts once the endpoint is Ready.
        var logger = new ListLogger<EndpointHealthLog>();
        var log = new EndpointHealthLog(logger);
        var endpoint = Endpoint("Sales", EndpointReadinessState.Starting, heartbeat: Now, staleAfter: TimeSpan.FromSeconds(30));

        log.Evaluate([endpoint], Now.AddMinutes(5));

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task Background_monitor_logs_unhealthy_endpoints()
    {
        var registry = new EndpointStatusRegistry(TimeProvider.System);
        registry.Report("Sales", EndpointReadinessState.Stopped);
        var logger = new ListLogger<EndpointHealthLog>();
        var log = new EndpointHealthLog(logger);

        using var monitor = new EndpointHealthMonitor(registry, TimeProvider.System, log, TimeSpan.FromMilliseconds(50));
        await monitor.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (logger.Entries.Count == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }
        }
        finally
        {
            await monitor.StopAsync(CancellationToken.None);
        }

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Sales"));
    }
}
