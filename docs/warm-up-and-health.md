# Warm-up & Health Checks

Design notes for two related add-ons:

- **NServiceBusContrib.WarmUp** — block message processing until user-defined
  warm-up actions complete.
- **NServiceBusContrib.HealthCheck** — expose a single `/health` endpoint that
  aggregates the readiness of every NServiceBus endpoint in the process.

Origin: [forum thread on executing warm-up/start-up tasks before message
processing begins](https://discuss.particular.net/t/executing-warm-up-start-up-tasks-healthchecks-before-message-processing-begins/4586).

## Problem

1. Some handlers are latency-sensitive. Caches, connection pools, JIT paths,
   etc. should be primed *before* the endpoint starts pulling messages, so the
   first real message isn't the one that pays the cold-start cost.
2. Containers (Docker/Kubernetes) want a `/health` URL to gate traffic and
   detect crashes. A process may host **multiple** NServiceBus endpoints (made
   practical by NServiceBus 10.2's `AddNServiceBusEndpoint`). If one endpoint
   faults while the others keep running, the single `/health` must reflect that.

## Key mechanism: warm-up before the pump

NServiceBus runs all `FeatureStartupTask`s during `Endpoint.Start`, and the
message pump only begins receiving **after** those tasks complete. That is the
canonical, per-endpoint hook to delay processing — more robust than relying on
`IHostedService` start ordering, and it works whether the endpoint is hosted
standalone or as one of many via `AddNServiceBusEndpoint`.

So warm-up is implemented as an NServiceBus `Feature` that registers a
`FeatureStartupTask`:

```
Endpoint.Start
  -> Feature startup tasks run (our warm-up actions execute here)  <-- pump still closed
  -> message pump opens                                            <-- processing begins
```

## NServiceBusContrib.WarmUp

Dependencies: `NServiceBus` only (plus `Microsoft.Extensions.DependencyInjection.Abstractions`).
No web or health-check dependencies.

### API — EndpointConfiguration extension (primary)

```csharp
var endpointConfiguration = new EndpointConfiguration("Sales");

endpointConfiguration.WarmUp(warmup =>
{
    // 1. simple lambda
    warmup.Run(async cancellationToken => await Cache.PrimeAsync(cancellationToken));

    // 2. lambda with access to the endpoint's IServiceProvider
    warmup.Run(async (services, cancellationToken) =>
    {
        var db = services.GetRequiredService<MyDbContext>();
        await db.Database.CanConnectAsync(cancellationToken);
    });

    // 3. a typed task resolved from DI
    warmup.Run<PrimeConnectionPoolTask>();
});
```

Actions run **sequentially in registration order** so dependencies between them
are predictable. Any exception fails endpoint start (the pump never opens) —
warming up "half way" and then processing is worse than failing fast.

```csharp
public interface IWarmUpTask
{
    Task WarmUpAsync(CancellationToken cancellationToken);
}
```

### API — IServiceCollection (DI-centric)

For hosts that prefer to register everything against the service collection:

```csharp
builder.Services.AddNServiceBusWarmUpTask<PrimeConnectionPoolTask>("Sales");
```

The feature still has to be enabled on the endpoint; these DI-registered tasks
are then picked up and run alongside any configured inline. `config.WarmUp()`
(no argument) enables the feature for the endpoint without configuring inline
actions.

### Enabling and assembly scanning

The warm-up `Feature` is enabled explicitly from `config.WarmUp(...)` via
`EnableFeature<WarmUpFeature>()` (this is the pattern NServiceBus 11 will
require; `EnableByDefault` is being retired). Because `EnableFeature<T>()` adds
the feature directly rather than relying on discovery, it works whether or not
assembly scanning is enabled, so multi-endpoint hosts (like
`OmNomNom.AllInOne`) that disable scanning are fully supported. The trade-off is
that warm-up is opt-in per endpoint: an endpoint that never calls
`config.WarmUp(...)`/`config.WarmUp()` gets no warm-up and is not tracked for
readiness.

## NServiceBusContrib.HealthCheck

Dependencies: `NServiceBusContrib.WarmUp` + `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions`.

### Readiness registry (lives in WarmUp)

A host-level singleton `IEndpointReadinessRegistry` tracks each endpoint's
state. Because every endpoint in a multi-endpoint host shares the host's
singletons (each endpoint gets its own *scoped* container built on top of the
host services), one registry sees them all.

The warm-up startup task drives the state:

| Hook                       | State        |
| -------------------------- | ------------ |
| startup task `OnStart` begins | `Starting`   |
| warm-up actions complete      | `Ready`      |
| startup task `OnStop`         | `Stopped`    |

`Stopped` covers graceful shutdown **and** a crash: when NServiceBus tears an
endpoint down after a critical error, the feature's `OnStop` runs and the
registry flips that endpoint out of `Ready`. So a single faulted endpoint among
many makes `/health` report unhealthy — without the package overriding the
user's critical-error action.

### Health check

```csharp
builder.Services
    .AddHealthChecks()
    .AddNServiceBusEndpoints();   // reads IEndpointReadinessRegistry

// consumer maps it the standard ASP.NET way
app.MapHealthChecks("/health");
```

- **Healthy** — every registered endpoint is `Ready`.
- **Unhealthy** — at least one endpoint is `Starting` or `Stopped` (Docker keeps
  the container out of rotation until all endpoints are warm).
- The result `data` carries a per-endpoint breakdown so the cause is visible.

## Phasing

### Phase 1 (this work)

- Warm-up feature + `IWarmUpTask` + both API surfaces.
- Readiness registry driven by the warm-up startup task lifecycle.
- Aggregate `/health` over all endpoints (Ready vs not-Ready).
- "Healthy once started" — readiness is the signal; no liveness probing yet.

### Phase 2 (designed-for, not built)

- **Heartbeat liveness**: each endpoint periodically sends a tracking message to
  its own queue. Successful processing refreshes a timestamp. If the timestamp
  goes stale (process hung / pump died without a clean stop), the registry marks
  the endpoint unhealthy even though it never reached `OnStop`. This catches
  failure modes a lifecycle hook can't see.
- Configurable thresholds (heartbeat interval, staleness window).
- Optional split of `/health` (liveness) vs `/health/ready` (readiness) using
  health-check tags.
