using EventGoNoGoAdvisor.Api.Models;

namespace EventGoNoGoAdvisor.Api.Rules;

public sealed class PublicHolidayRule : IEventRule
{
    public RuleReason Evaluate(WeatherForecast forecast, HolidayInfo holiday)
    {
        return new RuleReason(
            Rule: nameof(PublicHolidayRule),
            Severity: RuleSeverity.Advisory,
            Triggered: holiday.IsPublicHoliday,
            Message: holiday.IsPublicHoliday
                ? $"This date is a public holiday ({holiday.Name}) — expect staffing/attendance risk."
                : "This date is not a public holiday.");
    }
}
