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

## Publishing

Publishing is tag-driven via GitHub Actions ([`.github/workflows/publish.yml`](.github/workflows/publish.yml)).
The package version is derived from the git tag by [MinVer](https://github.com/adamralph/minver),
so pushing a SemVer tag builds, tests, and pushes both packages to NuGet at that
version:

```bash
# stable release from main
git tag 1.0.0
git push origin 1.0.0

# prerelease from a branch
git tag 1.1.0-alpha.1
git push origin 1.1.0-alpha.1
```

Both packages are versioned in lockstep from the tag (so `HealthCheck 1.0.0`
depends on `WarmUp 1.0.0`). Set the `NUGET_API_KEY` repository secret for the push
to succeed. Untagged local builds get a MinVer height-based prerelease version
(e.g. `0.0.0-alpha.0.N`); there is no `<Version>` in source.

## Status

The warm-up and health-check add-ons cover readiness (warm-up gating) and
heartbeat-based liveness. See [`docs/architecture.md`](docs/architecture.md) for
what's built and what's next.
