namespace EventGoNoGoAdvisor.Api.Options;

public sealed class EventRuleThresholds
{
    public double MaxPrecipitationProbabilityPercent { get; set; } = 70;

    public double MaxWindSpeedKmh { get; set; } = 40;
}
