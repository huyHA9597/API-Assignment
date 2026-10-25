using EventGoNoGoAdvisor.Api.Models;
using EventGoNoGoAdvisor.Api.Rules;

namespace EventGoNoGoAdvisor.Tests.Rules;

public class PublicHolidayRuleTests
{
    [Fact]
    public void Evaluate_WhenIsPublicHoliday_TriggersAdvisoryReasonWithHolidayName()
    {
        var rule = new PublicHolidayRule();
        var forecast = new WeatherForecast(MaxPrecipitationProbabilityPercent: 10, MaxWindSpeedKmh: 10);
        var holiday = new HolidayInfo(IsPublicHoliday: true, Name: "Veterans Day");

        var reason = rule.Evaluate(forecast, holiday);

        Assert.True(reason.Triggered);
        Assert.Equal(RuleSeverity.Advisory, reason.Severity);
        Assert.Equal(nameof(PublicHolidayRule), reason.Rule);
        Assert.Contains("Veterans Day", reason.Message);
    }

    [Fact]
    public void Evaluate_WhenNotPublicHoliday_DoesNotTrigger()
    {
        var rule = new PublicHolidayRule();
        var forecast = new WeatherForecast(MaxPrecipitationProbabilityPercent: 10, MaxWindSpeedKmh: 10);
        var holiday = new HolidayInfo(IsPublicHoliday: false, Name: null);

        var reason = rule.Evaluate(forecast, holiday);

        Assert.False(reason.Triggered);
        Assert.Equal(RuleSeverity.Advisory, reason.Severity);
    }
}
