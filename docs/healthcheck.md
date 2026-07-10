# NServiceBusContrib.HealthCheck

Aggregates the status of every NServiceBus endpoint in the process into ASP.NET
Core health checks, suitable for container `/health` probes.

## Registration

For a single `/health` URL (e.g. plain Docker with one `HEALTHCHECK`):

```csharp
builder.Services
    .AddHealthChecks()
    .AddNServiceBus();

app.MapHealthChecks("/health");
```

For the Docker/Kubernetes probe model, register the readiness and liveness checks
and map them to separate URLs by tag:

```csharp
builder.Services
    .AddHealthChecks()
    .AddNServiceBusReadiness()   // tag: "ready"
    .AddNServiceBusLiveness();   // tag: "live"

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});
```

## Readiness vs liveness

The two checks answer different questions, and `Starting` (warming up) is the
only endpoint state where they differ; that difference is the whole point:

| Endpoint state           | `/health/ready` | `/health/live` |
| ------------------------ | --------------- | -------------- |
| not started yet          | Unhealthy       | Healthy        |
| **Starting** (warm-up)   | Unhealthy       | **Healthy**    |
| Ready, fresh heartbeat   | Healthy         | Healthy        |
| Ready, stale heartbeat   | Unhealthy       | Unhealthy      |
| Stopped / crashed        | Unhealthy       | Unhealthy      |

- **Readiness**: "has it finished starting and can it serve?" Gates traffic.
- **Liveness**: "is it alive, or should it be restarted?" A warming-up endpoint
  is alive, so liveness stays healthy during warm-up and only fails on a real
  crash (`Stopped`) or a stalled pump (stale heartbeat). An empty registry
  (nothing started yet) is alive too, so a liveness probe never restarts a
  still-booting process.

### Docker

Plain Docker has a single health probe. Point it at readiness (or the combined
`/health`); the **`starting`** state comes from `--start-period`, not the app:

```dockerfile
HEALTHCHECK --start-period=60s --interval=10s --timeout=3s --retries=3 \
  CMD curl -f http://localhost:8080/health/ready || exit 1
```

While `/health/ready` fails inside the start period, Docker shows the container as
`starting`; the first success flips it to `healthy`; failures only mark it
`unhealthy` after the start period elapses (and each restart resets the window).

### Kubernetes

```yaml
startupProbe:        # bounds total boot time; the others don't run until it passes
  httpGet: { path: /health/ready, port: 8080 }
  periodSeconds: 10
  failureThreshold: 30        # ~5 minutes of warm-up budget
readinessProbe:      # gates Service traffic (in/out of rotation)
  httpGet: { path: /health/ready, port: 8080 }
  periodSeconds: 10
livenessProbe:       # restarts only a wedged/dead pod
  httpGet: { path: /health/live, port: 8080 }
  periodSeconds: 10
  failureThreshold: 3
```

Startup and readiness share `/health/ready`. The `startupProbe` gives a generous
boot budget and is the only probe that should kill a pod that never finishes
starting; the `livenessProbe` stays lenient so warm-up never triggers a restart.

> Don't mix the two styles on one URL. A tag-filtered endpoint excludes any check
> that lacks the tag, so either map an untagged `/health` from the combined check,
> or the tag-filtered `/health/ready` + `/health/live`.

## What the readiness check evaluates

The readiness check (and the combined `AddNServiceBus`) reads a snapshot
of all endpoints and evaluates each:

```mermaid
flowchart TD
    A(["GET /health"]) --> B{any endpoints<br/>registered?}
    B -- no --> U1["Unhealthy<br/>no endpoints started yet"]
    B -- yes --> C[for each endpoint]
    C --> D{State is Ready?}
    D -- no --> U2["Unhealthy<br/>Starting / Stopped"]
    D -- yes --> E{heartbeat tracked?<br/>LastHeartbeat set}
    E -- no --> OK[endpoint OK]
    E -- yes --> F{heartbeat older<br/>than StaleAfter?}
    F -- yes --> U3["Unhealthy<br/>Stale"]
    F -- no --> OK
    OK --> G{all endpoints OK?}
    G -- yes --> H(["Healthy"])
    G -- no --> U(["Unhealthy"])
```

- **Healthy**: every endpoint is `Ready` and, where heartbeat liveness is
  enabled, has a fresh heartbeat.
- **Unhealthy**: at least one endpoint is `Starting`/`Stopped`, or its heartbeat
  is stale. Docker keeps the container out of rotation until all endpoints are
  warm and live.
