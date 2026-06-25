using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.WarmUp.Tests;

public class WarmUpIntegrationTests
{
    [Fact]
    public async Task Warm_up_completes_before_messages_are_processed()
    {
        using var storage = new TempStorage();
        var state = new ProbeState();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(state);

        var endpoint = new EndpointConfiguration("WarmUpContrib.ProcessingTest");
        ConfigureTransport(endpoint, storage.Path);
        endpoint.WarmUp(warmup => warmup.Run(async (services, cancellationToken) =>
        {
            await Task.Delay(150, cancellationToken);
            var probe = services.GetRequiredService<ProbeState>();
            probe.WarmUpAt = DateTimeOffset.UtcNow;
            probe.WarmUpDone = true;
        }));

        builder.Services.AddNServiceBusEndpoint(endpoint);
        builder.Services.AddNServiceBusWarmUp();

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            // StartAsync only returns after warm-up has run.
            Assert.True(state.WarmUpDone);

            var registry = host.Services.GetRequiredService<IEndpointReadinessRegistry>();
            Assert.Contains(registry.GetAll(), e =>
                e.EndpointName == "WarmUpContrib.ProcessingTest" && e.State == EndpointReadinessState.Ready);

            await host.Services.GetRequiredService<IMessageSession>().SendLocal(new Ping());
            await state.Handled.Task.WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(state.WarmUpDoneWhenHandled);
            Assert.True(state.WarmUpAt <= state.HandledAt);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Warm_up_runs_when_assembly_scanning_is_disabled()
    {
        using var storage = new TempStorage();
        var state = new ProbeState();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(state);

        var endpoint = new EndpointConfiguration("WarmUpContrib.NoScanningTest");
        ConfigureTransport(endpoint, storage.Path);
        endpoint.AssemblyScanner().Disable = true;   // the multi-endpoint hosting scenario
        endpoint.WarmUp(warmup => warmup.Run(cancellationToken =>
        {
            state.WarmUpDone = true;
            return Task.CompletedTask;
        }));

        builder.Services.AddNServiceBusEndpoint(endpoint);
        builder.Services.AddNServiceBusWarmUp();

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            // Proves the feature activates without assembly scanning discovering it.
            Assert.True(state.WarmUpDone);

            var registry = host.Services.GetRequiredService<IEndpointReadinessRegistry>();
            Assert.Contains(registry.GetAll(), e =>
                e.EndpointName == "WarmUpContrib.NoScanningTest" && e.State == EndpointReadinessState.Ready);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    static void ConfigureTransport(EndpointConfiguration endpoint, string storageDirectory)
    {
        endpoint.UseTransport(new LearningTransport { StorageDirectory = storageDirectory });
        endpoint.UseSerialization<SystemJsonSerializer>();
        endpoint.SendFailedMessagesTo("error");
        endpoint.EnableInstallers();
    }

    public sealed class ProbeState
    {
        public volatile bool WarmUpDone;
        public bool WarmUpDoneWhenHandled;
        public DateTimeOffset WarmUpAt;
        public DateTimeOffset HandledAt;
        public TaskCompletionSource Handled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    sealed class TempStorage : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nsbcontrib-warmup", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
                // best-effort cleanup of the learning transport folder
            }
        }
    }

    public class Ping : IMessage
    {
    }

    public class PingHandler(ProbeState state) : IHandleMessages<Ping>
    {
        public Task Handle(Ping message, IMessageHandlerContext context)
        {
            state.HandledAt = DateTimeOffset.UtcNow;
            state.WarmUpDoneWhenHandled = state.WarmUpDone;
            state.Handled.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
