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
        endpoint.EnableEndpointHeartbeat(heartbeat =>
        {
            heartbeat.Interval = TimeSpan.FromMilliseconds(150);
            heartbeat.StaleAfter = TimeSpan.FromSeconds(30);
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
