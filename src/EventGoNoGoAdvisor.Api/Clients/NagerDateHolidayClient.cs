using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventGoNoGoAdvisor.Api.Models;

namespace EventGoNoGoAdvisor.Api.Clients;

public sealed class NagerDateHolidayClient : IHolidayClient
{
    private readonly HttpClient _httpClient;

    public NagerDateHolidayClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<HolidayInfo> GetHolidayInfoAsync(string countryCode, DateOnly date, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/v3/PublicHolidays/{date.Year}/{countryCode}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new HolidayClientException("Failed to reach the Nager.Date holiday service.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HolidayClientException(
                $"Nager.Date holiday service returned {(int)response.StatusCode} {response.StatusCode}.");
        }

        NagerHoliday[]? holidays;
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            holidays = JsonSerializer.Deserialize<NagerHoliday[]>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new HolidayClientException("Nager.Date holiday service returned an unexpected response shape.", ex);
        }

        if (holidays is null)
        {
            throw new HolidayClientException("Nager.Date holiday service returned an unexpected response shape.");
        }

        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var match = holidays.FirstOrDefault(h => h.Date == dateText);

        return match is null
            ? new HolidayInfo(IsPublicHoliday: false, Name: null)
            : new HolidayInfo(IsPublicHoliday: true, Name: match.Name);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record NagerHoliday(
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("name")] string Name);
}
