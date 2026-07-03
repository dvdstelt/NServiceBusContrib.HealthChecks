# NServiceBusContrib

Community add-ons for [NServiceBus](https://docs.particular.net/nservicebus/),
targeting .NET 10.

## Projects in this solution

| Project | What it does |
| ------- | ------------ |
| [`NServiceBusContrib.WarmUp`](NServiceBusContrib.WarmUp) | Blocks message processing until user-defined warm-up actions complete; tracks endpoint readiness. |
| [`NServiceBusContrib.HealthCheck`](NServiceBusContrib.HealthCheck) | Aggregates the readiness and heartbeat liveness of every endpoint in the process into one `/health` check. |
| [`NServiceBusContrib.WarmUp.Tests`](NServiceBusContrib.WarmUp.Tests) | xUnit tests for both packages, including endpoint integration tests. |

`HealthCheck` depends on `WarmUp`; the shared `IEndpointStatusRegistry` lives in
`WarmUp`.

## Build & test

```bash
dotnet build NServiceBusContrib.slnx
dotnet test NServiceBusContrib.slnx
```

Shared MSBuild settings (target framework, nullable, package metadata) live in
[`Directory.Build.props`](Directory.Build.props). The SDK version is pinned in
`../global.json`.

## Design docs

The design, with Mermaid diagrams, is under [`../docs`](../docs):

- [`architecture.md`](../docs/architecture.md) — how the two packages fit together.
- [`warmup.md`](../docs/warmup.md) — warm-up-before-pump and readiness states.
- [`healthcheck.md`](../docs/healthcheck.md) — `/health` aggregation and heartbeat liveness.
