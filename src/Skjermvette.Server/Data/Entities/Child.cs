namespace Skjermvette.Server.Data.Entities;

public class Child
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Family Family { get; set; } = null!;
    public required string Name { get; set; }

    /// <summary>IANA time zone, e.g. "Europe/Oslo".</summary>
    public required string TimeZoneId { get; set; }

    /// <summary>Local hour (0–23) when the day's budget resets.</summary>
    public int ResetHour { get; set; }

    public TimeSpan DailyTotal { get; set; }
    public TimeSpan OfflineAllowance { get; set; }

    public List<AppRule> AppRules { get; } = [];
    public List<Device> Devices { get; } = [];
}
