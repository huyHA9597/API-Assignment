using EventGoNoGoAdvisor.Api.Models;
using EventGoNoGoAdvisor.Api.Rules;

namespace EventGoNoGoAdvisor.Tests.Rules;

public class EventRuleEngineTests
{
    private sealed class FakeRule : IEventRule
    {
        private readonly RuleReason _result;
        public FakeRule(RuleReason result) => _result = result;
        public RuleReason Evaluate(WeatherForecast forecast, HolidayInfo holiday) => _result;
    }

    private static readonly WeatherForecast AnyForecast = new(10, 10);
    private static readonly HolidayInfo AnyHoliday = new(false, null);

    [Fact]
    public void Evaluate_WhenNoRuleTriggers_ReturnsGoWithAllReasons()
    {
        var rules = new IEventRule[]
        {
            new FakeRule(new RuleReason("RuleA", RuleSeverity.Blocking, Triggered: false, Message: "clear")),
            new FakeRule(new RuleReason("RuleB", RuleSeverity.Advisory, Triggered: false, Message: "clear")),
        };
        var engine = new EventRuleEngine(rules);

        var result = engine.Evaluate(AnyForecast, AnyHoliday);

        Assert.Equal("GO", result.Verdict);
        Assert.Equal(2, result.Reasons.Count);
    }

    [Fact]
    public void Evaluate_WhenBlockingRuleTriggers_ReturnsNoGoRegardlessOfOtherRules()
    {
        var rules = new IEventRule[]
        {
            new FakeRule(new RuleReason("RuleA", RuleSeverity.Blocking, Triggered: true, Message: "blocked")),
            new FakeRule(new RuleReason("RuleB", RuleSeverity.Advisory, Triggered: false, Message: "clear")),
        };
        var engine = new EventRuleEngine(rules);

        var result = engine.Evaluate(AnyForecast, AnyHoliday);

        Assert.Equal("NO_GO", result.Verdict);
    }

    [Fact]
    public void Evaluate_WhenOnlyAdvisoryRuleTriggers_ReturnsGoWithReasonPresent()
    {
        var rules = new IEventRule[]
        {
            new FakeRule(new RuleReason("RuleA", RuleSeverity.Blocking, Triggered: false, Message: "clear")),
            new FakeRule(new RuleReason("RuleB", RuleSeverity.Advisory, Triggered: true, Message: "holiday")),
        };
        var engine = new EventRuleEngine(rules);

        var result = engine.Evaluate(AnyForecast, AnyHoliday);

        Assert.Equal("GO", result.Verdict);
        Assert.Contains(result.Reasons, r => r.Rule == "RuleB" && r.Triggered);
    }

    [Fact]
    public void Evaluate_ReasonsIncludeOneEntryPerRuleInOrder()
    {
        var rules = new IEventRule[]
        {
            new FakeRule(new RuleReason("RuleA", RuleSeverity.Blocking, Triggered: false, Message: "a")),
            new FakeRule(new RuleReason("RuleB", RuleSeverity.Advisory, Triggered: false, Message: "b")),
            new FakeRule(new RuleReason("RuleC", RuleSeverity.Blocking, Triggered: false, Message: "c")),
        };
        var engine = new EventRuleEngine(rules);

        var result = engine.Evaluate(AnyForecast, AnyHoliday);

        Assert.Equal(new[] { "RuleA", "RuleB", "RuleC" }, result.Reasons.Select(r => r.Rule));
    }
}
