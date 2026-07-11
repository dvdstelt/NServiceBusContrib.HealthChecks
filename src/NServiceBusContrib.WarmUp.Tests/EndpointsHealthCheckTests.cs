using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NServiceBusContrib.HealthCheck;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.WarmUp.Tests;

public class EndpointsHealthCheckTests
{
    static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    static Task<HealthReport> RunAsync(Action<IEndpointStatusRegistry> arrange, CancellationToken cancellationToken, TimeProvider? timeProvider = null) =>
        RunCoreAsync(arrange, b => b.AddNServiceBus(), cancellationToken, timeProvider);

    static Task<HealthReport> RunReadinessAsync(Action<IEndpointStatusRegistry> arrange, CancellationToken cancellationToken, TimeProvider? timeProvider = null) =>
        RunCoreAsync(arrange, b => b.AddNServiceBusReadiness(), cancellationToken, timeProvider);

    static Task<HealthReport> RunLivenessAsync(Action<IEndpointStatusRegistry> arrange, CancellationToken cancellationToken, TimeProvider? timeProvider = null) =>
        RunCoreAsync(arrange, b => b.AddNServiceBusLiveness(), cancellationToken, timeProvider);

    static async Task<HealthReport> RunCoreAsync(
        Action<IEndpointStatusRegistry> arrange,
        Action<IHealthChecksBuilder> register,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null,
        Func<HealthCheckRegistration, bool>? predicate = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);   // wins over the default TimeProvider.System
        }

        register(services.AddHealthChecks());
        var provider = services.BuildServiceProvider();

        arrange(provider.GetRequiredService<IEndpointStatusRegistry>());

        var healthChecks = provider.GetRequiredService<HealthCheckService>();
        return predicate is null
            ? await healthChecks.CheckHealthAsync(cancellationToken)
            : await healthChecks.CheckHealthAsync(predicate, cancellationToken);
    }

    // ---- Combined check (AddNServiceBus == readiness semantics) ----

    [Fact]
    public async Task Unhealthy_when_no_endpoints_have_started()
    {
        var report = await RunAsync(_ => { }, CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }

    [Fact]
    public async Task Healthy_when_every_endpoint_is_ready()
    {
        var report = await RunAsync(registry =>
        {
            registry.Report("Sales", EndpointReadinessState.Ready);
            registry.Report("Billing", EndpointReadinessState.Ready);
        }, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task Unhealthy_when_any_endpoint_is_not_ready()
    {
        var report = await RunAsync(registry =>
        {
            registry.Report("Sales", EndpointReadinessState.Ready);
            registry.Report("Billing", EndpointReadinessState.Stopped);
        }, CancellationToken.None);

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
        }, CancellationToken.None, time);

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
        }, CancellationToken.None, time);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal("Stale", report.Entries[HealthChecksBuilderExtensions.DefaultName].Data["Sales"]);
    }

    [Fact]
    public async Task Ready_endpoint_without_heartbeat_tracking_stays_healthy()
    {
        // No heartbeat reported: liveness is simply not evaluated for this endpoint.
        var report = await RunAsync(registry => registry.Report("Sales", EndpointReadinessState.Ready), CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    // ---- Readiness check ----

    [Fact]
    public async Task Readiness_is_unhealthy_while_an_endpoint_is_starting()
    {
        var report = await RunReadinessAsync(registry => registry.Report("Sales", EndpointReadinessState.Starting), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal("Starting", report.Entries[HealthChecksBuilderExtensions.ReadinessName].Data["Sales"]);
    }

    [Fact]
    public async Task Readiness_keeps_its_canonical_tag_when_custom_tags_supplied()
    {
        var report = await RunCoreAsync(
            registry => registry.Report("Sales", EndpointReadinessState.Ready),
            builder => builder.AddNServiceBusReadiness(tags: ["custom"]),
            CancellationToken.None,
            predicate: r => r.Tags.Contains(HealthChecksBuilderExtensions.ReadinessTag));

        // Filtering on the canonical "ready" tag still finds the check despite the custom tag.
        Assert.True(report.Entries.ContainsKey(HealthChecksBuilderExtensions.ReadinessName));
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    // ---- Liveness check ----

    [Fact]
    public async Task Liveness_is_healthy_while_an_endpoint_is_starting()
    {
        var report = await RunLivenessAsync(registry => registry.Report("Sales", EndpointReadinessState.Starting), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Equal("Starting", report.Entries[HealthChecksBuilderExtensions.LivenessName].Data["Sales"]);
    }

    [Fact]
    public async Task Liveness_is_healthy_when_no_endpoints_have_started()
    {
        var report = await RunLivenessAsync(_ => { }, CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task Liveness_is_healthy_while_starting_even_with_a_stale_seeded_heartbeat()
    {
        // A long warm-up must never trip the liveness probe: the pump is not open during Starting,
        // so the seeded heartbeat cannot be refreshed and would otherwise read as stale.
        var time = new TestTimeProvider(Now);
        var report = await RunLivenessAsync(registry =>
        {
            registry.Report("Sales", EndpointReadinessState.Starting);
            registry.ReportHeartbeat("Sales", TimeSpan.FromSeconds(30));
            time.Advance(TimeSpan.FromMinutes(5));   // warm-up takes far longer than StaleAfter
        }, CancellationToken.None, time);

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Equal("Starting", report.Entries[HealthChecksBuilderExtensions.LivenessName].Data["Sales"]);
    }

    [Fact]
    public async Task Liveness_is_unhealthy_when_an_endpoint_is_stopped()
    {
        var report = await RunLivenessAsync(registry => registry.Report("Sales", EndpointReadinessState.Stopped), CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }

    [Fact]
    public async Task Liveness_is_unhealthy_when_heartbeat_is_stale()
    {
        var time = new TestTimeProvider(Now);
        var report = await RunLivenessAsync(registry =>
        {
            registry.Report("Sales", EndpointReadinessState.Ready);
            registry.ReportHeartbeat("Sales", TimeSpan.FromSeconds(30));
            time.Advance(TimeSpan.FromSeconds(31));
        }, CancellationToken.None, time);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal("Stale", report.Entries[HealthChecksBuilderExtensions.LivenessName].Data["Sales"]);
    }

    [Fact]
    public async Task Liveness_is_healthy_when_starting_alongside_a_ready_endpoint()
    {
        var report = await RunLivenessAsync(registry =>
        {
            registry.Report("Sales", EndpointReadinessState.Starting);
            registry.Report("Billing", EndpointReadinessState.Ready);
        }, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task Liveness_is_unhealthy_when_one_endpoint_stopped_among_ready()
    {
        var report = await RunLivenessAsync(registry =>
        {
            registry.Report("Sales", EndpointReadinessState.Stopped);
            registry.Report("Billing", EndpointReadinessState.Ready);
        }, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }

    [Fact]
    public async Task Liveness_ready_without_heartbeat_tracking_is_healthy()
    {
        // Never having sent a heartbeat is not "dead".
        var report = await RunLivenessAsync(registry => registry.Report("Sales", EndpointReadinessState.Ready), CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    // ---- Tag filtering: readiness and liveness map to separate URLs ----

    [Fact]
    public async Task Readiness_and_liveness_checks_are_filtered_by_tag()
    {
        // A warming-up endpoint: not ready, but alive.
        Action<IEndpointStatusRegistry> arrange = registry => registry.Report("Sales", EndpointReadinessState.Starting);
        Action<IHealthChecksBuilder> registerBoth = builder =>
        {
            builder.AddNServiceBusReadiness();
            builder.AddNServiceBusLiveness();
        };

        var ready = await RunCoreAsync(arrange, registerBoth, CancellationToken.None,
            predicate: r => r.Tags.Contains(HealthChecksBuilderExtensions.ReadinessTag));
        Assert.Equal(HealthStatus.Unhealthy, ready.Status);
        Assert.True(ready.Entries.ContainsKey(HealthChecksBuilderExtensions.ReadinessName));
        Assert.False(ready.Entries.ContainsKey(HealthChecksBuilderExtensions.LivenessName));

        var live = await RunCoreAsync(arrange, registerBoth, CancellationToken.None,
            predicate: r => r.Tags.Contains(HealthChecksBuilderExtensions.LivenessTag));
        Assert.Equal(HealthStatus.Healthy, live.Status);
        Assert.True(live.Entries.ContainsKey(HealthChecksBuilderExtensions.LivenessName));
        Assert.False(live.Entries.ContainsKey(HealthChecksBuilderExtensions.ReadinessName));
    }
}
