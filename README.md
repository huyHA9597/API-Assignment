# Event Go/No-Go Advisor

A small ASP.NET Core Web API that composes two public, keyless APIs
([Open-Meteo](https://open-meteo.com/) for weather, [Nager.Date](https://date.nager.at/)
for public holidays) into a single explainable **GO / NO_GO** decision for a
proposed outdoor event.

Idea: [docs/ideas/event-go-no-go-advisor.md](docs/ideas/event-go-no-go-advisor.md) ·
Spec: [docs/specs/event-go-no-go-advisor.md](docs/specs/event-go-no-go-advisor.md)

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

## Design decisions

- **Rules are pure, and tested without any HTTP involved.** `IEventRule.Evaluate(WeatherForecast, HolidayInfo)`
  takes already-fetched domain models, not raw API responses — the rule
  engine's correctness never depends on the network. `RainProbabilityRuleTests`,
  `WindSpeedRuleTests`, `PublicHolidayRuleTests`, and `EventRuleEngineTests`
  construct these models directly and never touch `HttpClient`.
- **Blocking vs. advisory severity is enforced at the engine level, not by convention.**
  `EventRuleEngine.Evaluate` only flips the verdict to `NO_GO` when a *triggered*
  rule's `Severity` is `Blocking`. `PublicHolidayRule` is always `Advisory`, so a
  holiday can never block an event by itself — it only surfaces a staffing/attendance
  risk alongside the weather verdict. This mirrors how the business rule should
  actually behave (a holiday doesn't ruin outdoor weather) and is covered directly
  by `EventRuleEngineTests` using fake rules, independent of the real rule implementations.
- **Thresholds are configuration, not constants.** `EventRuleThresholds` binds
  from `appsettings.json` via `IOptions<T>`, so the 70%/40km/h defaults can be
  tuned per environment without a code change or redeploy.
- **Clients throw typed exceptions instead of returning nulls or default data.**
  `WeatherClientException` / `HolidayClientException` surface as `502 Bad Gateway`
  from the endpoint, explicitly naming which upstream failed. The endpoint never
  falls back to a guessed verdict on upstream failure.
- **A shared `FakeHttpMessageHandler` test helper** backs both client test suites,
  so neither test suite needs a real network call, and mocking boilerplate isn't
  duplicated across the two clients.
- **`eventDate` is bound as a `string`, not `DateOnly`.** This keeps malformed-date
  handling inside the endpoint's own validation (producing a field-level `400`
  message) instead of failing during ASP.NET Core's model binding, where the
  error shape can't be controlled as precisely.

### A real bug this caught

While manually smoke-testing the endpoint against live data (Task 11/12), a
request for Paris briefly failed with a generic "unexpected response shape"
error. The actual cause: Open-Meteo can return `null` for
`precipitation_probability_max` on the later days of its 16-day forecast window
(apparently data-freshness dependent, not a fixed day cutoff) even while
`wind_speed_10m_max` for the same day is populated. `OpenMeteoWeatherClient`
originally deserialized both arrays as non-nullable, so a `null` threw a
`JsonException` that got reported with a misleading message. Fixed by
deserializing to nullable arrays and throwing a specific "no complete forecast
data yet for `{date}`" exception instead, with a regression test covering it
(`GetForecastAsync_WhenRequestedDateHasNullForecastValues_ThrowsWeatherClientException`).

## Left out, and why

Mirroring the idea doc's [Not Doing](docs/ideas/event-go-no-go-advisor.md#not-doing-and-why) list:

- **Geocoding from city/address text** — would add a third external dependency
  for a take-home; the caller supplies coordinates and a country code directly.
- **Multi-date "best date in range" recommendation** — a single date in, single
  verdict out, per the spec. Recommending across a range is a distinct feature,
  not core to proving the rule-engine design.
- **Persistence / a database** — the API is stateless request-in/response-out;
  nothing here needs to be stored.
- **Caching / retry policies** — every request makes a fresh call to both
  upstream APIs with no retry on transient failure. This is a known production
  gap, acceptable for a take-home exercise, but would matter under real load.
- **Auth on the endpoint** — out of scope for an integration-logic exercise;
  both upstream APIs are keyless, so there's nothing to protect yet.
- **Live validation of `countryCode` against Nager.Date's `AvailableCountries` list** —
  the spec called this out as "if practical." The endpoint validates only the
  ISO 3166-1 alpha-2 *format* (two letters); an unrecognized-but-well-formed code
  surfaces as a `502` from the holiday client instead of an immediate `400`. The
  smoke test confirmed `AvailableCountries` is reachable, if this is wanted later.
- **Regional-only holidays** — Nager.Date returns some holidays with a non-null
  `counties` list (observed for the US, e.g. "Lincoln's Birthday"), meaning
  they're not nationwide. `PublicHolidayRule`/`NagerDateHolidayClient` currently
  treat any date match as a holiday regardless of region — a reasonable
  simplification for this scope, called out here rather than silently assumed.

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
tests/EventGoNoGoAdvisor.Tests/
  Rules/*Tests.cs                         -> pure unit tests, no HTTP
  Clients/*Tests.cs                       -> mocked HttpMessageHandler, no real network calls
  TestHelpers/FakeHttpMessageHandler.cs
docs/
  ideas/event-go-no-go-advisor.md
  specs/event-go-no-go-advisor.md
tasks/
  plan.md, todo.md                        -> implementation plan and task-by-task notes/findings
```
