# ADR-004: Telemetry via Aspire ServiceDefaults + OTLP, auto-instrumentation only

## Status
Accepted

## Date
2026-10-26

## Context
Per [docs/specs/telemetry-observability-aspire.md](../specs/telemetry-observability-aspire.md),
the goal was to demonstrate production-style observability — traces,
metrics, and correlated logs — for the existing `/event-advisor` endpoint,
without turning this take-home into a resilience-engineering or APM
showcase. Only the .NET 10 SDK is installed locally, with no Aspire
workload (confirmed via `dotnet workload list`); Aspire's NuGet-based
project templates (`Aspire.ProjectTemplates`, installed for this task) are
sufficient — no workload install required.

## Decision
Add the standard .NET Aspire pattern: a new `EventGoNoGoAdvisor.ServiceDefaults`
project carrying OpenTelemetry auto-instrumentation (ASP.NET Core requests,
outgoing `HttpClient` calls, .NET runtime metrics) and `/health`/`/alive`
health checks, referenced by the `Api` project via `builder.AddServiceDefaults()`
/ `app.MapDefaultEndpoints()`; and a new `EventGoNoGoAdvisor.AppHost`
project that orchestrates the `Api` as its only resource and launches the
local Aspire Dashboard for viewing traces/metrics/logs during development.

Two deliberate deviations from the stock `aspire-servicedefaults` /
`aspire-apphost` templates (Aspire.ProjectTemplates 13.6.4):
- **`TargetFramework` reset to `net8.0`** on both new projects (the
  templates default to `net10.0`) to stay consistent with the rest of the
  solution — only the .NET 10 SDK is installed, but the app itself targets
  `net8.0`.
- **No `Microsoft.Extensions.Http.Resilience` / standard resilience
  handler.** The template wires `AddStandardResilienceHandler()` into
  `ConfigureHttpClientDefaults()` for every `HttpClient`, which would
  silently add retry/circuit-breaker behavior to the existing
  `OpenMeteoWeatherClient`/`NagerDateHolidayClient` typed clients —
  contradicting [ADR-002](002-keyless-upstream-apis-no-caching-or-retry.md)'s
  explicit "no caching, retry on transient failure" decision. Removed the
  package reference and the `ConfigureHttpClientDefaults` block entirely;
  kept `AddServiceDiscovery()` as a bare registration (unused today — no
  service-to-service calls exist — but matches the template shape for any
  future resource).

OTLP export targets the local Aspire Dashboard only (via the
`OTEL_EXPORTER_OTLP_ENDPOINT` env var the AppHost injects); the exporter is
only registered when that variable is set, so `dotnet run --project
src/EventGoNoGoAdvisor.Api` (no AppHost) keeps working exactly as before,
with no exporter errors.

## Alternatives Considered

### Hand-rolled OpenTelemetry wiring directly in `Program.cs`, no Aspire
- Pros: no new projects, no Aspire template/SDK dependency.
- Cons: reinvents what `ServiceDefaults` already standardizes (conditional
  OTLP registration, health-check conventions), and the request explicitly
  asked for ".NET Aspire" as the mechanism.
- Rejected: the standard Aspire shape is also what a reviewer would
  recognize and expect.

### A custom business metric (e.g. a `GO`/`NO_GO` verdict counter)
- Pros: a more "observability-complete" story — not just infrastructure
  metrics but a domain signal.
- Cons: would touch `Endpoints/EventAdvisorEndpoints.cs`, adding app code
  and a maintenance surface beyond what Aspire generates for free.
- Rejected per explicit confirmation during spec review: scope stays to
  Aspire's free auto-instrumentation only.

### Exporting to a third-party APM backend (Azure Monitor, Datadog, Honeycomb)
- Pros: durable/shareable telemetry beyond a local dev session.
- Cons: requires an account/API key — the same setup-friction problem
  ADR-002 rejected for the upstream weather/holiday APIs, now applied to
  telemetry.
- Rejected: local Aspire Dashboard via OTLP is sufficient to demonstrate
  the instrumentation; the commented-out Azure Monitor exporter block in
  the generated `Extensions.cs` is left as-is (inert, not wired up) for
  reference.

### Keep the template's default resilience handler
- Pros: less code to touch; matches what most Aspire tutorials ship.
- Cons: directly contradicts ADR-002's retry-free decision for the
  upstream clients, which was deliberate, not an oversight.
- Rejected; see Decision above.

## Consequences
- `dotnet run --project src/EventGoNoGoAdvisor.AppHost` is now the way to
  see the service's traces, metrics, and logs during development; `dotnet
  run --project src/EventGoNoGoAdvisor.Api` continues to work standalone
  for anyone who just wants the API without the dashboard.
- Three projects now build instead of one (`Api`, `ServiceDefaults`,
  `AppHost`), plus the test project — still a small solution, and
  `Rules/`, `Clients/`, `Endpoints/`, and `Models/` are untouched.
- If a custom business metric or a third-party telemetry backend is wanted
  later, both were deliberately deferred here (see Alternatives), not
  foreclosed.
