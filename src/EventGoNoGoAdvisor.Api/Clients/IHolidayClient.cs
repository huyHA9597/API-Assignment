using EventGoNoGoAdvisor.Api.Models;

namespace EventGoNoGoAdvisor.Api.Clients;

public interface IHolidayClient
{
    Task<HolidayInfo> GetHolidayInfoAsync(string countryCode, DateOnly date, CancellationToken cancellationToken = default);
}

public sealed class HolidayClientException : Exception
{
    public HolidayClientException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
