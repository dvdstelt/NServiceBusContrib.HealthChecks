using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NServiceBusContrib.HealthCheck;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.WarmUp.Tests;

public class EndpointsHealthCheckTests
{
    static async Task<HealthReport> RunAsync(Action<IEndpointReadinessRegistry> arrange)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks().AddNServiceBusEndpoints();
        var provider = services.BuildServiceProvider();

        arrange(provider.GetRequiredService<IEndpointReadinessRegistry>());

        return await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
    }

    [Fact]
    public async Task Unhealthy_when_no_endpoints_have_started()
    {
        var report = await RunAsync(_ => { });
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }

    [Fact]
    public async Task Healthy_when_every_endpoint_is_ready()
    {
        var report = await RunAsync(registry =>
        {
            registry.Report("Sales", EndpointReadinessState.Ready);
            registry.Report("Billing", EndpointReadinessState.Ready);
        });

        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task Unhealthy_when_any_endpoint_is_not_ready()
    {
        var report = await RunAsync(registry =>
        {
            registry.Report("Sales", EndpointReadinessState.Ready);
            registry.Report("Billing", EndpointReadinessState.Stopped);
        });

        Assert.Equal(HealthStatus.Unhealthy, report.Status);

        var entry = report.Entries[HealthChecksBuilderExtensions.DefaultName];
        Assert.Equal("Stopped", entry.Data["Billing"]);
        Assert.Equal("Ready", entry.Data["Sales"]);
    }
}
