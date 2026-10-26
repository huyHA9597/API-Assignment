# Spec: Telemetry and Observability via .NET Aspire

Source: direct engineering request (no separate idea doc) — extends the
existing [Event Go/No-Go Advisor spec](event-go-no-go-advisor.md).

## Objective

Give the Event Go/No-Go Advisor API production-style observability —
distributed traces, metrics, and correlated structured logs — using the
standard .NET Aspire integration pattern (`ServiceDefaults` + `AppHost` +
OpenTelemetry + Aspire Dashboard), instead of hand-wiring OpenTelemetry
exporters directly into the API project.

**Who it's for:** the same audience as the base project — a reviewer
evaluating engineering judgment on a take-home. Observability here
demonstrates the ability to instrument a real ASP.NET Core service and reason
about its runtime behavior, not a production SRE setup for live traffic.

**Success looks like:** a reviewer runs one command, opens the Aspire
Dashboard in a browser, fires a `POST /event-advisor` request, and sees — for
that single request — an end-to-end trace spanning the inbound HTTP request
and both outbound calls to Open-Meteo and Nager.Date, request/HttpClient/
runtime metrics, and logs correlated to the trace by trace id. The existing
API, tests, and `dotnet run`/`dotnet test` workflow keep working unchanged
when Aspire isn't used.

## Assumptions

1. **Aspire version:** Aspire 9.x, NuGet-package based (`Aspire.AppHost.Sdk` +
   `Aspire.Hosting.AppHost`) — no `dotnet workload install aspire` needed;
   only the .NET 10 SDK is installed locally and no Aspire workload is
   present (confirmed via `dotnet workload list`).
2. **No new backing resources.** The AppHost orchestrates only the existing
   API project as a resource. Open-Meteo and Nager.Date stay plain outbound
   `HttpClient` calls (not modeled as Aspire resources) — they're external
   keyless public APIs, not something we provision.
3. **Telemetry sinks:** OTLP export to the local Aspire Dashboard only (via
   the `OTEL_EXPORTER_OTLP_ENDPOINT` env var the AppHost injects). No
   third-party backend (Datadog, Honeycomb, Azure Monitor, etc.) — out of
   scope for a take-home.
4. **TargetFramework stays `net8.0`** for the API project; only the new
   `AppHost` and `ServiceDefaults` projects are added. Aspire 9.x supports
   orchestrating a `net8.0` app from an AppHost project.
5. **Auto-instrumentation only, no custom business metrics.** ASP.NET Core
   requests, outgoing `HttpClient` calls, and .NET runtime metrics (GC,
   thread pool, exceptions) via the standard OpenTelemetry instrumentation
   packages the Aspire template wires into `ServiceDefaults` — nothing
   beyond what the template generates for free. No new `Meter`/`Counter`
   code in the `Api` project. **(Confirmed)**
6. **Standalone run still works.** `dotnet run --project src/EventGoNoGoAdvisor.Api`
   (no AppHost) keeps working exactly as today: `ServiceDefaults` only
   registers the OTLP exporter when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, per
   the standard Aspire template behavior, so running the API alone never
   errors for a missing collector.
7. **No new automated tests required.** The change is framework wiring only
   (DI/exporter registration, no new app code), so it carries the same test
   bar as the untested `Program.cs` — verified by a manual smoke test against
   the dashboard instead, same way the original upstream integrations were
   smoke-tested (see `tasks/todo.md` Task 2/12 precedent).
8. **Aspire 9.x, latest stable at implementation time.** **(Confirmed)**

→ Correct any of these now, or I'll proceed with them as stated.

## Tech Stack (additions)

- `Aspire.Hosting.AppHost` (new `EventGoNoGoAdvisor.AppHost` project) — local
  orchestration + Aspire Dashboard.
- `Microsoft.Extensions.ServiceDiscovery`, `OpenTelemetry.Extensions.Hosting`,
  `OpenTelemetry.Exporter.OpenTelemetryProtocol`,
  `OpenTelemetry.Instrumentation.AspNetCore`,
  `OpenTelemetry.Instrumentation.Http`,
  `OpenTelemetry.Instrumentation.Runtime`,
  `Microsoft.Extensions.Diagnostics.HealthChecks` (new
  `EventGoNoGoAdvisor.ServiceDefaults` project) — the standard Aspire
  "service defaults" template.
- No new packages in the `Api` or `Tests` projects beyond a project
  reference to `ServiceDefaults`.

## Commands (additions)

```
dotnet run --project src/EventGoNoGoAdvisor.AppHost   # launches API + Aspire Dashboard
dotnet build
dotnet test
dotnet run --project src/EventGoNoGoAdvisor.Api        # unchanged, standalone, no dashboard
```

The Aspire Dashboard URL and login token print to the AppHost's console on
startup (standard Aspire behavior).

## Project Structure (additions/changes)

