namespace Skjermvette.Server.Contracts;

/// <summary>A null <see cref="OfflineAllowanceOverrideMinutes"/> means the device uses the child's setting.</summary>
public sealed record DeviceUpdateRequest(string Name, int? OfflineAllowanceOverrideMinutes);
