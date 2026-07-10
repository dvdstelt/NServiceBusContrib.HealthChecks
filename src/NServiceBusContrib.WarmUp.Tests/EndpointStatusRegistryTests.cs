using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.WarmUp.Tests;

public class EndpointStatusRegistryTests
{
    [Fact]
    public void Report_adds_endpoint_and_latest_state_wins()
    {
        var registry = new EndpointStatusRegistry(TimeProvider.System);

        registry.Report("Sales", EndpointReadinessState.Starting);
        registry.Report("Sales", EndpointReadinessState.Ready);
        registry.Report("Billing", EndpointReadinessState.Starting);

        var all = registry.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Equal(EndpointReadinessState.Ready, all.Single(e => e.EndpointName == "Sales").State);
        Assert.Equal(EndpointReadinessState.Starting, all.Single(e => e.EndpointName == "Billing").State);
        Assert.Null(all.Single(e => e.EndpointName == "Sales").LastHeartbeat);
    }

    [Fact]
    public void Report_rejects_empty_endpoint_name()
    {
        var registry = new EndpointStatusRegistry(TimeProvider.System);
        Assert.Throws<ArgumentException>(() => registry.Report("", EndpointReadinessState.Ready));
    }

    [Fact]
    public void ReportHeartbeat_stamps_current_time_and_window_without_changing_state()
    {
        var time = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var registry = new EndpointStatusRegistry(time);

        registry.Report("Sales", EndpointReadinessState.Ready);
        registry.ReportHeartbeat("Sales", TimeSpan.FromSeconds(45));

        var status = Assert.Single(registry.GetAll());
        Assert.Equal(EndpointReadinessState.Ready, status.State);
        Assert.Equal(time.GetUtcNow(), status.LastHeartbeat);
        Assert.Equal(TimeSpan.FromSeconds(45), status.HeartbeatStaleAfter);
    }
}
