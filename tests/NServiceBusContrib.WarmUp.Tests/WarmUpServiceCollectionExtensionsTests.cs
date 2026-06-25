using Microsoft.Extensions.DependencyInjection;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.WarmUp.Tests;

public class WarmUpServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNServiceBusWarmUp_registers_the_status_registry()
    {
        var provider = new ServiceCollection()
            .AddNServiceBusWarmUp()
            .BuildServiceProvider();

        Assert.IsType<EndpointStatusRegistry>(provider.GetRequiredService<IEndpointStatusRegistry>());
    }

    [Fact]
    public async Task AddNServiceBusWarmUpTask_registers_task_for_the_named_endpoint()
    {
        var services = new ServiceCollection();
        services.AddNServiceBusWarmUpTask<CountingTask>("Sales");

        // The task registry is a singleton instance captured at registration time.
        var registry = (WarmUpTaskRegistry)services.Single(d => d.ServiceType == typeof(WarmUpTaskRegistry)).ImplementationInstance!;
        var provider = services.BuildServiceProvider();

        var salesActions = registry.GetFor("Sales");
        Assert.Single(salesActions);
        Assert.Empty(registry.GetFor("Billing"));

        await salesActions[0](provider, CancellationToken.None);
        Assert.Equal(1, CountingTask.Runs);
    }

    sealed class CountingTask : IWarmUpTask
    {
        public static int Runs;

        public Task WarmUpAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Runs);
            return Task.CompletedTask;
        }
    }
}
