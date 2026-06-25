using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.WarmUp.Tests;

public class EndpointReadinessRegistryTests
{
    [Fact]
    public void Report_adds_endpoint_and_latest_state_wins()
    {
        var registry = new EndpointReadinessRegistry();

        registry.Report("Sales", EndpointReadinessState.Starting);
        registry.Report("Sales", EndpointReadinessState.Ready);
        registry.Report("Billing", EndpointReadinessState.Starting);

        var all = registry.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Equal(EndpointReadinessState.Ready, all.Single(e => e.EndpointName == "Sales").State);
        Assert.Equal(EndpointReadinessState.Starting, all.Single(e => e.EndpointName == "Billing").State);
    }

    [Fact]
    public void Report_rejects_empty_endpoint_name()
    {
        var registry = new EndpointReadinessRegistry();
        Assert.Throws<ArgumentException>(() => registry.Report("", EndpointReadinessState.Ready));
    }
}
