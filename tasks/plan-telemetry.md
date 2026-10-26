# Implementation Plan: Telemetry and Observability via .NET Aspire

Spec: [docs/specs/telemetry-observability-aspire.md](../docs/specs/telemetry-observability-aspire.md)

## Overview

Add the standard Aspire "service defaults" pattern to the existing Event
Go/No-Go Advisor API: a new `EventGoNoGoAdvisor.ServiceDefaults` project
carrying OpenTelemetry auto-instrumentation (ASP.NET Core, outgoing
`HttpClient`, .NET runtime) plus `/health`/`/alive` health checks, and a new
`EventGoNoGoAdvisor.AppHost` project that orchestrates the API and launches
the Aspire Dashboard. Build order is foundation-first: `ServiceDefaults`
first (so it can be verified in isolation), then wired into the `Api`
project (verify the standalone run path still works unchanged), then the
`AppHost` added on top (verify the orchestrated/dashboard path), then one
manual end-to-end telemetry smoke test, then docs. No business logic
(`Rules/`, `Clients/`, `Endpoints/`) is touched at any step.

## Architecture Decisions

- **`ServiceDefaults` is built and verified before anything references it.**
  Isolates any package/SDK resolution problems from the `Api`/`AppHost`
  wiring steps.
- **The standalone run path is explicitly re-verified right after wiring
  `ServiceDefaults` into `Api` (Task 2), before `AppHost` exists.** This is
  the spec's "Always" boundary (don't break `dotnet run --project
  .../Api`) and the cheapest point to catch an exporter crashing on missing
  `OTEL_EXPORTER_OTLP_ENDPOINT`.
- **No resilience-handler package.** The common Aspire template also wires
  `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler`)
  into `ConfigureHttpClientDefaults`. The spec's package list omits it and
  the "ask first" boundary covers packages beyond that list, so
  `ServiceDefaults` only adds `AddServiceDiscovery()` — no retry/circuit-
  breaker behavior change to the existing `OpenMeteoWeatherClient`/
  `NagerDateHolidayClient` typed clients, which is also consistent with ADR
  002 (keyless upstream APIs, no caching or retry) already on record.
- **Health checks and OTLP exporter registration follow the template's own
  guard conditions exactly** (health endpoints mapped only in
  `Development`; OTLP exporter only registered when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set) rather than custom conditions, so
  behavior matches what's documented upstream and in the dashboard.
- **The manual smoke test is its own task, after both projects exist**,
  mirroring the base feature's precedent (`tasks/todo.md` Task 2/12) of
  treating live/dashboard verification as a tracked task rather than an
  implicit part of coding.
- **README + ADR land last**, once the smoke test has confirmed actual
  behavior (dashboard contents, run commands) rather than documenting
  intent ahead of verification.

## Task List

### Phase 1: ServiceDefaults foundation

- [x] Task 1: Scaffold `EventGoNoGoAdvisor.ServiceDefaults` project

### Checkpoint: ServiceDefaults
- [x] `dotnet build` succeeds for the new project and the whole solution
- [x] No other project references it yet — isolated build only

**Deviation from spec, corrected:** the stock `aspire-servicedefaults`
template (Aspire.ProjectTemplates 13.6.4, installed for this task) targets
`net10.0` and wires `Microsoft.Extensions.Http.Resilience`'s standard
resilience handler onto every `HttpClient` by default. Both were reverted
to match the approved spec: `TargetFramework` set back to `net8.0`, and the
resilience-handler package/`ConfigureHttpClientDefaults` block removed so
the existing retry-free `OpenMeteoWeatherClient`/`NagerDateHolidayClient`
(ADR 002) aren't silently given retry behavior.

### Phase 2: Wire into the API

- [x] Task 2: Reference `ServiceDefaults` from `Api` and wire `Program.cs`

### Checkpoint: API wiring
- [x] `dotnet build` succeeds
- [x] `dotnet test` passes unchanged (19/19, zero regressions)
- [x] `dotnet run --project src/EventGoNoGoAdvisor.Api` (standalone, no
      `OTEL_EXPORTER_OTLP_ENDPOINT`) starts cleanly, no exporter errors in
      the console
- [x] `/swagger` and `POST /event-advisor` behave exactly as before
- [x] `/health` and `/alive` return `200` in Development

### Phase 3: AppHost orchestration

- [ ] Task 3: Scaffold `EventGoNoGoAdvisor.AppHost` project

### Checkpoint: AppHost
- [ ] `dotnet build` succeeds for the full solution (3 app projects + test
      project)
- [ ] `dotnet run --project src/EventGoNoGoAdvisor.AppHost` starts, prints a
      reachable Aspire Dashboard URL, and the `event-advisor-api` resource
      reaches a running state

### Phase 4: Verification

- [ ] Task 4: Manual end-to-end telemetry smoke test

### Checkpoint: Telemetry verified
- [ ] One trace per `POST /event-advisor` request shows the inbound span
      plus child spans for both outbound calls
- [ ] ASP.NET Core, `HttpClient`, and runtime metrics populate in the
      dashboard
- [ ] Logs correlate to the trace id for that request

### Phase 5: Docs

- [ ] Task 5: README and ADR updates

### Checkpoint: Complete
- [ ] All Success Criteria in `docs/specs/telemetry-observability-aspire.md`
      are met
- [ ] Ready for human review

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `Aspire.AppHost.Sdk`/`Aspire.Hosting.AppHost` package resolution needs network access to nuget.org and a compatible SDK resolver | Medium — blocks Task 3 entirely | Run `dotnet restore` right after creating the AppHost project (part of Task 3), before writing orchestration code, so a restore failure surfaces immediately rather than after other work |
| Aspire 9.x tooling has a minimum .NET SDK requirement that could exceed what's installed | Low — only .NET 10 SDK is present, which should exceed any Aspire 9.x floor | Confirm `dotnet --version` meets the installed Aspire package's `global.json`/SDK requirement during Task 3; fall back to the newest Aspire 9.x patch that supports the installed SDK if not |
| OTLP exporter registration throws or logs noisily when `OTEL_EXPORTER_OTLP_ENDPOINT` is unset (standalone run) | Medium — would violate the spec's "never crash standalone" boundary | Task 2's checkpoint explicitly re-runs the standalone API and checks the console before moving on |
| Existing response-header middleware (CSP, X-Frame-Options, HSTS) in `Program.cs` conflicts with Aspire's mapped `/health`/`/alive` endpoints | Low | Task 2 manually checks both endpoints return `200` after wiring, not just that the app starts |
| Manual dashboard verification (Task 4) is inherently harder to automate/reproduce than a unit test | Low — verification quality, not functionality | Record concrete findings (trace contents, metric names observed) in `tasks/todo-telemetry.md` Task 4 notes, same precedent as the base feature's live smoke tests |

## Open Questions

None blocking. Spec is approved with both prior open questions resolved
(auto-instrumentation only, Aspire 9.x latest).
