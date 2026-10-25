# Implementation Plan: Event Go/No-Go Advisor

Spec: [docs/specs/event-go-no-go-advisor.md](../docs/specs/event-go-no-go-advisor.md)

## Overview

A single `POST /event-advisor` endpoint on .NET 8 / ASP.NET Core Minimal API.
The build order proves the business logic (the `IEventRule` list) is correct
in complete isolation from HTTP before the two external clients and the
endpoint wiring are added on top. A manual smoke test against the live
Open-Meteo and Nager.Date APIs runs early, so the client implementations are
written against confirmed real response shapes instead of assumptions.

## Architecture Decisions

- **Rules are pure and tested before any client exists.** `IEventRule.Evaluate(WeatherForecast, HolidayInfo)` takes already-fetched domain models, not raw API responses — so the rule engine's correctness never depends on HTTP being wired up. This is the core thing being demonstrated per the spec's Objective.
- **One rule = one task.** Each of `RainProbabilityRule`, `WindSpeedRule`, `PublicHolidayRule` ships with its own unit tests in the same task, so the task list stays small and each is independently verifiable.
- **Smoke test before client code.** Task 2 is a manual spike (no production code) that confirms Open-Meteo's forecast horizon and Nager.Date's response shape against the spec's resolved-but-unconfirmed defaults, before Tasks 8–9 write parsing code against them.
- **Shared fake `HttpMessageHandler` test helper**, introduced in the first client task and reused by the second, avoids duplicating HTTP-mocking boilerplate.
- **Endpoint wiring is split from DI/Swagger setup** (Tasks 10 and 11) because combining them would exceed the ~5-file task-size guideline once validation and error handling are included.

## Task List

### Phase 1: Foundation

- [x] Task 1: Scaffold solution and projects
- [x] Task 2: Manual smoke test of Open-Meteo and Nager.Date
- [x] Task 3: Domain models and options

### Checkpoint: Foundation
- [x] `dotnet build` succeeds
- [x] `dotnet test` runs (zero tests, zero failures)
- [x] Smoke-test findings documented; spec updated if any assumption was wrong (no update needed — both confirmed)

### Phase 2: Rule Engine (pure business logic)

- [x] Task 4: RainProbabilityRule + unit tests
- [x] Task 5: WindSpeedRule + unit tests
- [x] Task 6: PublicHolidayRule + unit tests
- [x] Task 7: EventRuleEngine (aggregator) + unit tests

### Checkpoint: Rule Engine
- [x] All rule and engine unit tests pass
- [x] Zero HTTP/I/O dependencies anywhere in `Rules/`
- [x] Engine correctly flips to `NO_GO` on a blocking rule and never flips on the advisory rule alone

### Phase 3: External Clients

- [x] Task 8: OpenMeteoWeatherClient + unit tests
- [x] Task 9: NagerDateHolidayClient + unit tests

### Checkpoint: Clients
- [x] All client unit tests pass against the mocked `HttpMessageHandler` (success + failure cases)
- [x] No test makes a real network call

### Phase 4: Endpoint

- [x] Task 10: DI wiring, Options binding, Swagger setup
- [x] Task 11: POST /event-advisor endpoint, input validation, upstream-failure handling

### Checkpoint: Endpoint
- [x] `dotnet test` passes in full
- [x] Swagger UI loads at `/swagger` and documents the endpoint
- [x] Manual Swagger call with a valid payload returns a sensible verdict

### Phase 5: Verification & Docs

- [x] Task 12: Manual end-to-end smoke test against live APIs (three scenarios)
- [x] Task 13: README

### Checkpoint: Complete
- [x] All Success Criteria in the spec are met
- [ ] Ready for human review

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Open-Meteo forecast horizon differs from the assumed 15-day cutoff | Medium — wrong validation bound | Task 2 smoke test confirms this before Task 11 locks validation |
| Nager.Date response shape or endpoint differs from assumption | Medium — client parsing breaks | Task 2 smoke test confirms the real JSON shape before Task 9 |
| Rain/wind thresholds feel arbitrary to a reviewer | Low — subjective judgment call | Document rationale for the chosen defaults in the README (Task 13) |
| Mocking `HttpMessageHandler` becomes repetitive boilerplate across two clients | Low — time cost | Shared `FakeHttpMessageHandler` test helper introduced in Task 8, reused in Task 9 |
| Upstream API down/slow during live smoke tests (Task 2, Task 12) | Low — blocks manual verification temporarily | Both APIs are public/free with no auth; retry the manual call later if transient |

## Open Questions

None blocking implementation. Task 2's findings may prompt small, low-risk adjustments to validation bounds or threshold defaults already called out as resolved-pending-confirmation in the spec.
