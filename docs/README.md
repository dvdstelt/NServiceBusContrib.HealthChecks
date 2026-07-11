# Docs

Design notes, diagrams, and the idea backlog for the NServiceBus add-ons in this
repo. Diagrams are [Mermaid](https://mermaid.js.org/) and render on GitHub.

- [`architecture.md`](architecture.md): how the two packages fit together
  (component diagram, package boundaries, multi-endpoint host).
- [`warmup.md`](warmup.md): NServiceBusContrib.WarmUp design: warm-up-before-pump
  sequence and readiness state machine.
- [`healthcheck.md`](healthcheck.md): NServiceBusContrib.HealthCheck design:
  `/health` aggregation flow and heartbeat liveness.
- [`ideas.md`](ideas.md): backlog of add-on ideas and their status.

As an add-on moves from idea to implementation, capture its design notes in a
dedicated file here and link it from `ideas.md`.
