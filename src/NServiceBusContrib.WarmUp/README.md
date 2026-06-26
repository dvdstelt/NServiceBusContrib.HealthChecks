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
    warmup.Run<PrimeConnectionPoolTask>();   // resolved from DI, runs in order
});
```

Tasks are configured on the endpoint, so there is no endpoint-name string to pass.
`warmup.Run<T>()` resolves the task (and its dependencies) from the endpoint's
service provider.

Works in single- and multi-endpoint hosts (including hosts that disable assembly
scanning). Pairs with **NServiceBusContrib.HealthCheck** to expose endpoint
readiness over a `/health` endpoint.
