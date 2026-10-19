namespace Skjermvette.Server.Contracts;

public sealed record LoginCodeRequest(TwoFactorMethodEnum Method, string Code, bool RememberBrowser);
