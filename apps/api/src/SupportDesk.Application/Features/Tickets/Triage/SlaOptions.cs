namespace SupportDesk.Application.Features.Tickets.Triage;

public sealed class SlaOptions
{
    public const string SectionName = "Sla";

    public Dictionary<string, int> BaseWindowsHours { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Critical"] = 4,
        ["High"] = 8,
        ["Medium"] = 24,
        ["Low"] = 72
    };

    public decimal PremiumMultiplier { get; set; } = 0.5m;
    public int MinimumWindowHours { get; set; } = 1;
    public int AtRiskThresholdPercent { get; set; } = 25;
}