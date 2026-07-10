# NServiceBusContrib.HealthCheck

Aggregates the readiness of every NServiceBus endpoint in the process into a
single ASP.NET Core health check, suitable for a container `/health` endpoint.
A process may host multiple endpoints (NServiceBus 10.2+); if one faults while
the others keep running, the health check reports unhealthy.

```csharp
builder.Services
    .AddHealthChecks()
    .AddNServiceBus();

// map it the standard way
app.MapHealthChecks("/health");
```

- **Healthy** — every endpoint has completed warm-up and (where heartbeat
  liveness is enabled) has a fresh heartbeat.
- **Unhealthy** — at least one endpoint is still starting, has stopped, or its
  heartbeat has gone stale.

Readiness is driven by **NServiceBusContrib.WarmUp**, so enable warm-up on each
endpoint you want reflected (`endpointConfiguration.WarmUp(...)`).

## Readiness + liveness (Docker / Kubernetes)

A warming-up endpoint is *alive but not ready*. Register the two probes and map
them to separate URLs so a liveness probe doesn't restart the app during warm-up:

```csharp
builder.Services
    .AddHealthChecks()
    .AddNServiceBusReadiness()   // tag: "ready"
    .AddNServiceBusLiveness();   // tag: "live"

app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });
app.MapHealthChecks("/health/live",  new() { Predicate = r => r.Tags.Contains("live")  });
```

| Endpoint state | `/health/ready` | `/health/live` |
| -------------- | --------------- | -------------- |
| Starting       | Unhealthy       | Healthy        |
| Ready (fresh)  | Healthy         | Healthy        |
| Stopped / stale heartbeat | Unhealthy | Unhealthy   |

Kubernetes: point `startupProbe` + `readinessProbe` at `/health/ready` and
`livenessProbe` at `/health/live`. Docker: one `HEALTHCHECK --start-period=...`
at `/health/ready` (or `/health`) — Docker's `starting` state comes from the
start period. See [docs/healthcheck.md](../../docs/healthcheck.md) for full
probe examples.

## Heartbeat liveness (optional)

Detect a stalled or dead message pump, not just an orderly shutdown. Configured
**per endpoint** (the mirror of `WarmUp(...)`): instrument the endpoint here, then
expose it with `AddNServiceBusLiveness()`.

```csharp
endpointConfiguration.EnableLivenessHeartbeat(heartbeat =>
{
    heartbeat.Interval(TimeSpan.FromSeconds(15));
    heartbeat.StaleAfter(TimeSpan.FromMinutes(1));   // defaults to 3 * interval
});
```

The endpoint periodically sends a heartbeat to its own queue; processing it keeps
the endpoint live. If the pump stops processing, the heartbeat goes stale and the
liveness check reports the endpoint unhealthy.

Heartbeats are automatically **excluded from auditing**, so they never reach the
audit queue even when `AuditProcessedMessagesTo(...)` is enabled.

## Logging

Health transitions are logged (deduped): a **warning** when an endpoint becomes
unhealthy (stopped or heartbeat stale) and **information** when it recovers. This is
on by default and logged when a health check runs (on a probe). To log transitions
even without probes, add the optional background monitor:

```csharp
builder.Services.AddNServiceBusEndpointHealthMonitor();   // polls every 30s (configurable)
```
