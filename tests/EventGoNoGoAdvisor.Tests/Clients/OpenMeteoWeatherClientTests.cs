using System.Globalization;
using System.Net;
using System.Text;
using EventGoNoGoAdvisor.Api.Clients;
using EventGoNoGoAdvisor.Tests.TestHelpers;

namespace EventGoNoGoAdvisor.Tests.Clients;

public class OpenMeteoWeatherClientTests
{
    private const string SuccessFixture = """
        {
          "latitude": 40.71,
          "longitude": -74.0,
          "daily_units": { "precipitation_probability_max": "%", "wind_speed_10m_max": "km/h" },
          "daily": {
            "time": ["2026-11-04", "2026-11-05", "2026-11-06"],
            "precipitation_probability_max": [16, 25, 26],
            "wind_speed_10m_max": [18.1, 18.7, 21.5]
          }
        }
        """;

    private static OpenMeteoWeatherClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.open-meteo.com") };
        return new OpenMeteoWeatherClient(httpClient);
    }

    [Fact]
    public async Task GetForecastAsync_WhenRequestSucceeds_ReturnsMappedForecastForRequestedDate()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SuccessFixture, Encoding.UTF8, "application/json")
        });

        var forecast = await client.GetForecastAsync(40.7128, -74.0060, new DateOnly(2026, 11, 5));

        Assert.Equal(25, forecast.MaxPrecipitationProbabilityPercent);
        Assert.Equal(18.7, forecast.MaxWindSpeedKmh);
    }

    [Fact]
    public async Task GetForecastAsync_WhenResponseIsNonSuccess_ThrowsWeatherClientException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"reason":"bad request","error":true}""", Encoding.UTF8, "application/json")
        });

        await Assert.ThrowsAsync<WeatherClientException>(
            () => client.GetForecastAsync(40.7128, -74.0060, new DateOnly(2026, 11, 5)));
    }

    [Fact]
    public async Task GetForecastAsync_WhenNetworkFails_ThrowsWeatherClientException()
    {
        var client = CreateClient(_ => throw new HttpRequestException("network down"));

        await Assert.ThrowsAsync<WeatherClientException>(
            () => client.GetForecastAsync(40.7128, -74.0060, new DateOnly(2026, 11, 5)));
    }

    [Fact]
    public async Task GetForecastAsync_WhenRequestedDateHasNullForecastValues_ThrowsWeatherClientException()
    {
        // Open-Meteo can return null precipitation_probability_max for the tail of its forecast
        // window (data-freshness dependent) even though the date is within forecast_days=16.
        const string fixtureWithNullTail = """
            {
              "daily_units": { "precipitation_probability_max": "%", "wind_speed_10m_max": "km/h" },
              "daily": {
                "time": ["2026-11-04", "2026-11-05", "2026-11-06"],
                "precipitation_probability_max": [16, null, 26],
                "wind_speed_10m_max": [18.1, 18.7, 21.5]
              }
            }
            """;
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(fixtureWithNullTail, Encoding.UTF8, "application/json")
        });

        var exception = await Assert.ThrowsAsync<WeatherClientException>(
            () => client.GetForecastAsync(40.7128, -74.0060, new DateOnly(2026, 11, 5)));
        Assert.Contains("2026-11-05", exception.Message);
        Assert.DoesNotContain("unexpected response shape", exception.Message);
    }

    [Fact]
    public async Task GetForecastAsync_BuildsRequestUriWithInvariantCultureCoordinates()
    {
        // String-interpolating a double uses CurrentCulture by default (e.g. "40,7128" under
        // de-DE), which would corrupt the query string sent to Open-Meteo. Coordinates must be
        // formatted with InvariantCulture regardless of the host's locale.
        HttpRequestMessage? capturedRequest = null;
        var client = CreateClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessFixture, Encoding.UTF8, "application/json")
            };
        });

        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            await client.GetForecastAsync(40.7128, -74.0060, new DateOnly(2026, 11, 5));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        var query = capturedRequest!.RequestUri!.Query;
        Assert.Contains("latitude=40.7128", query);
        Assert.Contains("longitude=-74.006", query);
    }
}
