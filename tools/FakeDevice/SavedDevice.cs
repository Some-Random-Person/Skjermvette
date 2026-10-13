namespace FakeDevice;

/// <summary>A paired fake device, stored locally so later commands can use it by name.</summary>
public sealed record SavedDevice(string Name, Uri Server, Guid DeviceId, Guid ChildId, string Token);
