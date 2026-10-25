using EventGoNoGoAdvisor.Api.Models;

namespace EventGoNoGoAdvisor.Api.Rules;

public sealed record EventRuleEvaluation(string Verdict, IReadOnlyList<RuleReason> Reasons);

public sealed class EventRuleEngine
{
    private readonly IReadOnlyList<IEventRule> _rules;

    public EventRuleEngine(IEnumerable<IEventRule> rules) => _rules = rules.ToList();

    public EventRuleEvaluation Evaluate(WeatherForecast forecast, HolidayInfo holiday)
    {
        var reasons = _rules.Select(rule => rule.Evaluate(forecast, holiday)).ToList();
        var verdict = reasons.Any(reason => reason.Severity == RuleSeverity.Blocking && reason.Triggered)
            ? "NO_GO"
            : "GO";
        return new EventRuleEvaluation(verdict, reasons);
    }
}
