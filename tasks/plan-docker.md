# Implementation Plan: Docker Containerization

Spec: [docs/specs/docker-containerization.md](../docs/specs/docker-containerization.md)

Tasks tracked in [tasks/todo-docker.md](todo-docker.md) (following the
project's existing per-feature convention: see `plan-telemetry.md` /
`todo-telemetry.md` for the telemetry feature).

## Overview

Containerize `EventGoNoGoAdvisor.Api` with a multi-stage Dockerfile and a
docker-compose.yml that serves HTTP + HTTPS (dev cert), then wire
`EventGoNoGoAdvisor.AppHost` so the Api project resource is associated with
that Dockerfile for container-based publish — all without changing the
existing `dotnet run` inner dev loop. No business logic (`Rules/`,
`Clients/`, endpoints) changes.

## Architecture Decisions

- **Container defaults to `ASPNETCORE_ENVIRONMENT=Development`** — keeps
  Swagger and `/health`/`/alive` reachable (both are gated behind
  `IsDevelopment()` today). A hardened Production profile is explicit future
  work, not part of this plan (spec Decision 1).
- **Runtime image: `mcr.microsoft.com/dotnet/aspnet:8.0-alpine`**, build
  image: `mcr.microsoft.com/dotnet/sdk:8.0`. No native dependencies exist in
  this project, so the glibc/musl split between build and runtime stages is
  safe.
- **Docker build context is the repo root**, not `src/EventGoNoGoAdvisor.Api/`,
  because the Api project references `EventGoNoGoAdvisor.ServiceDefaults` via
  a relative `ProjectReference` — the Dockerfile needs both projects visible
  in its context.
- **HTTPS via a locally-generated dev cert**, mounted read-only into the
  container from the host's `dotnet dev-certs https` output — documented as
  a one-time manual step (spec Decision 3), not automated.
- **AppHost wiring is a research spike**, not a locked API call: the exact
  Aspire Hosting extension method for Dockerfile-based publish on the
  installed `Aspire.AppHost.Sdk/13.6.4` isn't confirmed yet (spec Decision 2).

## Dependency Graph

```
.dockerignore (Task 1)
    │
    ▼
Dockerfile, standalone build+run (Task 2)
    │
    ├──▶ docker-compose.yml, HTTPS dev-cert wiring (Task 3)
    │
    └──▶ AppHost.cs wired for Dockerfile-based publish (Task 4)
                │
                ▼
    Full verification pass: container + AppHost + dotnet test (Task 5)
                │
                ▼
        README "Running with Docker" section (Task 6)
```

Tasks 3 and 4 both depend only on Task 2 and can be done in either order (or
in parallel across two sessions) — they touch disjoint files
(`docker-compose.yml` vs. `AppHost.cs`).

## Task List

### Phase 1: Foundation
- [x] Task 1: Add `.dockerignore`
- [x] Task 2: Multi-stage Dockerfile, standalone build + run

### Checkpoint: Foundation
- [x] `docker build` succeeds from repo root
- [x] Container runs standalone over plain HTTP as a non-root user
- [x] Review with human before proceeding

### Phase 2: Compose + AppHost wiring
- [x] Task 3: `docker-compose.yml` with HTTPS dev-cert wiring
- [x] Task 4: Confirm Aspire Hosting API; wire AppHost for Dockerfile-based publish

### Checkpoint: Compose + AppHost
- [x] `docker compose up --build` serves HTTP + HTTPS, Swagger, health endpoints
- [x] `dotnet run --project src/EventGoNoGoAdvisor.AppHost` (plain) still works unchanged
- [x] Review with human before proceeding

### Phase 3: Verification & Docs
- [x] Task 5: Full verification pass (4 sample scenarios, `dotnet test`, both `dotnet run` loops)
- [x] Task 6: README "Running with Docker" section

### Checkpoint: Complete
- [x] All Success Criteria in the spec are met
- [x] `dotnet build` and `dotnet test` both succeed, unchanged
- [x] README is accurate and complete
- [ ] Ready for human review

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Aspire Hosting API for Dockerfile-based publish doesn't exist/work as expected on `Aspire.AppHost.Sdk/13.6.4` | Medium — Task 4 can't complete as scoped | Task 4 is explicitly a research spike; if the API doesn't support this cleanly, report back to the human with findings instead of improvising a workaround that risks breaking the local dev loop |
| Container's `ASPNETCORE_ENVIRONMENT=Development` default is an intentionally non-hardened choice | Low — known tradeoff, not a bug | Documented in README (Task 6) and spec Decision 1; hardened profile is tracked as separate future work |
| Self-signed dev cert isn't trusted by default inside the Alpine container's/host's trust store | Low — local-only, `curl -k` or browser "proceed anyway" works | Documented in README; acceptable for local/dev use, not a production concern |
| `.dockerignore` excludes something `dotnet restore`/`publish` actually needs (e.g. accidentally excluding `src/`) | Low — build breaks loudly at `docker build` time | Task 2's verification step (`docker build` succeeding) catches this immediately |

## Open Questions

None blocking — all four open questions from the spec were resolved before
this plan was written (see spec's "Decisions (resolved)" section).
