using EventGoNoGoAdvisor.Api.Models;
using EventGoNoGoAdvisor.Api.Options;
using Microsoft.Extensions.Options;

namespace EventGoNoGoAdvisor.Api.Rules;

public sealed class RainProbabilityRule : IEventRule
{
    private readonly double _maxPrecipitationProbabilityPercent;

    public RainProbabilityRule(IOptions<EventRuleThresholds> thresholds)
        => _maxPrecipitationProbabilityPercent = thresholds.Value.MaxPrecipitationProbabilityPercent;

    public RuleReason Evaluate(WeatherForecast forecast, HolidayInfo holiday)
    {
        var triggered = forecast.MaxPrecipitationProbabilityPercent > _maxPrecipitationProbabilityPercent;
        return new RuleReason(
            Rule: nameof(RainProbabilityRule),
            Severity: RuleSeverity.Blocking,
            Triggered: triggered,
            Message: $"Max precipitation probability {forecast.MaxPrecipitationProbabilityPercent}% is "
                + (triggered ? $"above the {_maxPrecipitationProbabilityPercent}% threshold." : $"below the {_maxPrecipitationProbabilityPercent}% threshold."));
    }
}
