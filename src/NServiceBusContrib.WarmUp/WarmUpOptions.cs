using Microsoft.Extensions.DependencyInjection;

namespace NServiceBusContrib.WarmUp;

/// <summary>
/// Collects the warm-up actions for an endpoint. All registered forms are
/// normalized to a <see cref="Func{IServiceProvider, CancellationToken, Task}"/>
/// and run sequentially, in registration order, before the message pump opens.
/// </summary>
public sealed class WarmUpOptions
{
    readonly List<Func<IServiceProvider, CancellationToken, Task>> actions = [];

    internal IReadOnlyList<Func<IServiceProvider, CancellationToken, Task>> Actions => actions;

    /// <summary>Runs a warm-up action.</summary>
    public WarmUpOptions Run(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        actions.Add((_, cancellationToken) => action(cancellationToken));
        return this;
    }

    /// <summary>Runs a warm-up action with access to the endpoint's service provider.</summary>
    public WarmUpOptions Run(Func<IServiceProvider, CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        actions.Add(action);
        return this;
    }

    /// <summary>Runs a warm-up task of type <typeparamref name="T"/>, resolved from the endpoint's service provider.</summary>
    public WarmUpOptions Run<T>() where T : IWarmUpTask
    {
        actions.Add((services, cancellationToken) =>
            ActivatorUtilities.GetServiceOrCreateInstance<T>(services).WarmUpAsync(cancellationToken));
        return this;
    }

    /// <summary>Runs an already-constructed warm-up task instance.</summary>
    public WarmUpOptions Run(IWarmUpTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        actions.Add((_, cancellationToken) => task.WarmUpAsync(cancellationToken));
        return this;
    }
}
