using EventGoNoGoAdvisor.Api.Models;
using EventGoNoGoAdvisor.Api.Options;
using Microsoft.Extensions.Options;

namespace EventGoNoGoAdvisor.Api.Rules;

public sealed class WindSpeedRule : IEventRule
{
    private readonly double _maxWindSpeedKmh;

    public WindSpeedRule(IOptions<EventRuleThresholds> thresholds)
        => _maxWindSpeedKmh = thresholds.Value.MaxWindSpeedKmh;

    public RuleReason Evaluate(WeatherForecast forecast, HolidayInfo holiday)
    {
        var triggered = forecast.MaxWindSpeedKmh > _maxWindSpeedKmh;
        return new RuleReason(
            Rule: nameof(WindSpeedRule),
            Severity: RuleSeverity.Blocking,
            Triggered: triggered,
            Message: $"Max wind speed {forecast.MaxWindSpeedKmh}km/h is "
                + (triggered ? $"above the {_maxWindSpeedKmh}km/h threshold." : $"below the {_maxWindSpeedKmh}km/h threshold."));
    }
}
