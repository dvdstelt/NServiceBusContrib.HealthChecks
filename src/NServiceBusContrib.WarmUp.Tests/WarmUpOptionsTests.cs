using Microsoft.Extensions.DependencyInjection;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.WarmUp.Tests;

public class WarmUpOptionsTests
{
    static readonly IServiceProvider EmptyProvider = new ServiceCollection().BuildServiceProvider();

    [Fact]
    public async Task Runs_actions_in_registration_order()
    {
        var order = new List<string>();
        var provider = new ServiceCollection()
            .AddSingleton(order)
            .BuildServiceProvider();
        var options = new WarmUpOptions();

        options
            .Run(_ => { order.Add("first"); return Task.CompletedTask; })
            .Run((_, _) => { order.Add("second"); return Task.CompletedTask; })
            .Run<RecordingTask>();

        foreach (var action in options.Actions)
        {
            await action(provider, CancellationToken.None);
        }

        Assert.Equal(["first", "second", "third"], order);
    }

    [Fact]
    public async Task Run_instance_invokes_the_task()
    {
        var task = new FlagTask();
        var options = new WarmUpOptions();
        options.Run(task);

        await options.Actions.Single()(EmptyProvider, CancellationToken.None);

        Assert.True(task.Ran);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var options = new WarmUpOptions();
        Assert.Throws<ArgumentNullException>(() => options.Run((Func<CancellationToken, Task>)null!));
        Assert.Throws<ArgumentNullException>(() => options.Run((IWarmUpTask)null!));
    }

    sealed class RecordingTask(List<string> order) : IWarmUpTask
    {
        public Task WarmUpAsync(CancellationToken cancellationToken = default)
        {
            order.Add("third");
            return Task.CompletedTask;
        }
    }

    sealed class FlagTask : IWarmUpTask
    {
        public bool Ran { get; private set; }

        public Task WarmUpAsync(CancellationToken cancellationToken = default)
        {
            Ran = true;
            return Task.CompletedTask;
        }
    }
}
