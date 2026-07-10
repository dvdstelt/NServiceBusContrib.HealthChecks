using NServiceBus.Pipeline;

namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Excludes liveness heartbeat messages from auditing. Registered on the audit pipeline: when the
/// message carries the heartbeat header it short-circuits (does not call <c>next</c>), so the
/// heartbeat is never forwarded to the audit queue. Harmless when auditing is disabled: the audit
/// pipeline is simply not invoked.
/// </summary>
sealed class HeartbeatAuditFilterBehavior : Behavior<IAuditContext>
{
    public override Task Invoke(IAuditContext context, Func<Task> next) =>
        context.Message.Headers.ContainsKey(EndpointHeartbeat.HeaderKey)
            ? Task.CompletedTask
            : next();
}
