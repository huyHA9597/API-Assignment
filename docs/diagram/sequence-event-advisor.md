# Sequence Diagram: `POST /event-advisor`

Request flow through validation, the two upstream clients, and the rule
engine, including the `400`/`502` error paths from
[ADR-003](../decisions/003-harden-against-resource-abuse-and-info-leaks.md).

```mermaid
sequenceDiagram
    actor Client
    participant RL as RateLimiter<br/>(20 req/min per IP)
    participant EP as EventAdvisorEndpoints
    participant WC as OpenMeteoWeatherClient
    participant HC as NagerDateHolidayClient
    participant RE as EventRuleEngine
    participant OM as Open-Meteo API
    participant ND as Nager.Date API

    Client->>RL: POST /event-advisor<br/>{eventDate, latitude, longitude, countryCode}

    alt rate limit exceeded
        RL-->>Client: 429 Too Many Requests
    else within limit
        RL->>EP: forward request

        EP->>EP: validate eventDate, latitude,<br/>longitude, countryCode

        alt validation fails
            EP-->>Client: 400 ValidationProblem<br/>(field-level error messages)
        else validation passes
            EP->>WC: GetForecastAsync(lat, lon, date)
            WC->>OM: GET /v1/forecast?... (10s timeout)

            alt Open-Meteo fails / times out
                OM-->>WC: error / timeout
                WC-->>EP: throws WeatherClientException
                EP-->>Client: 502 Bad Gateway<br/>"weather forecast service is currently unavailable"
            else Open-Meteo succeeds
                OM-->>WC: 200 daily forecast JSON
                WC-->>EP: WeatherForecast

                EP->>HC: GetHolidayInfoAsync(countryCode, date)
                HC->>ND: GET /api/v3/PublicHolidays/{year}/{countryCode} (10s timeout)

                alt Nager.Date fails / times out
                    ND-->>HC: error / timeout
                    HC-->>EP: throws HolidayClientException
                    EP-->>Client: 502 Bad Gateway<br/>"public holiday service is currently unavailable"
                else Nager.Date succeeds
                    ND-->>HC: 200 holiday list JSON
                    HC-->>EP: HolidayInfo

                    EP->>RE: Evaluate(weather, holiday)
                    RE->>RE: run RainProbabilityRule,<br/>WindSpeedRule, PublicHolidayRule
                    RE-->>EP: verdict (GO/NO_GO) + reasons

                    EP-->>Client: 200 OK<br/>{verdict, reasons, weather, holiday}
                end
            end
        end
    end
```

See also: [architecture diagram](architecture.md), [README](../../README.md).
