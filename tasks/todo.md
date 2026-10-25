# Tasks: Event Go/No-Go Advisor

Plan: [tasks/plan.md](plan.md) · Spec: [docs/specs/event-go-no-go-advisor.md](../docs/specs/event-go-no-go-advisor.md)

## Phase 1: Foundation

### Task 1: Scaffold solution and projects

**Description:** Create the .NET 8 solution and the two projects (`src/EventGoNoGoAdvisor.Api`, `tests/EventGoNoGoAdvisor.Tests`) with nullable reference types enabled, plus a `.gitignore` for .NET build output. No feature code yet — just a buildable skeleton with the default minimal API `Program.cs`.

**Acceptance criteria:**
- [x] `EventGoNoGoAdvisor.sln` at repo root references both projects
- [x] Both `.csproj` files target `net8.0` with `<Nullable>enable</Nullable>`
- [x] Test project references the Api project and has xUnit + Moq package references

**Verification:**
- [x] `dotnet build` succeeds
- [x] `dotnet test` runs with 0 tests, 0 failures

**Notes:** Only the .NET 10 SDK is installed locally (no dedicated .NET 8 SDK), but the net8.0 runtime is present, so `net8.0`-targeted projects build and test correctly under the .NET 10 SDK. `dotnet new sln` defaults to the new `.slnx` format on this SDK — used `-f sln` to get the classic `.sln` the spec expects.

**Dependencies:** None

**Files likely touched:**
- `EventGoNoGoAdvisor.sln`
- `src/EventGoNoGoAdvisor.Api/EventGoNoGoAdvisor.Api.csproj`
- `src/EventGoNoGoAdvisor.Api/Program.cs`
- `tests/EventGoNoGoAdvisor.Tests/EventGoNoGoAdvisor.Tests.csproj`
- `.gitignore`

**Estimated scope:** Small

---

### Task 2: Manual smoke test of Open-Meteo and Nager.Date

**Description:** Spike task, no production code. Manually call the live Open-Meteo forecast endpoint (daily `precipitation_probability_max`, `wind_speed_10m_max`) and the Nager.Date `PublicHolidays/{year}/{countryCode}` endpoint with a sample lat/long/country. Confirm: actual JSON field names and units, how many days ahead Open-Meteo returns forecast data, and that Nager.Date's response is a flat array containing a `date` field matching `yyyy-MM-dd`. Record findings.

