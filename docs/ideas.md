# Add-on ideas

A backlog of NServiceBus add-on ideas. Each entry gets a status and, once it
moves past the idea stage, a link to its own design doc.

Status legend: 💡 idea · 📝 designing · 🚧 in progress · ✅ shipped

## Backlog

| Add-on | Status | Notes |
| ------ | ------ | ----- |
| WarmUp | ✅ | Block message processing until user-defined warm-up actions complete. See [warm-up-and-health.md](warm-up-and-health.md). |
| HealthCheck | ✅ | Aggregate `/health` over all endpoints, with readiness + heartbeat liveness. See [warm-up-and-health.md](warm-up-and-health.md). |

## Idea template

When fleshing out an idea, copy this into a new section:

```
### <Add-on name>

**Status:** 💡 idea

**Problem:** What gap or pain point does this address?

**Approach:** How would it hook into NServiceBus (behavior, feature, satellite,
pipeline step, mutator, ...)?

**Open questions:** Anything undecided.
```
