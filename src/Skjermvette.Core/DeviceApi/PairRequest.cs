namespace Skjermvette.Core.DeviceApi;

public sealed record PairRequest(string Code, string DeviceName, DevicePlatformEnum Platform);