**Acceptance criteria:**
- [x] Real sample JSON from both APIs captured (e.g. pasted into a scratch note or this task's notes)
- [x] Forecast horizon confirmed — spec's 15-day cutoff assumption confirmed or corrected
- [x] Nager.Date query shape confirmed — spec's assumed endpoint/response shape confirmed or corrected
- [x] `docs/specs/event-go-no-go-advisor.md` updated if any assumption was wrong (not needed — both assumptions confirmed as-is)

**Verification:**
- [x] Manual — findings recorded; no automated check applies

**Findings (2026-10-25):**

*Open-Meteo* — `GET https://api.open-meteo.com/v1/forecast?latitude=40.7128&longitude=-74.0060&daily=precipitation_probability_max,wind_speed_10m_max&forecast_days=16`:
```json
{"latitude":40.710335,"longitude":-73.99308,"daily_units":{"precipitation_probability_max":"%","wind_speed_10m_max":"km/h"},"daily":{"time":["2026-10-25", "...", "2026-11-09"],"precipitation_probability_max":[0,2,0,0,0,0,1,22,26,24,16,25,26,19,19,19],"wind_speed_10m_max":[10.9,14.5,16.5,13.9,14.1,16.2,11.9,17.6,18.8,10.8,18.1,18.7,21.5,23.7,19.2,23.8]}}
```
Field names and units confirmed exactly as assumed: `daily.precipitation_probability_max` (%), `daily.wind_speed_10m_max` (km/h). `forecast_days=17` returns a 400 (`"Allowed range 0 to 16"`), so the max horizon is **today + 15 days ahead** (16 calendar days including today) — this confirms the spec's 15-day cutoff assumption exactly. No correction needed.

*Nager.Date* — `GET https://date.nager.at/api/v3/PublicHolidays/2026/US`:
```json
[{"date":"2026-01-01","localName":"New Year's Day","name":"New Year's Day","countryCode":"US","fixed":false,"global":true,"counties":null,"launchYear":null,"types":["Public","Bank"]}, "...", {"date":"2026-11-11","localName":"Veterans Day","name":"Veterans Day","countryCode":"US","fixed":false,"global":true,"counties":null,"launchYear":null,"types":["Public","Bank"]}]
```
Confirmed: flat JSON array, each entry has `date` in `yyyy-MM-dd`, matching the spec's assumed shape exactly. Note (non-blocking): some entries have non-null `counties` (regional-only holidays, e.g. "Lincoln's Birthday"); Task 9's simple "does `date` appear in the list" check will treat these the same as global holidays. This is a reasonable simplification for a take-home scope and will be called out in the README, not a spec correction.

`GET https://date.nager.at/api/v3/AvailableCountries` also confirmed reachable and returns the expected `{countryCode, name}` array, available if country-code validation against this list is added in Task 11.

**Dependencies:** None (can run in parallel with Task 1)

**Files likely touched:**
- `docs/specs/event-go-no-go-advisor.md` (only if corrections are needed)

**Estimated scope:** Small

---

### Task 3: Domain models and options

**Description:** Create the plain DTOs the rule engine and clients will share (`WeatherForecast`, `HolidayInfo`, `RuleReason` + `RuleSeverity`), the request/response contracts from the spec's API Contract section, and the `EventRuleThresholds` options POCO that will later bind to `appsettings.json`.

**Acceptance criteria:**
- [x] `WeatherForecast` and `HolidayInfo` records match the fields used in the spec's example response (`MaxPrecipitationProbabilityPercent`, `MaxWindSpeedKmh`, `IsPublicHoliday`, `Name`)
- [x] `RuleReason` record has `Rule`, `Severity`, `Triggered`, `Message`; `RuleSeverity` enum has `Blocking`/`Advisory`
- [x] `EventAdvisorRequest`/`EventAdvisorResponse` records match the spec's JSON contract
- [x] `EventRuleThresholds` has `MaxPrecipitationProbabilityPercent` and `MaxWindSpeedKmh` properties with the spec's defaults (70, 40)

**Verification:**
- [x] `dotnet build` succeeds

**Dependencies:** Task 1

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/Models/WeatherForecast.cs`
- `src/EventGoNoGoAdvisor.Api/Models/HolidayInfo.cs`
- `src/EventGoNoGoAdvisor.Api/Models/RuleReason.cs`
- `src/EventGoNoGoAdvisor.Api/Models/EventAdvisorContracts.cs`
- `src/EventGoNoGoAdvisor.Api/Options/EventRuleThresholds.cs`

**Estimated scope:** Small

---

## Checkpoint: Foundation
- [x] `dotnet build` succeeds
- [x] `dotnet test` passes (0 tests)
- [x] Task 2 findings reviewed; any spec corrections applied (none needed)
- [ ] Review with human before proceeding

## Phase 2: Rule Engine (pure business logic)

### Task 4: RainProbabilityRule + unit tests

**Description:** Define `IEventRule` (`RuleReason Evaluate(WeatherForecast forecast, HolidayInfo holiday)`) and implement the first blocking rule: triggers when `forecast.MaxPrecipitationProbabilityPercent` exceeds the configured threshold. Constructor takes `IOptions<EventRuleThresholds>`. Cover both branches in tests — no HTTP involved.

**Acceptance criteria:**
- [x] `IEventRule` interface defined
- [x] `RainProbabilityRule` triggers `NO_GO`-eligible (`Triggered: true`, `Severity: Blocking`) when probability > threshold, and `Triggered: false` otherwise
- [x] Message text reflects actual value and threshold (per spec's Code Style example)

**Verification:**
- [x] `dotnet test --filter RainProbabilityRuleTests` passes
- [x] Test asserts both the triggered and non-triggered branch

**Dependencies:** Task 3

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/Rules/IEventRule.cs`
- `src/EventGoNoGoAdvisor.Api/Rules/RainProbabilityRule.cs`
- `tests/EventGoNoGoAdvisor.Tests/Rules/RainProbabilityRuleTests.cs`

**Estimated scope:** Small

---

### Task 5: WindSpeedRule + unit tests

**Description:** Second blocking rule, same shape as Task 4: triggers when `forecast.MaxWindSpeedKmh` exceeds the configured threshold.

**Acceptance criteria:**
- [x] `WindSpeedRule` triggers `Triggered: true, Severity: Blocking` when wind speed > threshold, `Triggered: false` otherwise

**Verification:**
- [x] `dotnet test --filter WindSpeedRuleTests` passes, covering both branches

**Dependencies:** Task 4 (reuses `IEventRule`)

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/Rules/WindSpeedRule.cs`
- `tests/EventGoNoGoAdvisor.Tests/Rules/WindSpeedRuleTests.cs`

**Estimated scope:** Small

---

### Task 6: PublicHolidayRule + unit tests

**Description:** Advisory-only rule: triggers (`Severity: Advisory`) when `holiday.IsPublicHoliday` is true, surfacing the holiday name in the message. Per the spec, this rule must never be able to flip the final verdict by itself — enforce that at the engine level (Task 7), but keep this rule itself a simple pure check.

**Acceptance criteria:**
- [x] `PublicHolidayRule` returns `Triggered: true, Severity: Advisory` with the holiday name in the message when `IsPublicHoliday` is true
- [x] Returns `Triggered: false` when not a holiday

**Verification:**
- [x] `dotnet test --filter PublicHolidayRuleTests` passes, covering both branches

**Dependencies:** Task 4 (reuses `IEventRule`)

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/Rules/PublicHolidayRule.cs`
- `tests/EventGoNoGoAdvisor.Tests/Rules/PublicHolidayRuleTests.cs`

**Estimated scope:** Small

---

### Task 7: EventRuleEngine (aggregator) + unit tests

**Description:** `EventRuleEngine` takes an ordered `IEnumerable<IEventRule>`, evaluates all of them against a given `(WeatherForecast, HolidayInfo)`, and produces the final `verdict` + full `reasons` list (every rule's output, triggered or not — per the spec's API Contract). Verdict is `NO_GO` if any `Blocking` rule triggered, else `GO`; an `Advisory` rule triggering never changes the verdict. Test with simple fake `IEventRule` implementations (not the real rules) to keep this test isolated to aggregation logic.

**Acceptance criteria:**
- [x] A triggered `Blocking` rule produces `NO_GO` regardless of other rules
- [x] A triggered `Advisory` rule alone produces `GO`
- [x] `reasons` in the output contains one entry per input rule, in order, including non-triggered ones

**Verification:**
- [x] `dotnet test --filter EventRuleEngineTests` passes, covering: all-clear → `GO`; blocking triggered → `NO_GO`; advisory-only triggered → `GO` with reason present

**Dependencies:** Task 4 (reuses `IEventRule`)

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/Rules/EventRuleEngine.cs`
- `tests/EventGoNoGoAdvisor.Tests/Rules/EventRuleEngineTests.cs`

**Estimated scope:** Small

---

## Checkpoint: Rule Engine
- [x] All rule and engine tests pass (`dotnet test --filter Rules`)
- [x] Zero HTTP/I/O in any file under `Rules/`
- [ ] Review with human before proceeding

## Phase 3: External Clients

### Task 8: OpenMeteoWeatherClient + unit tests

**Description:** `IWeatherClient` with a single method (e.g. `Task<WeatherForecast> GetForecastAsync(double lat, double lon, DateOnly date)`). `OpenMeteoWeatherClient` calls the Open-Meteo forecast endpoint via an injected typed `HttpClient` and maps the response to `WeatherForecast`, using the real field names/units confirmed in Task 2. Introduce a shared `FakeHttpMessageHandler` test helper (returns a canned response or throws, per test) for mocking HTTP without a real network call; this helper is reused by Task 9.

**Acceptance criteria:**
- [x] Success case: given a canned Open-Meteo JSON fixture, `GetForecastAsync` returns a correctly-mapped `WeatherForecast`
- [x] Failure case: a non-2xx response or network exception surfaces as a typed exception/result, not an unhandled throw into the caller
- [x] No test makes a real network call

**Verification:**
- [x] `dotnet test --filter OpenMeteoWeatherClientTests` passes (success + failure)

**Dependencies:** Task 3 (models), Task 2 (confirmed response shape)

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/Clients/IWeatherClient.cs`
- `src/EventGoNoGoAdvisor.Api/Clients/OpenMeteoWeatherClient.cs`
- `tests/EventGoNoGoAdvisor.Tests/Clients/OpenMeteoWeatherClientTests.cs`
- `tests/EventGoNoGoAdvisor.Tests/TestHelpers/FakeHttpMessageHandler.cs`

**Estimated scope:** Medium

---

### Task 9: NagerDateHolidayClient + unit tests

**Description:** `IHolidayClient` with a single method (e.g. `Task<HolidayInfo> GetHolidayInfoAsync(string countryCode, DateOnly date)`). `NagerDateHolidayClient` fetches `/api/v3/PublicHolidays/{year}/{countryCode}` and checks whether `date` appears in the list, per the shape confirmed in Task 2. Reuses the `FakeHttpMessageHandler` helper from Task 8.

**Acceptance criteria:**
- [x] Success case: canned Nager.Date JSON fixture containing a matching date maps to `HolidayInfo { IsPublicHoliday: true, Name: ... }`; a fixture with no matching date maps to `IsPublicHoliday: false`
- [x] Failure case: non-2xx/network exception surfaces as a typed exception/result
- [x] No test makes a real network call

**Verification:**
- [x] `dotnet test --filter NagerDateHolidayClientTests` passes (success + failure)

**Dependencies:** Task 3, Task 2, Task 8 (reuses `FakeHttpMessageHandler`)

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/Clients/IHolidayClient.cs`
- `src/EventGoNoGoAdvisor.Api/Clients/NagerDateHolidayClient.cs`
- `tests/EventGoNoGoAdvisor.Tests/Clients/NagerDateHolidayClientTests.cs`

**Estimated scope:** Small

---

## Checkpoint: Clients
- [x] All client tests pass against the mocked handler
- [ ] Review with human before proceeding

## Phase 4: Endpoint

### Task 10: DI wiring, Options binding, Swagger setup

**Description:** In `Program.cs`, register typed `HttpClient`s for `IWeatherClient`/`IHolidayClient` via `AddHttpClient<TInterface, TImplementation>`, bind `EventRuleThresholds` from an `EventRuleThresholds` section via `IOptions<T>`, register `IEventRule` implementations and `EventRuleEngine` in DI, and add Swashbuckle (Swagger generation + UI in Development). Populate `appsettings.json` with the default thresholds from the spec.

**Acceptance criteria:**
- [x] App starts (`dotnet run`) without DI resolution errors
- [x] `EventRuleThresholds` section present in `appsettings.json` with values 70 / 40
- [x] `/swagger` loads in Development

**Verification:**
- [x] `dotnet build` succeeds
- [x] Manual: `dotnet run`, open `/swagger`, confirm it loads with no endpoint yet (or a placeholder) — full endpoint comes in Task 11

**Dependencies:** Task 1, Task 3

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/Program.cs`
- `src/EventGoNoGoAdvisor.Api/appsettings.json`
- `src/EventGoNoGoAdvisor.Api/appsettings.Development.json`

**Estimated scope:** Small

---

### Task 11: POST /event-advisor endpoint, input validation, upstream-failure handling

**Description:** Map `POST /event-advisor` per the spec's API Contract. Validate `eventDate` (parseable `yyyy-MM-dd`, not in the past, not more than the confirmed forecast-horizon days ahead), `latitude`/`longitude` ranges, and `countryCode` format — return `400` with field-level messages on failure. On success, call both clients, run them through `EventRuleEngine`, and return `200` with the verdict/reasons/weather/holiday payload. On an upstream client failure, return `502`/`503` naming which upstream failed, never a fabricated `200`.

**Acceptance criteria:**
- [x] Valid payload → `200` with full `EventAdvisorResponse` matching the spec's shape
- [x] Invalid date/coords/country → `400` naming the invalid field
- [x] Weather or holiday client failure (simulated) → `502`/`503` naming the failed upstream, not a default verdict

**Verification:**
- [x] `dotnet test` passes (endpoint tests via `WebApplicationFactory` skipped — would require a new NuGet package (`Microsoft.AspNetCore.Mvc.Testing`) beyond the spec's pre-approved list, and the spec's Boundaries say to ask first before adding one; verified manually instead, which the task allows)
- [x] Manual: call the endpoint via Swagger/curl with a valid payload and confirm the verdict

**Notes:** Manual curl testing against the real app (Development mode) confirmed: 200 with correct shape for a valid payload; 400 with field-level messages for bad date format, past date, >15-days-ahead date, out-of-range latitude, and malformed country code; 502 naming the failed upstream when Nager.Date returns 404 for an unrecognized country code. Also found and fixed a real bug: Open-Meteo can return `null` for `precipitation_probability_max` on later days within the forecast window (data-freshness dependent, not a fixed cutoff) even while `wind_speed_10m_max` is populated — the client originally crashed with a generic "unexpected response shape" `WeatherClientException`. Fixed by deserializing to nullable arrays and throwing a specific "no complete forecast data yet for {date}" exception instead; covered by a new client test.

**Dependencies:** Task 7 (rule engine), Task 8, Task 9 (clients), Task 10 (DI wiring)

**Files likely touched:**
- `src/EventGoNoGoAdvisor.Api/Endpoints/EventAdvisorEndpoints.cs`
- `src/EventGoNoGoAdvisor.Api/Program.cs`

**Estimated scope:** Medium

---

## Checkpoint: Endpoint
- [x] `dotnet test` passes in full
- [x] Swagger documents the endpoint correctly
- [x] Manual call through Swagger returns a sensible verdict
- [ ] Review with human before proceeding

## Phase 5: Verification & Docs

### Task 12: Manual end-to-end smoke test against live APIs

**Description:** With the app running against the real Open-Meteo and Nager.Date APIs (no mocks), run the three scenarios from the spec's Success Criteria: clear weather + non-holiday → `GO`; high rain or wind forecast → `NO_GO`; holiday + clear weather → `GO` with an advisory reason present. Capture one real request/response pair for the README.

**Acceptance criteria:**
- [x] All three scenarios produce the expected verdict against live data
- [x] One real sample request/response captured for documentation

**Verification:**
- [x] Manual — no automated test applies (this exercises real upstream APIs)

**Findings (2026-10-25), against the running app with live Open-Meteo/Nager.Date:**

1. Clear weather + non-holiday (New York, 2026-11-05, US) → `GO`, no reasons triggered.
2. High rain forecast (Singapore, 2026-10-27, SG — live forecast showed 96% precipitation probability) → `NO_GO`, `RainProbabilityRule` triggered.
3. Holiday + clear weather (Paris, 2026-11-01, FR — All Saints' Day) → `GO` with `PublicHolidayRule` triggered as an advisory reason, verdict unaffected.

Sample pair for the README is scenario 1 (closest to the spec's own example). Full JSON captured for all three; see README.

**Dependencies:** Task 11

**Files likely touched:** None (verification only; sample captured for Task 13)

**Estimated scope:** Small

---

### Task 13: README

**Description:** Write the README covering how to run the app and tests, key design decisions (pure rules tested without HTTP, advisory-vs-blocking split, why thresholds are configurable), the real sample request/response from Task 12, and an explicit "left out and why" section mirroring the idea doc's Not Doing list (no geocoding, no multi-date, no persistence, no caching/retry, no auth).

**Acceptance criteria:**
- [x] README includes run instructions, design rationale, a sample request/response, and a "left out and why" section
- [x] All Success Criteria from the spec are verifiably met

**Verification:**
- [x] Manual review against the spec's Success Criteria list

**Dependencies:** Task 12

**Files likely touched:**
- `README.md`

**Estimated scope:** Small

---

## Checkpoint: Complete
- [x] All Success Criteria in the spec are met
- [x] `dotnet build` and `dotnet test` both succeed
- [x] README is accurate and complete
- [ ] Ready for human review
