using EventGoNoGoAdvisor.Api.Models;

namespace EventGoNoGoAdvisor.Api.Clients;

public interface IWeatherClient
{
    Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, DateOnly date, CancellationToken cancellationToken = default);
}

public sealed class WeatherClientException : Exception
{
    public WeatherClientException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
