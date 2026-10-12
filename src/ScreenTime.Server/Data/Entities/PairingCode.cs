namespace ScreenTime.Server.Data.Entities;

/// <summary>A one-time 6-digit code a parent creates so a device can join a child.</summary>
public class PairingCode
{
    public Guid Id { get; set; }
    public Guid ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public required string Code { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
