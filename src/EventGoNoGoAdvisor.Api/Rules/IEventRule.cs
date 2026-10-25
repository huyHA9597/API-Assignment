using EventGoNoGoAdvisor.Api.Models;

namespace EventGoNoGoAdvisor.Api.Rules;

public interface IEventRule
{
    RuleReason Evaluate(WeatherForecast forecast, HolidayInfo holiday);
}
