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

## Status

Early days. See [`docs/ideas.md`](docs/ideas.md) for the backlog of add-on ideas.
