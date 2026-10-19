using System.ComponentModel;

namespace Skjermvette.Server.Data.Entities;

/// <summary>A child's rule for one app: either a daily limit or exempt. One rule per app and child.</summary>
public class AppRule
{
    public Guid Id { get; set; }
    public Guid ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public Guid AppDefinitionId { get; set; }
    public AppDefinition AppDefinition { get; set; } = null!;
    public AppRuleKindEnum Kind { get; set; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="AppRuleKindEnum.Limited"/>.</summary>
    public TimeSpan? Limit { get; set; }
}

public enum AppRuleKindEnum
{
    [Description("Daily limit; counts toward the app's limit and the daily total")]
    Limited = 1,

    [Description("Never blocked and never counted toward the daily total")]
    Exempt = 2,
}
