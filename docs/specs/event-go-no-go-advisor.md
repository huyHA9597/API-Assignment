# Spec: Event Go/No-Go Advisor

Source idea: [docs/ideas/event-go-no-go-advisor.md](../ideas/event-go-no-go-advisor.md)

## Objective

Build a small ASP.NET Core Web API that composes two public, keyless APIs
(Open-Meteo for weather, Nager.Date for public holidays) into a single
explainable GO / NO-GO decision for a proposed outdoor event.

**Who it's for:** a hiring reviewer evaluating a take-home submission for API
design quality, testable business-logic structure, and engineering judgment —
not an end user. The deliverable is the code + README, read by one engineer.

**Success looks like:** a reviewer can clone the repo, run it, hit the one
endpoint with a sample payload, see a sensible verdict with reasons, read the
rule engine and immediately understand how to add a new rule, and run the
test suite and see the business logic proven without any network calls.

## API Contract

**`POST /event-advisor`**

Request body:
```json
{
  "eventDate": "2026-11-05",
  "latitude": 40.7128,
  "longitude": -74.0060,
  "countryCode": "US"
}
```
- `eventDate`: ISO `yyyy-MM-dd`, no time component (single date in, single verdict out — see idea doc's "Not Doing").
- `latitude` / `longitude`: decimal degrees, validated to standard ranges (-90..90, -180..180).
- `countryCode`: ISO 3166-1 alpha-2, validated by format and (if practical) checked against Nager.Date's available-countries list.

Success response (`200 OK`):
```json
{
  "verdict": "GO",
  "reasons": [
    {
      "rule": "RainProbability",
      "severity": "Blocking",
      "triggered": false,
      "message": "Max precipitation probability 20% is below the 70% threshold."
    },
    {
      "rule": "PublicHoliday",
      "severity": "Advisory",
      "triggered": true,
      "message": "2026-11-05 is a public holiday in US (Veterans Day) — expect staffing/attendance risk."
    }
  ],
  "weather": { "maxPrecipitationProbabilityPercent": 20, "maxWindSpeedKmh": 18.4 },
  "holiday": { "isPublicHoliday": true, "name": "Veterans Day" }
}
```
- `verdict` is `GO` or `NO_GO`.
- `reasons` includes **every** rule's output (triggered or not), not just the ones that fired — this is what makes the decision explainable/auditable.
- Blocking rules (rain, wind) can flip `verdict` to `NO_GO`. The holiday rule is advisory-only and never changes `verdict` by itself, per the idea doc's framing.

Validation failure: `400 Bad Request` with field-level errors (bad date format, past date, out-of-range coordinates, malformed country code).

Upstream failure: `502 Bad Gateway` (or `503`) naming which upstream failed (weather vs. holiday) and that no verdict could be produced — never a silent default verdict.

## Tech Stack

- .NET 8, ASP.NET Core Minimal API, C# with nullable reference types enabled.
- `HttpClientFactory` (typed clients) for `IWeatherClient` / `IHolidayClient`.
- `Microsoft.Extensions.Options` for configurable rule thresholds.
- Swashbuckle for Swagger/OpenAPI.
- xUnit for tests; Moq (or an equivalent fake `HttpMessageHandler`) for mocking HTTP in client tests.

## Commands

```
dotnet build
dotnet test
dotnet run --project src/EventGoNoGoAdvisor.Api
```
Swagger UI available at `/swagger` when running in Development.

## Project Structure

```
EventGoNoGoAdvisor.sln
src/EventGoNoGoAdvisor.Api/
  Program.cs
  Endpoints/EventAdvisorEndpoints.cs      -> maps POST /event-advisor
  Clients/
    IWeatherClient.cs, OpenMeteoWeatherClient.cs
    IHolidayClient.cs, NagerDateHolidayClient.cs
  Rules/
    IEventRule.cs, RuleReason.cs, RuleVerdict.cs
    RainProbabilityRule.cs, WindSpeedRule.cs, PublicHolidayRule.cs
    EventRuleEngine.cs                    -> aggregates rule results into final verdict
  Models/
    EventAdvisorRequest.cs, EventAdvisorResponse.cs
    WeatherForecast.cs, HolidayInfo.cs
  Options/
    EventRuleThresholds.cs                -> bound from appsettings.json
  appsettings.json
tests/EventGoNoGoAdvisor.Tests/
  Rules/RainProbabilityRuleTests.cs
  Rules/WindSpeedRuleTests.cs
  Rules/PublicHolidayRuleTests.cs
  Rules/EventRuleEngineTests.cs
  Clients/OpenMeteoWeatherClientTests.cs
  Clients/NagerDateHolidayClientTests.cs
docs/
  ideas/event-go-no-go-advisor.md
  specs/event-go-no-go-advisor.md
README.md
```

## Code Style

```csharp
public interface IEventRule
{
    RuleReason Evaluate(WeatherForecast forecast, HolidayInfo holiday);
}

public sealed class WindSpeedRule : IEventRule
{
    private readonly double _maxWindSpeedKmh;

    public WindSpeedRule(IOptions<EventRuleThresholds> thresholds)
        => _maxWindSpeedKmh = thresholds.Value.MaxWindSpeedKmh;

    public RuleReason Evaluate(WeatherForecast forecast, HolidayInfo holiday)
    {
        var triggered = forecast.MaxWindSpeedKmh > _maxWindSpeedKmh;
        return new RuleReason(
            Rule: nameof(WindSpeedRule),
            Severity: RuleSeverity.Blocking,
            Triggered: triggered,
            Message: $"Max wind speed {forecast.MaxWindSpeedKmh}km/h is "
                + (triggered ? $"above the {_maxWindSpeedKmh}km/h threshold." : $"below the {_maxWindSpeedKmh}km/h threshold."));
    }
}
```
- PascalCase for types/public members, camelCase for locals/parameters, `_camelCase` for private fields.
- Prefer `record`/`sealed record` for DTOs (`WeatherForecast`, `HolidayInfo`, request/response models) — they're immutable data.
- Each rule is a pure function of `(forecast, holiday) -> RuleReason`: no I/O, no `HttpClient`, no static state.
- Async methods end in `Async` and return `Task<T>`.

## Testing Strategy

- **Rules** (`Rules/*Tests.cs`): pure unit tests, no HTTP, construct `WeatherForecast`/`HolidayInfo` directly, assert `RuleReason.Triggered` and the final aggregated `verdict` from `EventRuleEngine`. Cover both the triggered and non-triggered branch of every rule, plus the engine's aggregation (blocking rule fires → `NO_GO` regardless of advisory rule; advisory rule alone never flips verdict).
- **Clients** (`Clients/*Tests.cs`): inject a fake/mocked `HttpMessageHandler` returning canned JSON fixtures for success, and non-2xx/timeout for failure; assert correct parsing and that failures surface as a typed exception/result rather than throwing an unhandled exception into the endpoint.
- **No live network calls in the test suite.** A one-time manual smoke test against the real Open-Meteo and Nager.Date endpoints is a task in the plan (not an automated test) to confirm response shapes before building client parsing against them.
- Coverage expectation: every rule branch, every client success/failure branch, and the engine's aggregation logic. Endpoint-level request validation can be covered with a handful of integration-style tests using `WebApplicationFactory` if time allows, but is not required to hit the bar.

## Boundaries

- **Always:** run `dotnet test` before considering a task done; keep rules pure (no HTTP/DB/static state inside `IEventRule` implementations); validate all three input fields before calling either upstream API; return a typed error (never a fabricated default verdict) on upstream failure.
- **Ask first:** adding any NuGet package beyond ASP.NET Core defaults + Swashbuckle + xUnit + Moq; changing the request/response JSON contract above once implementation starts; widening scope to multi-date or geocoding (both explicitly out per the idea doc).
- **Never:** add authentication/authorization to the endpoint; add a database or persistence; add caching or retry policies (name it as a known gap in the README instead); commit secrets (moot — both APIs are keyless, but applies to any config added later).

## Success Criteria

- `dotnet build` and `dotnet test` succeed with zero failures.
- `POST /event-advisor` with a valid payload returns `200` with a `verdict` and a `reasons` array containing an entry for every rule (fired or not).
- Each of the three example cases produces the expected verdict: clear weather + non-holiday → `GO`; high rain probability or wind → `NO_GO` regardless of holiday; holiday + clear weather → `GO` with an advisory reason present.
- Invalid input (bad date format, past date, out-of-range lat/long, malformed country code) returns `400` with a message identifying the invalid field.
- A simulated upstream failure (mocked in tests) returns a `502`/`503`-class response naming the failed upstream, never a `200` with guessed data.
- Every `IEventRule` implementation has unit tests with zero HTTP dependency; both weather and holiday clients have tests against a mocked `HttpMessageHandler` for success and failure.
- Swagger UI loads at `/swagger` and documents the endpoint's request/response shapes.
- README covers: how to run, design decisions (why rules are pure, why holiday is advisory-only), and an explicit "left out and why" section mirroring the idea doc's "Not Doing."

## Open Questions

None blocking. The items below are accepted as working defaults; the smoke-test task in the plan will confirm the two API-shape assumptions against the live services and flag it if either needs adjusting, but implementation is not blocked on that confirmation.

Resolved decisions (were open in the idea doc, now settled for this spec):
- .NET 8.
- xUnit as the test framework.
- Rule thresholds live in `appsettings.json` via an `EventRuleThresholds` options class, not hardcoded constants.
- Solution lives at the repo root (`src/`, `tests/`), not under a subfolder.
- **Open-Meteo forecast horizon:** `eventDate` more than 15 days in the future is rejected as `400`, pending smoke-test confirmation of Open-Meteo's actual supported range.
- **Default thresholds:** blocking if max precipitation probability > 70%, or max wind speed > 40 km/h. Both overridable via `appsettings.json` under `EventRuleThresholds`.
- **Nager.Date query shape:** fetch `/api/v3/PublicHolidays/{year}/{countryCode}` and check whether `eventDate` appears in the list (rather than `/api/v3/IsTodayPublicHoliday`, which only answers for "today").
