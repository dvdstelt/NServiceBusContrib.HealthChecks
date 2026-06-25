# NServiceBus Contrib

A collection of community add-ons and extensions for [NServiceBus](https://docs.particular.net/nservicebus/).

## Layout

| Path     | Purpose                                                            |
| -------- | ------------------------------------------------------------------ |
| `src/`   | Source code for the add-on packages and their tests.               |
| `docs/`  | Ideas, design notes, and plans for current and future add-ons.     |

## Getting started

The repo targets .NET 10 (pinned via [`global.json`](global.json) with
`rollForward: latestFeature`).

```bash
dotnet build
dotnet test
```

## Add-ons

| Package | Description |
| ------- | ----------- |
| [`NServiceBusContrib.WarmUp`](src/NServiceBusContrib.WarmUp) | Block message processing until user-defined warm-up actions complete. |
| [`NServiceBusContrib.HealthCheck`](src/NServiceBusContrib.HealthCheck) | Aggregate the readiness of every endpoint in the process into one `/health` check. |

See [`docs/warm-up-and-health.md`](docs/warm-up-and-health.md) for the design, and
[`docs/ideas.md`](docs/ideas.md) for the backlog.

## Status

The warm-up and health-check add-ons cover readiness (warm-up gating) and
heartbeat-based liveness. See [`docs/warm-up-and-health.md`](docs/warm-up-and-health.md)
for what's built and what's next.