- The result `data` carries a per-endpoint breakdown (`Ready`, `Starting`,
  `Stopped`, or `Stale`) so the cause is visible.

Staleness is evaluated against an injectable `TimeProvider`, so it is unit
testable.

## Heartbeat liveness

Readiness alone can't catch a process that hangs or a pump that dies without a
clean stop: `OnStop` never fires, so the endpoint would keep reporting `Ready`.
Heartbeat liveness closes that gap.

It's configured **per endpoint** (like `WarmUp(...)`), because the heartbeat is
sent through that endpoint's own queue; host DI has no session to send through.
Think of it as two sides: *instrument each endpoint* (`WarmUp(...)` for readiness,
`EnableLivenessHeartbeat(...)` for liveness) → *expose the aggregate* with
`AddNServiceBus*` on `AddHealthChecks()`. The settings use the NServiceBus fluent
style, like `Recoverability().Delayed(...)`:

```csharp
endpointConfiguration.EnableLivenessHeartbeat(heartbeat =>
{
    heartbeat.Interval(TimeSpan.FromSeconds(15));    // how often a heartbeat is sent
    heartbeat.StaleAfter(TimeSpan.FromMinutes(1));   // defaults to 3 * interval
});
```

A `FeatureStartupTask` seeds an initial heartbeat at start and then periodically
sends an `EndpointHeartbeat` to the endpoint's **own** queue. Only the handler
*processing* that message refreshes the timestamp, so it stays fresh only while
the pump is genuinely working.

```mermaid
sequenceDiagram
    autonumber
    participant Timer as Heartbeat timer
    participant Session as IMessageSession
    participant Queue as Endpoint's own queue
    participant Handler as EndpointHeartbeatHandler
    participant Reg as IEndpointStatusRegistry

    Note over Timer: every Interval
    Timer->>Session: SendLocal(EndpointHeartbeat)
    Session->>Queue: enqueue
    Queue->>Handler: Handle(EndpointHeartbeat)
    Handler->>Reg: ReportHeartbeat(staleAfter)
    Note over Reg: LastHeartbeat = now
```

If the pump stalls, no heartbeat is processed, the timestamp ages past
`StaleAfter`, and the health check reports the endpoint unhealthy.

Heartbeats are **excluded from auditing**: each is stamped with a header and an
audit-pipeline behavior (`Behavior<IAuditContext>`) short-circuits it, so even
with `AuditProcessedMessagesTo(...)` enabled the heartbeats never reach the audit
queue. (Harmless when auditing is off: the audit pipeline isn't invoked.)

Heartbeats are only sent when a status registry is present (i.e. the health checks,
or `AddNServiceBusWarmUp()`, are registered on the host). If you enable
`EnableLivenessHeartbeat(...)` on an endpoint whose host doesn't consume liveness,
it is a no-op (nothing sends), so the same shared endpoint configuration can be
reused by hosts that don't expose `/health`.

## Logging health transitions

Health status is also logged, on **transitions** only (deduped: once per change, not per
probe): a `Warning` when an endpoint becomes unhealthy (stopped, or its heartbeat goes stale)
and an `Information` when it recovers. `Starting`/`Ready` are not logged; that's normal
warm-up, handled by readiness gating.

This is **on by default** whenever the health checks are registered: the check logs the
transition the next time it runs (i.e. on a probe), via a small singleton that remembers each
endpoint's last state.

For hosts that may go long stretches without probing `/health`, or to catch a hung endpoint
proactively, add the optional background monitor, which evaluates on a timer and logs the same
transitions even with no probe:

```csharp
builder.Services.AddNServiceBusHealthMonitor();                       // default: every 30s
builder.Services.AddNServiceBusHealthMonitor(TimeSpan.FromSeconds(10));
```

Both paths drive the same log, so enabling the monitor alongside the checks does not double-log.

## Handler registration ([Handler] / source generation)

`EndpointHeartbeatHandler` is a NServiceBus 10.2 `[Handler]` POCO: it does **not**
implement `IHandleMessages<T>`, so assembly scanning never discovers it. The
NServiceBus source generator emits an adapter plus a C# interceptor that rewrites
the package's `endpointConfiguration.AddHandler<EndpointHeartbeatHandler>()` call
into the generated, trim-safe registration (which also registers the message
type). Registering it explicitly means heartbeats work regardless of the user's
scanning setting, with no risk of double registration. `EndpointHeartbeat` itself
is a plain POCO; the generator registers it as a message, so no `IMessage` marker
is needed.
