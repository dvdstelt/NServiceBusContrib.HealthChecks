# NServiceBusContrib.HealthCheck

Aggregates the readiness of every NServiceBus endpoint in the process into a
single ASP.NET Core health check, suitable for a container `/health` endpoint.
A process may host multiple endpoints (NServiceBus 10.2+); if one faults while
the others keep running, the health check reports unhealthy.

```csharp
builder.Services
    .AddHealthChecks()
    .AddNServiceBusEndpoints();

// map it the standard way
app.MapHealthChecks("/health");
```

- **Healthy** — every endpoint has completed warm-up and is processing.
- **Unhealthy** — at least one endpoint is still starting or has stopped.

Readiness is driven by **NServiceBusContrib.WarmUp**, so enable warm-up on each
endpoint you want reflected (`endpointConfiguration.WarmUp(...)`).
