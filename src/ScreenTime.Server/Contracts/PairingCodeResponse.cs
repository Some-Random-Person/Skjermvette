namespace ScreenTime.Server.Contracts;

public sealed record PairingCodeResponse(string Code, DateTime ExpiresAtUtc);
