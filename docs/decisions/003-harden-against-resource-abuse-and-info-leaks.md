# ADR-003: Harden the endpoint against resource abuse and information leakage

## Status
Accepted

## Date
2026-10-25

## Context
Once `/event-advisor` worked functionally (Task 11) but before treating it as
done, a hardening pass (commit `45263b4`) looked at what an unauthenticated,
public, single-endpoint API exposes by default:

- No rate limiting — the endpoint could be called without bound, each call
  consuming the app's own egress against two free upstream APIs
  ([ADR-002](002-keyless-upstream-apis-no-caching-or-retry.md)) that have no
  SLA and could rate-limit or block the app's IP if abused.
- No timeout on the upstream `HttpClient`s — a hung upstream could tie up a
  request (and a rate-limit slot) indefinitely.
- Client-facing `502` error bodies echoed the raw exception message from
  `WeatherClientException`/`HolidayClientException`, which could include
  upstream-specific text not meant for an external caller.
- ASP.NET Core's bare defaults don't include HSTS, HTTPS redirection,
  standard security response headers, or a production-safe (non-Development)
  fallback exception handler.

## Decision
- **Per-IP rate limiting** on `POST /event-advisor`: a fixed 20
  requests/minute window per remote IP, with `QueueLimit = 0` — once the
  window is exhausted, excess requests are rejected immediately with `429`
  rather than queued, since queuing would just delay the abuse rather than
  stop it.
- **10-second timeout** on both upstream `HttpClient`s (`IWeatherClient`,
  `IHolidayClient`), so a hung upstream fails the request instead of
  blocking it indefinitely.
- **Generic, fixed-text error details** in the `502` responses for upstream
  failures. The real exception — with the upstream's actual message — is
  logged server-side via `ILogger` instead of being returned to the caller.
- **Baseline hardening in `Program.cs`**: HTTPS redirection and HSTS outside
  `Development`, a generic production exception handler that never echoes
  exception details, and standard security response headers
  (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, a
  restrictive `Content-Security-Policy`) on every response.

## Alternatives Considered

### Sliding-window or token-bucket rate limiter instead of fixed-window
- Pros: smoother behavior under bursty traffic at window boundaries.
- Cons: more moving parts than this scope needs.
- Rejected for now: a fixed window satisfies the actual requirement here
  ("bound total request volume per caller"). Worth revisiting if burst
  behavior at window edges becomes a real problem under real traffic.

### Require authentication (API key / JWT) instead of rate limiting
- Rejected: the spec's Boundaries explicitly rule out auth for this
  exercise (no real users/tenants to authenticate —
  [ADR-002](002-keyless-upstream-apis-no-caching-or-retry.md)). Rate
  limiting addresses the actual risk — unbounded resource consumption by
  any single caller — without that scope increase.

### Return upstream status codes/details to the caller for debuggability
- Pros: easier for a caller to self-diagnose a `502`.
- Cons: the caller has no actionable use for Open-Meteo's or Nager.Date's
  internal error text, and returning it is a textbook information-leakage
  vector (it can hint at internal architecture, library versions, or
  partner identities).
- Rejected in favor of a generic client-facing message plus server-side
  logging via `ILogger`.

## Consequences
- A caller debugging a `502` only sees "the service is currently
  unavailable" — not why. Diagnosing the real cause requires server log
  access, which is acceptable here since the only caller today is a
  reviewer running the app locally, who has that access.
- The `20` requests/minute limit and `10`-second timeout are literal
  constants in `Program.cs`, not configuration (unlike
  `EventRuleThresholds`, which is deliberately configurable per
  [the spec](../specs/event-go-no-go-advisor.md)). If real traffic patterns
  ever require tuning these, they're candidates for promotion to options,
  not a design this ADR locks in forever.
- The `Content-Security-Policy: default-src 'none'` header assumes this API
  never serves a browser-rendered page (it only returns JSON) — if a
  Swagger UI or similar browser-facing surface were ever exposed outside
  `Development`, that header would need to be scoped per-route instead of
  applied globally.
