namespace NServiceBusContrib.WarmUp;

/// <summary>
/// A unit of warm-up work that runs before an endpoint begins processing messages.
/// Implementations are resolved from the endpoint's service provider, so they can
/// depend on registered services.
/// </summary>
public interface IWarmUpTask
{
    /// <summary>
    /// Performs the warm-up work. The message pump does not open until this completes.
    /// Throwing fails endpoint start.
    /// </summary>
    Task WarmUpAsync(CancellationToken cancellationToken);
}
