# ADR-001: Pure `IEventRule` strategy list with engine-enforced blocking/advisory split

## Status
Accepted

## Date
2026-10-25

## Context
The core thing this project demonstrates (per the spec's Objective) is a
testable business-logic structure for turning weather + holiday data into an
explainable GO/NO_GO decision. That means:

- Each check (rain, wind, holiday) must be independently unit-testable with
  no HTTP involved, so correctness of the decision logic never depends on
  two external APIs being reachable.
- New checks must be addable without touching existing ones.
- The holiday check must never be able to flip the verdict by itself — a
  public holiday is a staffing/attendance risk, not a reason to cancel an
  outdoor event, and that distinction has to be structural, not a
  convention an individual rule could get wrong.

## Decision
Model every check as a pure function via `IEventRule.Evaluate(WeatherForecast,
HolidayInfo) -> RuleReason`, where `RuleReason` carries a `Severity`
(`Blocking` or `Advisory`). `EventRuleEngine` evaluates every registered rule
and sets the final verdict to `NO_GO` if and only if a *triggered* rule's
`Severity` is `Blocking`. No `IEventRule` implementation decides the final
verdict itself — only the engine does, by reading the severity it declares.

## Alternatives Considered

### Let each rule decide whether it can block, inline
- Pros: one less concept (`RuleSeverity`) to understand.
- Cons: the "can this block the verdict" decision would be duplicated inside
  every rule's own logic instead of living in one place.
- Rejected: a future rule author could accidentally make an advisory-only
  check (e.g. holiday) affect the verdict, with no single place to catch it.
  Keeping the engine as the only thing that reads `Severity` makes that
  mistake structurally harder, and is covered directly by
  `EventRuleEngineTests` using fake rules, independent of the real ones.

### A rule pipeline where rules can short-circuit execution
- Pros: resembles ASP.NET Core middleware, familiar pattern.
- Cons: introduces rule *ordering* as a correctness concern, and the spec
  requires every rule's result in the response (`reasons` includes every
  rule, triggered or not) — a short-circuiting pipeline would have to be
  reworked to still collect skipped rules' reasons.
- Rejected: all rules evaluate the same two already-fetched inputs with no
  dependency between them, so there's nothing for a pipeline to buy here.

### Fetch weather/holiday data inside each rule
- Pros: fewer moving parts (`IWeatherClient`/`IHolidayClient` wouldn't need
  to sit between the endpoint and the rules).
- Cons: couples rule logic to HTTP, directly contradicting the testability
  goal — rule unit tests would need mocked `HttpClient`s instead of plain
  constructed records.
- Rejected: the endpoint fetches both models once and passes them to every
  rule, so `RainProbabilityRuleTests`, `WindSpeedRuleTests`,
  `PublicHolidayRuleTests`, and `EventRuleEngineTests` construct
  `WeatherForecast`/`HolidayInfo` directly and never touch `HttpClient`.

## Consequences
- Adding a new rule means implementing `IEventRule` and registering it in
  DI (`Program.cs`) — no change to `EventRuleEngine` or any existing rule.
- The blocking/advisory split is enforced in exactly one place
  (`EventRuleEngine.Evaluate`), so it can be audited and tested in isolation
  from the real rules.
- All rules are constrained to operate on the same two fetched models
  (`WeatherForecast`, `HolidayInfo`). A future rule needing a different
  input (e.g. venue capacity) would require widening those models or adding
  a new one, not a rule-engine redesign.
