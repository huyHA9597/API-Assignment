# Tasks: Telemetry and Observability via .NET Aspire

Plan: [tasks/plan-telemetry.md](plan-telemetry.md) · Spec: [docs/specs/telemetry-observability-aspire.md](../docs/specs/telemetry-observability-aspire.md)

## Phase 1: ServiceDefaults foundation

### Task 1: Scaffold `EventGoNoGoAdvisor.ServiceDefaults` project

**Description:** Create `src/EventGoNoGoAdvisor.ServiceDefaults`, a plain
`net8.0` class-library-style SDK project (`Nullable` enabled,
`ImplicitUsings` enabled) carrying the standard Aspire service-defaults
extension methods: `AddServiceDefaults()`, `ConfigureOpenTelemetry()`,
`AddOpenTelemetryExporters()` (OTLP exporter registered only when
`OTEL_EXPORTER_OTLP_ENDPOINT` is set), `AddDefaultHealthChecks()`, and
`MapDefaultEndpoints()` (`/health` + `/alive`, mapped only in
`Development`). No custom metrics or spans — auto-instrumentation only, per
the spec.

**Acceptance criteria:**
- [ ] `EventGoNoGoAdvisor.ServiceDefaults.csproj` targets `net8.0` with
      `Nullable`/`ImplicitUsings` enabled, referencing exactly:
      `Microsoft.Extensions.ServiceDiscovery`,
      `OpenTelemetry.Extensions.Hosting`,
      `OpenTelemetry.Exporter.OpenTelemetryProtocol`,
      `OpenTelemetry.Instrumentation.AspNetCore`,
      `OpenTelemetry.Instrumentation.Http`,
      `OpenTelemetry.Instrumentation.Runtime`,
      `Microsoft.Extensions.Diagnostics.HealthChecks`
- [ ] `Extensions.cs` exposes `AddServiceDefaults(this IHostApplicationBuilder)`
      and `MapDefaultEndpoints(this WebApplication)`, matching the standard
      Aspire template shape shown in the spec's Code Style section
- [ ] Project added to `EventGoNoGoAdvisor.sln`

**Verification:**
- [ ] `dotnet build` succeeds for the solution
- [ ] `dotnet restore` completes without network/package errors

**Dependencies:** None

**Files likely touched:**
- `EventGoNoGoAdvisor.sln`
- `src/EventGoNoGoAdvisor.ServiceDefaults/EventGoNoGoAdvisor.ServiceDefaults.csproj`
- `src/EventGoNoGoAdvisor.ServiceDefaults/Extensions.cs`

**Estimated scope:** Small

---

## Phase 2: Wire into the API

### Task 2: Reference `ServiceDefaults` from `Api` and wire `Program.cs`

**Description:** Add a project reference from
`EventGoNoGoAdvisor.Api.csproj` to `ServiceDefaults`, then call
`builder.AddServiceDefaults()` early in `Program.cs` and
`app.MapDefaultEndpoints()` alongside the existing endpoint mapping. No
other `Program.cs` behavior changes — existing middleware order (exception
handler, HSTS, security headers, rate limiter, Swagger) stays as-is.

**Acceptance criteria:**
- [ ] `Api` project references `ServiceDefaults`
- [ ] `Program.cs` calls `builder.AddServiceDefaults()` and
      `app.MapDefaultEndpoints()`
- [ ] No changes to `Endpoints/`, `Rules/`, `Clients/`, or `Models/`

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes with the same pass count as before this task
      (zero regressions)
- [ ] `dotnet run --project src/EventGoNoGoAdvisor.Api` (no
      `OTEL_EXPORTER_OTLP_ENDPOINT` set) starts cleanly with no exporter
      errors/warnings in the console
- [ ] Manual: `POST /event-advisor` with the README's sample payload still
      returns the same response shape as documented
- [ ] Manual: `/swagger` still loads
- [ ] Manual: `/health` and `/alive` return `200 OK` while running in
      Development

