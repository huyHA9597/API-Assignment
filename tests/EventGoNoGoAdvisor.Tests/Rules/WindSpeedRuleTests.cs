using EventGoNoGoAdvisor.Api.Models;
using EventGoNoGoAdvisor.Api.Options;
using EventGoNoGoAdvisor.Api.Rules;
using Microsoft.Extensions.Options;

namespace EventGoNoGoAdvisor.Tests.Rules;

public class WindSpeedRuleTests
{
    private static WindSpeedRule CreateRule(double maxWindSpeedKmh = 40)
    {
        var options = Options.Create(new EventRuleThresholds
        {
            MaxWindSpeedKmh = maxWindSpeedKmh
        });
        return new WindSpeedRule(options);
    }

    [Fact]
    public void Evaluate_WhenWindSpeedExceedsThreshold_TriggersBlockingReason()
    {
        var rule = CreateRule(maxWindSpeedKmh: 40);
        var forecast = new WeatherForecast(MaxPrecipitationProbabilityPercent: 10, MaxWindSpeedKmh: 55.2);
        var holiday = new HolidayInfo(IsPublicHoliday: false, Name: null);

        var reason = rule.Evaluate(forecast, holiday);

        Assert.True(reason.Triggered);
        Assert.Equal(RuleSeverity.Blocking, reason.Severity);
        Assert.Equal(nameof(WindSpeedRule), reason.Rule);
        Assert.Contains("55.2", reason.Message);
        Assert.Contains("40", reason.Message);
    }

    [Fact]
    public void Evaluate_WhenWindSpeedAtOrBelowThreshold_DoesNotTrigger()
    {
        var rule = CreateRule(maxWindSpeedKmh: 40);
        var forecast = new WeatherForecast(MaxPrecipitationProbabilityPercent: 10, MaxWindSpeedKmh: 18.4);
        var holiday = new HolidayInfo(IsPublicHoliday: false, Name: null);

        var reason = rule.Evaluate(forecast, holiday);

        Assert.False(reason.Triggered);
        Assert.Equal(RuleSeverity.Blocking, reason.Severity);
    }
}
