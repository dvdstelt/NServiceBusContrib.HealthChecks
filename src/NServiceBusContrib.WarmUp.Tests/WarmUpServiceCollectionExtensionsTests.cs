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
}
