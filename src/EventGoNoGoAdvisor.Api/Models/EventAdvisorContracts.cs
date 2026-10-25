namespace EventGoNoGoAdvisor.Api.Models;

public sealed record EventAdvisorRequest(string EventDate, double Latitude, double Longitude, string CountryCode);

public sealed record EventAdvisorResponse(
    string Verdict,
    IReadOnlyList<RuleReason> Reasons,
    WeatherForecast Weather,
    HolidayInfo Holiday);
