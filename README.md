# NServiceBus Contrib

A collection of community add-ons and extensions for [NServiceBus](https://docs.particular.net/nservicebus/).

## Layout

| Path     | Purpose                                                                 |
| -------- | ----------------------------------------------------------------------- |
| `src/`   | All code: the solution, the add-on packages, and the test project.      |
| `docs/`  | Design notes, diagrams, and the idea backlog.                           |

## Getting started

The repo targets .NET 10 (pinned via [`global.json`](global.json) with
`rollForward: latestFeature`).

```bash
dotnet build src/NServiceBusContrib.slnx
dotnet test src/NServiceBusContrib.slnx
```

## Add-ons

| Package | Description |
| ------- | ----------- |
| [`NServiceBusContrib.WarmUp`](src/NServiceBusContrib.WarmUp) | Block message processing until user-defined warm-up actions complete. |
| [`NServiceBusContrib.HealthCheck`](src/NServiceBusContrib.HealthCheck) | Aggregate the readiness of every endpoint in the process into one `/health` check. |

## Docs

| Doc | Contents |
| --- | -------- |
| [`docs/architecture.md`](docs/architecture.md) | How the two packages fit together (component diagram, multi-endpoint host). |
| [`docs/warmup.md`](docs/warmup.md) | WarmUp design: warm-up-before-pump, readiness states. |
| [`docs/healthcheck.md`](docs/healthcheck.md) | HealthCheck design: aggregation and heartbeat liveness. |
| [`docs/ideas.md`](docs/ideas.md) | Backlog of add-on ideas. |

## Status

The warm-up and health-check add-ons cover readiness (warm-up gating) and
heartbeat-based liveness. See [`docs/architecture.md`](docs/architecture.md) for
what's built and what's next.
