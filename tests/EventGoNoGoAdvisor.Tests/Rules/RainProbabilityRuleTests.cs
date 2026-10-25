using EventGoNoGoAdvisor.Api.Models;
using EventGoNoGoAdvisor.Api.Options;
using EventGoNoGoAdvisor.Api.Rules;
using Microsoft.Extensions.Options;

namespace EventGoNoGoAdvisor.Tests.Rules;

public class RainProbabilityRuleTests
{
    private static RainProbabilityRule CreateRule(double maxPrecipitationProbabilityPercent = 70)
    {
        var options = Options.Create(new EventRuleThresholds
        {
            MaxPrecipitationProbabilityPercent = maxPrecipitationProbabilityPercent
        });
        return new RainProbabilityRule(options);
    }

    [Fact]
    public void Evaluate_WhenProbabilityExceedsThreshold_TriggersBlockingReason()
    {
        var rule = CreateRule(maxPrecipitationProbabilityPercent: 70);
        var forecast = new WeatherForecast(MaxPrecipitationProbabilityPercent: 85, MaxWindSpeedKmh: 10);
        var holiday = new HolidayInfo(IsPublicHoliday: false, Name: null);

        var reason = rule.Evaluate(forecast, holiday);

        Assert.True(reason.Triggered);
        Assert.Equal(RuleSeverity.Blocking, reason.Severity);
        Assert.Equal(nameof(RainProbabilityRule), reason.Rule);
        Assert.Contains("85", reason.Message);
        Assert.Contains("70", reason.Message);
    }

    [Fact]
    public void Evaluate_WhenProbabilityAtOrBelowThreshold_DoesNotTrigger()
    {
        var rule = CreateRule(maxPrecipitationProbabilityPercent: 70);
        var forecast = new WeatherForecast(MaxPrecipitationProbabilityPercent: 20, MaxWindSpeedKmh: 10);
        var holiday = new HolidayInfo(IsPublicHoliday: false, Name: null);

        var reason = rule.Evaluate(forecast, holiday);

        Assert.False(reason.Triggered);
        Assert.Equal(RuleSeverity.Blocking, reason.Severity);
    }
}
