using NServiceBus;
using NServiceBusContrib.HealthCheck;

namespace NServiceBusContrib.WarmUp.Tests;

public class HeartbeatConfigurationTests
{
    [Fact]
    public void Rejects_stale_window_not_longer_than_interval()
    {
        var endpoint = new EndpointConfiguration("HeartbeatContrib.ValidationTest");

        // StaleAfter <= Interval would make the endpoint flap unhealthy between beats.
        var ex = Assert.Throws<ArgumentException>(() => endpoint.EnableLivenessHeartbeat(heartbeat =>
        {
            heartbeat.Interval(TimeSpan.FromSeconds(30));
            heartbeat.StaleAfter(TimeSpan.FromSeconds(10));
        }));

        Assert.Contains("StaleAfter", ex.Message);
    }

    [Fact]
    public void Accepts_default_stale_window()
    {
        var endpoint = new EndpointConfiguration("HeartbeatContrib.ValidationDefault");

        // No StaleAfter configured: defaults to three intervals, always valid.
        endpoint.EnableLivenessHeartbeat(heartbeat => heartbeat.Interval(TimeSpan.FromSeconds(30)));
    }
}
