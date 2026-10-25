namespace EventGoNoGoAdvisor.Api.Models;

public enum RuleSeverity
{
    Blocking,
    Advisory
}

public sealed record RuleReason(string Rule, RuleSeverity Severity, bool Triggered, string Message);
