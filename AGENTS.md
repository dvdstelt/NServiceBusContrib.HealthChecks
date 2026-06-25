# Agent instructions

Repo-level guidance for any AI coding agent working in this repository.

## What this is

Community add-ons and extensions for NServiceBus. Each add-on lives under `src/`
as its own project (plus a matching test project). Design notes and the idea
backlog live under `docs/`.

## Conventions

- Target .NET 10 (pinned in `global.json`, `rollForward: latestFeature`).
- Nullable reference types enabled; use current C# language features.
- Follow [SemVer](https://semver.org/) for package versions.
- Keep each add-on self-contained: one project per package, named
  `NServiceBusContrib.<AddonName>`.
- Add a test project (`NServiceBusContrib.<AddonName>.Tests`) under `tests/`
  for each add-on.

## Build & test

```bash
dotnet build
dotnet test
```

## Before committing

- Build and run tests.
- Keep commits focused: one logical change per commit.
- Do not push to `main` directly.