**Dependencies:** Task 1

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/EventGoNoGoAdvisor.Api.csproj`
- `src/EventGoNoGoAdvisor.Api/Program.cs`

**Estimated scope:** Small

---

## Phase 3: AppHost orchestration

### Task 3: Scaffold `EventGoNoGoAdvisor.AppHost` project

**Description:** Create `src/EventGoNoGoAdvisor.AppHost` using the Aspire
AppHost SDK (`Aspire.AppHost.Sdk` + `Aspire.Hosting.AppHost`, latest stable
9.x), with a project reference to `EventGoNoGoAdvisor.Api` so the generated
`Projects.EventGoNoGoAdvisor_Api` type is available. The AppHost registers
exactly one resource — the API project — and no other Aspire-modeled
resources (Redis, Postgres, etc.), per the spec.

**Acceptance criteria:**
- [ ] `EventGoNoGoAdvisor.AppHost.csproj` uses the Aspire AppHost SDK and
      references `EventGoNoGoAdvisor.Api`
- [ ] AppHost entry point calls
      `builder.AddProject<Projects.EventGoNoGoAdvisor_Api>("event-advisor-api")`
      and nothing else
- [ ] Project added to `EventGoNoGoAdvisor.sln`

**Verification:**
- [ ] `dotnet restore` succeeds (flags any package/SDK resolution problem
      immediately — see plan's risk list)
- [ ] `dotnet build` succeeds for the full solution
- [ ] `dotnet run --project src/EventGoNoGoAdvisor.AppHost` starts and
      prints a reachable Aspire Dashboard URL
- [ ] Manual: the `event-advisor-api` resource reaches a running state in
      the dashboard

**Dependencies:** Task 2

**Files likely touched:**
- `EventGoNoGoAdvisor.sln`
- `src/EventGoNoGoAdvisor.AppHost/EventGoNoGoAdvisor.AppHost.csproj`
- `src/EventGoNoGoAdvisor.AppHost/AppHost.cs`

**Estimated scope:** Small

---

## Phase 4: Verification

### Task 4: Manual end-to-end telemetry smoke test

**Description:** Spike task, no production code. Run the AppHost, open the
Aspire Dashboard, send `POST /event-advisor` with the README's sample
payload, and confirm the full telemetry picture for that one request.
Record concrete findings (trace structure, metric names observed, log
correlation) the same way the base feature's live-API smoke test findings
were recorded.

**Acceptance criteria:**
- [ ] One trace for the request shows the inbound ASP.NET Core span plus
      child spans for both the Open-Meteo and Nager.Date outbound calls
- [ ] Dashboard's Metrics view shows ASP.NET Core request metrics,
      `HttpClient` metrics, and .NET runtime metrics populated from this
      run
- [ ] Dashboard's Logs view shows log entries correlated to the request's
      trace id
- [ ] `/health` and `/alive`, hit through the AppHost-launched instance,
      return `200`

**Verification:**
- [ ] Findings (what was actually observed — trace shape, metric names,
      any surprises) recorded as notes in this task

**Dependencies:** Task 3

**Files likely touched:**
- `tasks/todo-telemetry.md` (this file — findings recorded in this task's
  notes)

**Estimated scope:** Small

---

## Phase 5: Docs

### Task 5: README and ADR updates

**Description:** Update `README.md`'s "Running it" section with the
`dotnet run --project src/EventGoNoGoAdvisor.AppHost` command and a new
"Observability" subsection (what's instrumented, how to view it, and that
the standalone run path is unaffected). Add a new ADR under
`docs/decisions/` (next number after the existing 001–003) documenting the
choice of Aspire `ServiceDefaults` + OTLP + local Dashboard, auto-
instrumentation only, over hand-rolled OpenTelemetry wiring or a
third-party APM — and update `docs/decisions/README.md`'s index.

**Acceptance criteria:**
- [ ] README documents the AppHost run command, what's instrumented, and
      links the new ADR
- [ ] New ADR file follows the existing ADRs' format/style and is linked
      from `docs/decisions/README.md`

**Verification:**
- [ ] Manual: README instructions match what Task 3/4 actually verified
      (no documenting unverified behavior)

**Dependencies:** Task 4

**Files likely touched:**
- `README.md`
- `docs/decisions/004-telemetry-via-aspire-service-defaults.md`
- `docs/decisions/README.md`

**Estimated scope:** Small