```
EventGoNoGoAdvisor.sln                         -> add AppHost + ServiceDefaults projects
src/EventGoNoGoAdvisor.AppHost/
  AppHost.cs (or Program.cs)                   -> builder.AddProject<...Api>("event-advisor-api")
  EventGoNoGoAdvisor.AppHost.csproj
src/EventGoNoGoAdvisor.ServiceDefaults/
  Extensions.cs                                -> AddServiceDefaults(), MapDefaultEndpoints()
  EventGoNoGoAdvisor.ServiceDefaults.csproj
src/EventGoNoGoAdvisor.Api/
  Program.cs                                   -> builder.AddServiceDefaults(); app.MapDefaultEndpoints();
  EventGoNoGoAdvisor.Api.csproj                -> project reference to ServiceDefaults
```

`Endpoints/EventAdvisorEndpoints.cs` and the `Rules/`/`Clients/` layers are
untouched — this change is additive infrastructure only.

## Code Style (example)

Standard Aspire template shape for `ServiceDefaults/Extensions.cs` — this is
the entire scope of app-visible code change; no new app code in `Api`
beyond the two calls in `Program.cs`:

```csharp
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    builder.ConfigureOpenTelemetry();
    builder.AddDefaultHealthChecks();
    builder.Services.AddServiceDiscovery();
    return builder;
}

public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
{
    builder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);

    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation())
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());

    builder.AddOpenTelemetryExporters();
    return builder;
}
```

In `Program.cs`:

```csharp
builder.AddServiceDefaults();
// ...
app.MapDefaultEndpoints();
```

## Testing Strategy (additions)

- No new automated tests (framework wiring only, same bar as the untested
  `Program.cs` — no new business logic is introduced).
- Manual smoke test (documented in README, same pattern as the base spec's
  live-API smoke test): run the AppHost, hit `POST /event-advisor`, confirm
  in the Aspire Dashboard that one trace shows the inbound request + both
  outbound client calls, and that ASP.NET Core / HttpClient / runtime
  metrics are populated.
- Existing `dotnet test` suite must pass unchanged — zero regressions from
  adding `ServiceDefaults` to the API project.

## Boundaries

- **Always:** keep the existing `dotnet run --project src/EventGoNoGoAdvisor.Api`
  (no AppHost) and `dotnet test` workflows working exactly as before; keep
  `Rules/`, `Clients/`, and `Endpoints/` untouched — this change is additive
  infrastructure only; follow the standard Aspire template shape for
  `ServiceDefaults` rather than a bespoke OpenTelemetry setup; record an ADR
  for this decision per the project's existing `docs/decisions/` convention.
- **Ask first:** adding any NuGet package beyond the standard Aspire/
  OpenTelemetry set listed above; adding Aspire-modeled resources (Redis,
  Postgres, etc.) that don't correspond to something the app actually uses;
  exporting telemetry to any external/third-party backend; adding any
  custom `Meter`/business metric (explicitly out of scope per Assumption 5);
  adding `WebApplicationFactory`-based integration tests as part of this
  change (would be new test infrastructure, not just wiring).
- **Never:** send telemetry data to a non-local endpoint by default; add
  authentication requirements to `/health`/`/alive` that don't exist today
  on the main endpoint; let missing OTLP configuration cause the standalone
  API to crash or log errors on every request.

## Success Criteria

- `dotnet build` succeeds with the two new projects added to the solution.
- `dotnet run --project src/EventGoNoGoAdvisor.AppHost` starts the Aspire
  Dashboard and the API resource; the dashboard prints a reachable URL.
- A `POST /event-advisor` request made while the AppHost is running produces
  one trace in the dashboard containing: the inbound ASP.NET Core span, and
  child spans for the outbound Open-Meteo and Nager.Date HTTP calls.
- The dashboard's Metrics view shows ASP.NET Core request metrics,
  `HttpClient` metrics, and .NET runtime metrics populating per request.
- Logs in the dashboard are correlated to the trace (same trace id) for a
  request's lifecycle.
- `dotnet run --project src/EventGoNoGoAdvisor.Api` (standalone, no AppHost)
  still starts cleanly and serves `/event-advisor` and `/swagger`
  identically to today, with no exporter errors in the console.
- `/health` and `/alive` respond `200 OK` in Development when run via the
  AppHost.
- `dotnet test` passes with zero failures and zero new flaky/slow tests.
- README updated with: how to run via the AppHost to see telemetry, what's
  instrumented (traces/metrics/logs), and a link to the new ADR.
- A new ADR under `docs/decisions/` records the choice of Aspire
  `ServiceDefaults` + OTLP + local Dashboard over hand-rolled OpenTelemetry
  wiring or a third-party APM.

## Open Questions

None. Both prior open questions are resolved: scope is Aspire's free
auto-instrumentation only (no custom metrics), on Aspire 9.x latest stable.
