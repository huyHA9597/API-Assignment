# Architecture Diagram

Component view of `src/`, showing how the API project, its external clients,
the pure rule engine, the shared `ServiceDefaults`, and the Aspire `AppHost`
relate to each other and to the two upstream APIs.

```mermaid
graph TB
    subgraph External["External, keyless APIs"]
        OpenMeteo["Open-Meteo<br/>(weather forecast)"]
        NagerDate["Nager.Date<br/>(public holidays)"]
    end

    subgraph AppHost["EventGoNoGoAdvisor.AppHost"]
        AppHostCs["AppHost.cs<br/>orchestrates Api + Aspire Dashboard"]
    end

    subgraph Api["EventGoNoGoAdvisor.Api"]
        Program["Program.cs<br/>DI wiring, Options binding, Swagger, rate limiting, security headers"]

        subgraph Endpoints["Endpoints/"]
            EventAdvisorEndpoints["EventAdvisorEndpoints<br/>POST /event-advisor: validation + orchestration"]
        end

        subgraph Clients["Clients/"]
            IWeatherClient["IWeatherClient"]
            OpenMeteoWeatherClient["OpenMeteoWeatherClient"]
            IHolidayClient["IHolidayClient"]
            NagerDateHolidayClient["NagerDateHolidayClient"]
        end

        subgraph Rules["Rules/ (pure, no HTTP/IO)"]
            IEventRule["IEventRule"]
            RainProbabilityRule["RainProbabilityRule (Blocking)"]
            WindSpeedRule["WindSpeedRule (Blocking)"]
            PublicHolidayRule["PublicHolidayRule (Advisory)"]
            EventRuleEngine["EventRuleEngine<br/>aggregates reasons into GO/NO_GO verdict"]
        end

        subgraph Models["Models/"]
            DomainModels["WeatherForecast, HolidayInfo,<br/>RuleReason, EventAdvisorContracts"]
        end

        subgraph Options["Options/"]
            EventRuleThresholds["EventRuleThresholds<br/>bound from appsettings.json"]
        end
    end

    subgraph ServiceDefaults["EventGoNoGoAdvisor.ServiceDefaults"]
        Extensions["Extensions.cs<br/>OpenTelemetry (traces/metrics/logs) + /health, /alive"]
    end

    AppHostCs -->|orchestrates, injects OTLP endpoint| Program
    Program --> EventAdvisorEndpoints
    Program -->|AddServiceDefaults / MapDefaultEndpoints| Extensions
    Program -->|DI registration| Clients
    Program -->|DI registration| Rules
    Program -->|Configure| EventRuleThresholds

    EventAdvisorEndpoints --> IWeatherClient
    EventAdvisorEndpoints --> IHolidayClient
    EventAdvisorEndpoints --> EventRuleEngine
    EventAdvisorEndpoints --> DomainModels

    IWeatherClient -.implements.- OpenMeteoWeatherClient
    IHolidayClient -.implements.- NagerDateHolidayClient
    OpenMeteoWeatherClient -->|HTTPS, 10s timeout| OpenMeteo
    NagerDateHolidayClient -->|HTTPS, 10s timeout| NagerDate

    IEventRule -.implements.- RainProbabilityRule
    IEventRule -.implements.- WindSpeedRule
    IEventRule -.implements.- PublicHolidayRule
    EventRuleEngine --> RainProbabilityRule
    EventRuleEngine --> WindSpeedRule
    EventRuleEngine --> PublicHolidayRule
    RainProbabilityRule --> EventRuleThresholds
    WindSpeedRule --> EventRuleThresholds
```

See also: [sequence diagram of `POST /event-advisor`](sequence-event-advisor.md),
[README](../../README.md), [ADR-004: telemetry via Aspire ServiceDefaults](../decisions/004-telemetry-via-aspire-service-defaults.md).
