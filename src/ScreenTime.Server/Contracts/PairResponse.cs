namespace ScreenTime.Server.Contracts;

/// <summary><see cref="Token"/> is shown once; the device must store it securely.</summary>
public sealed record PairResponse(Guid DeviceId, Guid ChildId, string Token);
