using System.Net;
using System.Text;
using EventGoNoGoAdvisor.Api.Clients;
using EventGoNoGoAdvisor.Tests.TestHelpers;

namespace EventGoNoGoAdvisor.Tests.Clients;

public class NagerDateHolidayClientTests
{
    private const string FixtureWithMatchingDate = """
        [
          { "date": "2026-01-01", "localName": "New Year's Day", "name": "New Year's Day", "countryCode": "US" },
          { "date": "2026-11-11", "localName": "Veterans Day", "name": "Veterans Day", "countryCode": "US" }
        ]
        """;

    private static NagerDateHolidayClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://date.nager.at") };
        return new NagerDateHolidayClient(httpClient);
    }

    [Fact]
    public async Task GetHolidayInfoAsync_WhenDateMatchesAHoliday_ReturnsIsPublicHolidayTrueWithName()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureWithMatchingDate, Encoding.UTF8, "application/json")
        });

        var holiday = await client.GetHolidayInfoAsync("US", new DateOnly(2026, 11, 11));

        Assert.True(holiday.IsPublicHoliday);
        Assert.Equal("Veterans Day", holiday.Name);
    }

    [Fact]
    public async Task GetHolidayInfoAsync_WhenDateDoesNotMatchAnyHoliday_ReturnsIsPublicHolidayFalse()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FixtureWithMatchingDate, Encoding.UTF8, "application/json")
        });

        var holiday = await client.GetHolidayInfoAsync("US", new DateOnly(2026, 11, 5));

        Assert.False(holiday.IsPublicHoliday);
        Assert.Null(holiday.Name);
    }

    [Fact]
    public async Task GetHolidayInfoAsync_WhenResponseIsNonSuccess_ThrowsHolidayClientException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<HolidayClientException>(
            () => client.GetHolidayInfoAsync("US", new DateOnly(2026, 11, 5)));
    }

    [Fact]
    public async Task GetHolidayInfoAsync_WhenNetworkFails_ThrowsHolidayClientException()
    {
        var client = CreateClient(_ => throw new HttpRequestException("network down"));

        await Assert.ThrowsAsync<HolidayClientException>(
            () => client.GetHolidayInfoAsync("US", new DateOnly(2026, 11, 5)));
    }
}
