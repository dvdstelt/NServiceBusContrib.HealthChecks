# Agent instructions

Repo-level guidance for any AI coding agent working in this repository.

## What this is

Community add-ons and extensions for NServiceBus. All code lives under `src/`:
the solution (`src/NServiceBusContrib.slnx`), one project per add-on, and the
tests under `src/tests/`. Design notes, diagrams, and the idea backlog live under
`docs/`. Only `global.json`, the README, and `AGENTS.md` sit at the repo root.

## Conventions

- Target .NET 10 (pinned in `global.json`, `rollForward: latestFeature`).
- Nullable reference types enabled; use current C# language features.
- Follow [SemVer](https://semver.org/) for package versions.
- Keep each add-on self-contained: one project per package, named
  `NServiceBusContrib.<AddonName>` under `src/`.
- Add a test project (`NServiceBusContrib.<AddonName>.Tests`) under `src/tests/`
  for each add-on.
- Shared MSBuild settings live in `src/Directory.Build.props`.

## Build & test

```bash
dotnet build src/NServiceBusContrib.slnx
dotnet test src/NServiceBusContrib.slnx
```

## Before committing

- Build and run tests.
- Keep commits focused: one logical change per commit.
- Do not push to `main` directly.
