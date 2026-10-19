namespace Skjermvette.Agent.Service;

/// <summary>What pairing gave this computer: the server it belongs to and its device token.</summary>
public sealed record DeviceIdentity(Uri Server, Guid DeviceId, Guid ChildId, string Token);
