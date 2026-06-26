using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NServiceBusContrib.HealthCheck;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.WarmUp.Tests;

public class EndpointsHealthCheckTests
{
    static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    static async Task<HealthReport> RunAsync(Action<IEndpointStatusRegistry> arrange, TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);   // wins over the default TimeProvider.System
        }

        services.AddHealthChecks().AddNServiceBusEndpoints();
        var provider = services.BuildServiceProvider();

        arrange(provider.GetRequiredService<IEndpointStatusRegistry>());

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

    [Fact]
    public async Task Healthy_when_heartbeat_is_within_the_staleness_window()
    {
        var time = new TestTimeProvider(Now);
        var report = await RunAsync(registry =>
        {
            registry.Report("Sales", EndpointReadinessState.Ready);
            registry.ReportHeartbeat("Sales", TimeSpan.FromSeconds(30));
            time.Advance(TimeSpan.FromSeconds(20));
        }, time);

        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task Unhealthy_when_heartbeat_is_stale()
    {
        var time = new TestTimeProvider(Now);
        var report = await RunAsync(registry =>
        {
            registry.Report("Sales", EndpointReadinessState.Ready);
            registry.ReportHeartbeat("Sales", TimeSpan.FromSeconds(30));
            time.Advance(TimeSpan.FromSeconds(31));
        }, time);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal("Stale", report.Entries[HealthChecksBuilderExtensions.DefaultName].Data["Sales"]);
    }

    [Fact]
    public async Task Ready_endpoint_without_heartbeat_tracking_stays_healthy()
    {
        // No heartbeat reported: liveness is simply not evaluated for this endpoint.
        var report = await RunAsync(registry => registry.Report("Sales", EndpointReadinessState.Ready));
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }
}
