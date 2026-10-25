using System.Globalization;
using System.Text.RegularExpressions;
using EventGoNoGoAdvisor.Api.Clients;
using EventGoNoGoAdvisor.Api.Models;
using EventGoNoGoAdvisor.Api.Rules;

namespace EventGoNoGoAdvisor.Api.Endpoints;

public static class EventAdvisorEndpoints
{
    private const int MaxForecastHorizonDays = 15;
    private static readonly Regex CountryCodePattern = new("^[A-Za-z]{2}$", RegexOptions.Compiled);

    public static void MapEventAdvisorEndpoints(this WebApplication app, string rateLimitPolicy)
    {
        app.MapPost("/event-advisor", HandleAsync).RequireRateLimiting(rateLimitPolicy);
    }

    private static async Task<IResult> HandleAsync(
        EventAdvisorRequest request,
        IWeatherClient weatherClient,
        IHolidayClient holidayClient,
        EventRuleEngine ruleEngine,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("EventAdvisorEndpoints");
        var errors = new Dictionary<string, string[]>();
        var eventDate = ValidateEventDate(request.EventDate, errors);
        ValidateLatitude(request.Latitude, errors);
        ValidateLongitude(request.Longitude, errors);
        ValidateCountryCode(request.CountryCode, errors);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var validatedEventDate = eventDate!.Value;

        WeatherForecast weather;
        try
        {
            weather = await weatherClient.GetForecastAsync(request.Latitude, request.Longitude, validatedEventDate, cancellationToken);
        }
        catch (WeatherClientException ex)
        {
            logger.LogError(ex, "Weather upstream (Open-Meteo) failed.");
            return Results.Problem(
                detail: "The weather forecast service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway,
                title: "Upstream weather service failure");
        }

        HolidayInfo holiday;
        try
        {
            holiday = await holidayClient.GetHolidayInfoAsync(request.CountryCode, validatedEventDate, cancellationToken);
        }
        catch (HolidayClientException ex)
        {
            logger.LogError(ex, "Holiday upstream (Nager.Date) failed.");
            return Results.Problem(
                detail: "The public holiday service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway,
                title: "Upstream holiday service failure");
        }

        var evaluation = ruleEngine.Evaluate(weather, holiday);
        var response = new EventAdvisorResponse(evaluation.Verdict, evaluation.Reasons, weather, holiday);
        return Results.Ok(response);
    }

    private static DateOnly? ValidateEventDate(string eventDate, Dictionary<string, string[]> errors)
    {
        if (!DateOnly.TryParseExact(eventDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            errors["eventDate"] = [$"'{eventDate}' is not a valid date in yyyy-MM-dd format."];
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (parsedDate < today)
        {
            errors["eventDate"] = ["eventDate cannot be in the past."];
            return null;
        }

        if (parsedDate > today.AddDays(MaxForecastHorizonDays))
        {
            errors["eventDate"] = [$"eventDate cannot be more than {MaxForecastHorizonDays} days in the future."];
            return null;
        }

        return parsedDate;
    }

    private static void ValidateLatitude(double latitude, Dictionary<string, string[]> errors)
    {
        if (latitude < -90 || latitude > 90)
        {
            errors["latitude"] = ["latitude must be between -90 and 90."];
        }
    }

    private static void ValidateLongitude(double longitude, Dictionary<string, string[]> errors)
    {
        if (longitude < -180 || longitude > 180)
        {
            errors["longitude"] = ["longitude must be between -180 and 180."];
        }
    }

    private static void ValidateCountryCode(string countryCode, Dictionary<string, string[]> errors)
    {
        if (!CountryCodePattern.IsMatch(countryCode ?? string.Empty))
        {
            errors["countryCode"] = ["countryCode must be a 2-letter ISO 3166-1 alpha-2 code."];
        }
    }
}
