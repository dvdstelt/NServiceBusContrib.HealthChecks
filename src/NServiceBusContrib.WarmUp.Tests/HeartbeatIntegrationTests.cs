using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBusContrib.HealthCheck;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.WarmUp.Tests;

public class HeartbeatIntegrationTests
{
    [Theory]
    [InlineData(true)]   // multi-endpoint scenario: assembly scanning disabled
    [InlineData(false)]  // scanning on: the [Handler] is still registered via the interceptor, not scanning
    public async Task Heartbeats_are_sent_and_processed_keeping_the_endpoint_live(bool disableScanning)
    {
        var storage = Path.Combine(Path.GetTempPath(), "nsbcontrib-heartbeat", Guid.NewGuid().ToString("N"));
        var registry = new CountingStatusRegistry();
        var endpointName = $"HeartbeatContrib.Liveness{(disableScanning ? "NoScan" : "Scan")}";

        var builder = Host.CreateApplicationBuilder();
        // Pre-register the registry so it wins over the default the packages would add.
        builder.Services.AddSingleton<IEndpointStatusRegistry>(registry);

        var endpoint = new EndpointConfiguration(endpointName);
        endpoint.UseTransport(new LearningTransport { StorageDirectory = storage });
        endpoint.UseSerialization<SystemJsonSerializer>();
        endpoint.SendFailedMessagesTo("error");
        endpoint.EnableInstallers();
        if (disableScanning)
        {
            endpoint.AssemblyScanner().Disable = true;
        }

        endpoint.WarmUp();
        endpoint.EnableLivenessHeartbeat(heartbeat =>
        {
            heartbeat.Interval(TimeSpan.FromMilliseconds(150));
            heartbeat.StaleAfter(TimeSpan.FromSeconds(30));
        });

        builder.Services.AddNServiceBusEndpoint(endpoint);
        builder.Services.AddNServiceBusWarmUp();

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            // The start seeds one heartbeat; counts beyond that prove the pump actually
            // received and handled heartbeat messages.
            await WaitForAsync(() => registry.HeartbeatCount >= 2, TimeSpan.FromSeconds(15));

            Assert.True(registry.HeartbeatCount >= 2, $"expected at least 2 heartbeats, observed {registry.HeartbeatCount}");
            Assert.Contains(registry.GetAll(), e =>
                e.EndpointName == endpointName && e.State == EndpointReadinessState.Ready);
        }
        finally
        {
            await host.StopAsync();
            TryDelete(storage);
        }
    }

    [Fact]
    public async Task Heartbeats_are_not_audited()
    {
        var storage = Path.Combine(Path.GetTempPath(), "nsbcontrib-heartbeat", Guid.NewGuid().ToString("N"));
        var registry = new CountingStatusRegistry();
        const string endpointName = "HeartbeatContrib.AuditTest";

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IEndpointStatusRegistry>(registry);

        var endpoint = new EndpointConfiguration(endpointName);
        endpoint.UseTransport(new LearningTransport { StorageDirectory = storage });
        endpoint.UseSerialization<SystemJsonSerializer>();
        endpoint.SendFailedMessagesTo("error");
        endpoint.AuditProcessedMessagesTo("audit");   // auditing ON
        endpoint.EnableInstallers();
        endpoint.AssemblyScanner().Disable = true;

        endpoint.WarmUp();
        endpoint.EnableLivenessHeartbeat(heartbeat =>
        {
            heartbeat.Interval(TimeSpan.FromMilliseconds(150));
            heartbeat.StaleAfter(TimeSpan.FromSeconds(30));
        });

        builder.Services.AddNServiceBusEndpoint(endpoint);
        builder.Services.AddNServiceBusWarmUp();

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            // Make sure heartbeats were actually processed, so "nothing audited" is not vacuous
            // (the seed is 1; >= 3 means at least two heartbeats round-tripped through the pump).
            await WaitForAsync(() => registry.HeartbeatCount >= 3, TimeSpan.FromSeconds(15));
            Assert.True(registry.HeartbeatCount >= 3, $"expected heartbeats to be processed, observed {registry.HeartbeatCount}");
        }
        finally
        {
            await host.StopAsync();
        }

        // Despite auditing being enabled and heartbeats being processed, none reached the audit queue.
        var auditDir = Path.Combine(storage, "audit");
        var auditedHeartbeat = Directory.Exists(auditDir)
            && Directory.EnumerateFiles(auditDir, "*", SearchOption.AllDirectories)
                .Any(file => File.ReadAllText(file).Contains(nameof(EndpointHeartbeat), StringComparison.Ordinal));

        TryDelete(storage);
        Assert.False(auditedHeartbeat, "liveness heartbeat messages must not be forwarded to the audit queue");
    }

    [Fact]
    public async Task Heartbeat_without_a_status_registry_is_a_no_op()
    {
        var storage = Path.Combine(Path.GetTempPath(), "nsbcontrib-heartbeat", Guid.NewGuid().ToString("N"));

        var builder = Host.CreateApplicationBuilder();

        var endpoint = new EndpointConfiguration("HeartbeatContrib.NoRegistry");
        endpoint.UseTransport(new LearningTransport { StorageDirectory = storage });
        endpoint.UseSerialization<SystemJsonSerializer>();
        endpoint.SendFailedMessagesTo("error");
        endpoint.EnableInstallers();
        endpoint.AssemblyScanner().Disable = true;
        endpoint.EnableLivenessHeartbeat(heartbeat => heartbeat.Interval(TimeSpan.FromMilliseconds(100)));

        builder.Services.AddNServiceBusEndpoint(endpoint);
        // Deliberately no health checks / AddNServiceBusWarmUp -> no IEndpointStatusRegistry.

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            // Long enough that several heartbeats would have been sent if not gated.
            await Task.Delay(400);
        }
        finally
        {
            await host.StopAsync();
            TryDelete(storage);
        }

        // Reaching here without throwing proves EnableLivenessHeartbeat is a safe no-op when no
        // status registry consumes it (the sender is never started; OnStop handles the null loop).
    }

    static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    sealed class CountingStatusRegistry : IEndpointStatusRegistry
    {
        int heartbeats;
        readonly ConcurrentDictionary<string, (EndpointReadinessState State, DateTimeOffset? LastHeartbeat, TimeSpan? StaleAfter)> map = new();

        public int HeartbeatCount => Volatile.Read(ref heartbeats);

        public void Report(string endpointName, EndpointReadinessState state) =>
            map.AddOrUpdate(endpointName, _ => (state, null, null), (_, e) => (state, e.LastHeartbeat, e.StaleAfter));

        public void ReportHeartbeat(string endpointName, TimeSpan staleAfter)
        {
            Interlocked.Increment(ref heartbeats);
            var now = DateTimeOffset.UtcNow;
            map.AddOrUpdate(endpointName,
                _ => (EndpointReadinessState.Starting, now, staleAfter),
                (_, e) => (e.State, now, staleAfter));
        }

        public IReadOnlyCollection<EndpointStatus> GetAll() =>
            map.Select(kvp => new EndpointStatus(kvp.Key, kvp.Value.State, kvp.Value.LastHeartbeat, kvp.Value.StaleAfter)).ToArray();
    }
}
