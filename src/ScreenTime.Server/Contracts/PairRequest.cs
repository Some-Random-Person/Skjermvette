using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Contracts;

public sealed record PairRequest(string Code, string DeviceName, DevicePlatformEnum Platform);
