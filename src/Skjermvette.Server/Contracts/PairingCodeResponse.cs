namespace Skjermvette.Server.Contracts;

public sealed record PairingCodeResponse(string Code, DateTime ExpiresAtUtc);
