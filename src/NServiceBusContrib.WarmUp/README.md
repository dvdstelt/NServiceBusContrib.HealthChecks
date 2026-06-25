# NServiceBusContrib.WarmUp

Blocks NServiceBus message processing until user-defined warm-up actions have
completed. Warm-up runs as a `FeatureStartupTask`, so the message pump only opens
once your actions finish, priming caches, connection pools, and JIT paths before
the first real message arrives.

```csharp
var endpointConfiguration = new EndpointConfiguration("Sales");

endpointConfiguration.WarmUp(warmup =>
{
    warmup.Run(async cancellationToken => await Cache.PrimeAsync(cancellationToken));
    warmup.Run<PrimeConnectionPoolTask>();   // resolved from DI
});
```

Or register tasks against the service collection:

```csharp
builder.Services.AddNServiceBusWarmUpTask<PrimeConnectionPoolTask>("Sales");
```

Works in single- and multi-endpoint hosts (including hosts that disable assembly
scanning). Pairs with **NServiceBusContrib.HealthCheck** to expose endpoint
readiness over a `/health` endpoint.
