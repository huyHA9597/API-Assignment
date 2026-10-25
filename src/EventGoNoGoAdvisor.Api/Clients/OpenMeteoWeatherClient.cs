using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventGoNoGoAdvisor.Api.Models;

namespace EventGoNoGoAdvisor.Api.Clients;

public sealed class OpenMeteoWeatherClient : IWeatherClient
{
    private readonly HttpClient _httpClient;

    public OpenMeteoWeatherClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, DateOnly date, CancellationToken cancellationToken = default)
    {
        // InvariantCulture is required here: string-interpolating a double uses CurrentCulture,
        // which would send "40,7128" instead of "40.7128" on a comma-decimal host locale (e.g. de-DE).
        var requestUri = $"/v1/forecast?latitude={latitude.ToString(CultureInfo.InvariantCulture)}&longitude={longitude.ToString(CultureInfo.InvariantCulture)}"
            + "&daily=precipitation_probability_max,wind_speed_10m_max&forecast_days=16";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new WeatherClientException("Failed to reach the Open-Meteo weather service.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new WeatherClientException(
                $"Open-Meteo weather service returned {(int)response.StatusCode} {response.StatusCode}.");
        }

        OpenMeteoResponse? parsed;
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            parsed = JsonSerializer.Deserialize<OpenMeteoResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new WeatherClientException("Open-Meteo weather service returned an unexpected response shape.", ex);
        }

        if (parsed?.Daily is null)
        {
            throw new WeatherClientException("Open-Meteo weather service returned an unexpected response shape.");
        }

        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var index = Array.IndexOf(parsed.Daily.Time, dateText);
        if (index < 0)
        {
            throw new WeatherClientException($"Open-Meteo weather service has no forecast data for {dateText}.");
        }

        var precipitation = parsed.Daily.PrecipitationProbabilityMax[index];
        var windSpeed = parsed.Daily.WindSpeedMax[index];
        if (precipitation is null || windSpeed is null)
        {
            // Open-Meteo can return null for the tail of its forecast window (data-freshness
            // dependent) even for a date within the otherwise-supported forecast_days range.
            throw new WeatherClientException($"Open-Meteo weather service has no complete forecast data yet for {dateText}.");
        }

        return new WeatherForecast(
            MaxPrecipitationProbabilityPercent: precipitation.Value,
            MaxWindSpeedKmh: windSpeed.Value);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record OpenMeteoResponse([property: JsonPropertyName("daily")] OpenMeteoDaily? Daily);

    private sealed record OpenMeteoDaily(
        [property: JsonPropertyName("time")] string[] Time,
        [property: JsonPropertyName("precipitation_probability_max")] int?[] PrecipitationProbabilityMax,
        [property: JsonPropertyName("wind_speed_10m_max")] double?[] WindSpeedMax);
}
