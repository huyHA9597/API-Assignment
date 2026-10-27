# Event Go/No-Go Advisor

A small ASP.NET Core Web API that composes two public, keyless APIs
([Open-Meteo](https://open-meteo.com/) for weather, [Nager.Date](https://date.nager.at/)
for public holidays) into a single explainable **GO / NO_GO** decision for a
proposed outdoor event.

Idea: [docs/ideas/event-go-no-go-advisor.md](docs/ideas/event-go-no-go-advisor.md) ·
Spec: [docs/specs/event-go-no-go-advisor.md](docs/specs/event-go-no-go-advisor.md) ·
Decisions: [docs/decisions/](docs/decisions/README.md) ·
Diagrams: [architecture](docs/diagram/architecture.md) ([PNG](docs/diagram/architecture.png)), [sequence](docs/diagram/sequence-event-advisor.md) ([PNG](docs/diagram/sequence-event-advisor.png))

## Running it

Requires the .NET 8 SDK (or a later SDK with the net8.0 runtime installed — this
was built and tested against the .NET 10 SDK with the net8.0 shared runtime present).

```bash
dotnet build
dotnet test
dotnet run --project src/EventGoNoGoAdvisor.Api
```

Swagger UI is available at `/swagger` when running in the Development environment
(the default for `dotnet run`).

### Running with telemetry (Aspire Dashboard)

To see live traces, metrics, and logs for the API, run it through the Aspire
AppHost instead:

```bash
dotnet run --project src/EventGoNoGoAdvisor.AppHost
```

This prints a Dashboard URL (with a one-time login token) to the console.
Open it, send a request to `/event-advisor`, and the dashboard shows:
- **Traces** — the inbound request span plus child spans for the outbound
  Open-Meteo and Nager.Date calls.
- **Metrics** — ASP.NET Core request metrics, `HttpClient` metrics, and
  .NET runtime metrics (GC, thread pool, exceptions).
- **Logs** — correlated to the trace id for that request.

Running the API standalone (`dotnet run --project src/EventGoNoGoAdvisor.Api`,
no AppHost) still works exactly as before — the OTLP exporter only activates
when an endpoint is configured, so there's no dashboard dependency and no
exporter errors without it.

See [ADR-004](docs/decisions/004-telemetry-via-aspire-service-defaults.md)
for why this uses Aspire's `ServiceDefaults` pattern with auto-instrumentation
only (no custom business metrics, no third-party APM backend).

## Sample request/response

Captured against the live Open-Meteo and Nager.Date APIs:

```
POST /event-advisor
{
  "eventDate": "2026-11-05",
  "latitude": 40.7128,
  "longitude": -74.0060,
  "countryCode": "US"
}
```

```json
{
  "verdict": "GO",
  "reasons": [
    {
      "rule": "RainProbabilityRule",
      "severity": "Blocking",
      "triggered": false,
      "message": "Max precipitation probability 25% is below the 70% threshold."
    },
    {
      "rule": "WindSpeedRule",
      "severity": "Blocking",
      "triggered": false,
      "message": "Max wind speed 18.4km/h is below the 40km/h threshold."
    },
    {
      "rule": "PublicHolidayRule",
      "severity": "Advisory",
      "triggered": false,
      "message": "This date is not a public holiday."
    }
  ],
  "weather": { "maxPrecipitationProbabilityPercent": 25, "maxWindSpeedKmh": 18.4 },
  "holiday": { "isPublicHoliday": false, "name": null }
}
```

Two more live scenarios were verified manually during development (see
`tasks/todo.md` Task 12 for the full captures):

- **High rain forecast → `NO_GO`:** Singapore, 2026-10-27 — live forecast showed
  96% precipitation probability, well above the 70% threshold, so
  `RainProbabilityRule` triggered and flipped the verdict.
- **Holiday + clear weather → `GO` with an advisory reason:** Paris, 2026-11-01
  (All Saints' Day) — `PublicHolidayRule` triggered but, being advisory-only,
  never changed the verdict.

### More sample requests

High rain forecast (expected `NO_GO` via `RainProbabilityRule`):

```
POST /event-advisor
{
  "eventDate": "2026-10-27",
  "latitude": 1.3521,
  "longitude": 103.8198,
  "countryCode": "SG"
}
```

High wind forecast (expected `NO_GO` via `WindSpeedRule`):

```
POST /event-advisor
{
  "eventDate": "2026-11-10",
  "latitude": 51.5074,
  "longitude": -0.1278,
  "countryCode": "GB"
}
```

Public holiday with clear weather (expected `GO` with an advisory reason):

```
POST /event-advisor
{
  "eventDate": "2026-11-01",
  "latitude": 48.8566,
  "longitude": 2.3522,
  "countryCode": "FR"
}
```

Malformed date (expected `400` field-level validation error):

```
POST /event-advisor
{
  "eventDate": "not-a-date",
  "latitude": 40.7128,
  "longitude": -74.0060,
  "countryCode": "US"
}
```

Unrecognized but well-formed country code (expected `502` from the holiday client):

```
POST /event-advisor
{
  "eventDate": "2026-11-05",
  "latitude": 40.7128,
  "longitude": -74.0060,
  "countryCode": "ZZ"
}
```

## Project structure

```
EventGoNoGoAdvisor.sln
src/EventGoNoGoAdvisor.Api/
  Program.cs                              -> DI wiring, Options binding, Swagger, endpoint mapping
  Endpoints/EventAdvisorEndpoints.cs      -> POST /event-advisor: validation + orchestration
  Clients/
    IWeatherClient.cs, OpenMeteoWeatherClient.cs
    IHolidayClient.cs, NagerDateHolidayClient.cs
  Rules/
    IEventRule.cs
    RainProbabilityRule.cs, WindSpeedRule.cs, PublicHolidayRule.cs
    EventRuleEngine.cs                    -> aggregates rule results into final verdict
  Models/
    WeatherForecast.cs, HolidayInfo.cs, RuleReason.cs, EventAdvisorContracts.cs
  Options/EventRuleThresholds.cs          -> bound from appsettings.json
src/EventGoNoGoAdvisor.ServiceDefaults/
  Extensions.cs                           -> OpenTelemetry + health-check wiring (see ADR-004)
src/EventGoNoGoAdvisor.AppHost/
  AppHost.cs                              -> orchestrates the Api + Aspire Dashboard
tests/EventGoNoGoAdvisor.Tests/
  Rules/*Tests.cs                         -> pure unit tests, no HTTP
  Clients/*Tests.cs                       -> mocked HttpMessageHandler, no real network calls
  TestHelpers/FakeHttpMessageHandler.cs
docs/
  ideas/event-go-no-go-advisor.md
  specs/event-go-no-go-advisor.md, telemetry-observability-aspire.md
  decisions/                              -> ADRs: rule-engine shape, API choice, hardening, telemetry
  diagram/architecture.md                 -> component diagram of src/
  diagram/sequence-event-advisor.md       -> sequence diagram of POST /event-advisor
tasks/
  plan.md, todo.md                        -> base feature's implementation plan and task notes
  plan-telemetry.md, todo-telemetry.md    -> telemetry feature's implementation plan and task notes
```
