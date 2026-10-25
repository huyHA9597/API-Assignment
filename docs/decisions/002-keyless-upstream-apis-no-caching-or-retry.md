# ADR-002: Keyless upstream APIs (Open-Meteo, Nager.Date); no caching, retry, or auth

## Status
Accepted

## Date
2026-10-25

## Context
This is a take-home exercise with a 1-2 day budget (per the idea doc), read
by a single reviewer who needs to clone the repo and run it immediately.
Anything that adds signup friction, secrets, or infrastructure between
"clone" and "see a verdict" works against that goal. The exercise also needs
two real external integrations (weather, public holidays) to demonstrate
client/rule separation — not a resilience-engineering showcase.

## Decision
Use **Open-Meteo** for weather forecasts and **Nager.Date** for public
holidays — both free and keyless, confirmed reachable without
registration (Task 2 smoke test). Make one fresh call to each per request:
no response caching, no retry/circuit-breaker policy on transient failure,
and no authentication on the `/event-advisor` endpoint itself.

## Alternatives Considered

### Commercial weather providers (OpenWeatherMap, WeatherAPI, Tomorrow.io)
- Pros: richer data, published SLAs.
- Cons: all require an API key, which means secret management (env vars or
  user-secrets) a reviewer has to set up before the app even starts.
- Rejected: this exercise only needs two numeric fields (max precipitation
  probability, max wind speed), which Open-Meteo already exposes for free,
  confirmed in the Task 2 smoke test against the live API.

### Commercial holiday providers (Calendarific, Abstract API Holidays)
- Pros: potentially broader country coverage.
- Cons: also key-gated.
- Rejected: Nager.Date's `PublicHolidays/{year}/{countryCode}` already
  returns exactly the flat date + name list this needs, confirmed in the
  Task 2 smoke test, with no key.

### Add a caching layer (in-memory or distributed) in front of both clients
- Pros: fewer upstream calls under repeated requests for the same
  date/location.
- Cons: adds state to what's otherwise a stateless request-in/response-out
  service, and risks masking a real upstream outage behind stale cached
  data during review.
- Rejected for this scope; named explicitly as a known production gap in
  the README rather than silently assumed away.

### Add retry/circuit-breaker policies (e.g. Polly) around both clients
- Pros: standard production resilience against transient upstream failures.
- Cons: doubles the implementation and test surface for clients whose
  purpose here is demonstrating typed-exception error handling, not
  resilience policy — and the spec's Boundaries call out "ask first"
  before adding packages beyond the pre-approved list.
- Rejected for this scope; also named as a known gap in the README.

### Require an API key or token on `/event-advisor` itself
- Rejected per the spec's Boundaries: there are no real users/tenants to
  authenticate for a single-endpoint integration-logic exercise. (Resource
  abuse — a different concern from auth — is addressed separately in
  [ADR-003](003-harden-against-resource-abuse-and-info-leaks.md).)

## Consequences
- Zero setup burden: `dotnet run` and the app is immediately callable
  against live data, with nothing to configure or redact from version
  control.
- No resilience against upstream flakiness beyond the manual smoke tests
  (Task 2, Task 12): a transient upstream hiccup or rate limit surfaces as a
  `502` to the caller rather than being retried or served from cache. This
  is accepted and documented (README "Left out, and why"), not an oversight.
- If this ever needed to run as an unattended production service rather
  than a reviewed take-home, caching and retry policies would need to be
  revisited — this ADR does not claim they're permanently unnecessary, only
  out of scope here.
