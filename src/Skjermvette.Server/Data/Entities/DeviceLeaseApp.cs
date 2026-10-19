namespace Skjermvette.Server.Data.Entities;

public class DeviceLeaseApp
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    public DeviceLease Lease { get; set; } = null!;
    public Guid AppDefinitionId { get; set; }
    public TimeSpan Amount { get; set; }
}
