# Event Go/No-Go Advisor

## Problem Statement
How might we build a small ASP.NET Core integration that composes two public APIs
(weather + public holidays) into an explainable GO/NO-GO decision for an outdoor
event — demonstrating clean API design, testable business logic, and sound
engineering judgment for a job-application take-home?

## Recommended Direction
A minimal ASP.NET Core Web API with a single endpoint: given an event date,
coordinates, and a country code, it calls Open-Meteo (forecast) and Nager.Date
(public holidays), runs the result through a small ordered set of rules, and
returns a decision (GO / NO-GO) plus the specific reasons behind it.

The weather rules (rain probability, wind speed) are blocking — they can flip
the decision to NO-GO. The holiday check is advisory only — it never blocks by
itself, it just surfaces a staffing/attendance risk alongside the weather
verdict. This mirrors how a real business rule would actually work (a holiday
doesn't ruin outdoor weather) and avoids the trap of treating two unrelated
concerns as equally severe.

The architecture goal is a small, testable `IEventRule` strategy list — not a
plugin framework. Each rule is a pure function of (forecast, holiday info) ->
verdict + reason, independently unit-testable with no HTTP involved. The two
external clients sit behind interfaces (`IWeatherClient`, `IHolidayClient`) so
rule logic and API integration are tested separately.

## Key Assumptions to Validate
- [ ] Caller supplies lat/long + ISO country code directly (no geocoding step) — confirm this is an acceptable input shape, not "address text."
- [ ] Open-Meteo and Nager.Date stay free/keyless and respond reliably during development — do a smoke-test call to each before building the rule engine on top.
- [ ] Explainable reasons (not just a boolean) are seen as a positive signal, not unnecessary complexity — keep the reason strings short and rule-specific to hedge this.

## MVP Scope
**In:**
- ASP.NET Core minimal Web API, one `POST /event-advisor` endpoint, Swagger/OpenAPI enabled.
- `IWeatherClient` (Open-Meteo) and `IHolidayClient` (Nager.Date), each behind an interface, registered via DI, backed by `HttpClient`.
- Rule engine: ordered `IEventRule` list — rain-probability rule, wind-speed rule (both blocking), holiday rule (advisory). Aggregator combines verdicts + reasons.
- Input validation (valid date not in the past, valid lat/long range, valid ISO country code).
- Graceful handling of upstream API failure (clear error response, not a silent default).
- Unit tests for each rule (pure, no HTTP) + tests for the two clients against mocked `HttpMessageHandler`.
- README: how to run, design decisions, what was deliberately left out.

## Not Doing (and Why)
- **Geocoding from city/address text** — adds a third external dependency and pushes this past a 1–2 day budget; caller supplies coordinates directly.
- **Multi-date "best date in range" recommendation** — a single date in, single verdict out; recommending across a range is a distinct feature, not core to proving the rule-engine design.
- **Persistence / database** — stateless request-in/response-out; nothing here needs to be stored.
- **Caching / retry policies beyond basic error handling** — worth naming as a known production gap in the README, not worth building for a take-home.
- **Auth on the endpoint** — out of scope for an integration-logic exercise.

## Open Questions
- Test framework preference: xUnit or NUnit?
- Should the rain-probability and wind-speed thresholds be hardcoded constants, or exposed via `appsettings.json` (to show configuration awareness without over-engineering)?
