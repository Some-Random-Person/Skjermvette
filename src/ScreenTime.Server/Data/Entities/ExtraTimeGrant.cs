namespace ScreenTime.Server.Data.Entities;

/// <summary>Extra time a parent added to one day's total.</summary>
public class ExtraTimeGrant
{
    public Guid Id { get; set; }
    public Guid ChildId { get; set; }
    public Child Child { get; set; } = null!;

    /// <summary>Start of the screen-time day the grant applies to.</summary>
    public DateTime DayStartUtc { get; set; }

    public TimeSpan Amount { get; set; }
    public DateTime GrantedAtUtc { get; set; }
}
