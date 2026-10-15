namespace ScreenTime.Server.Contracts;

public sealed record LoginCodeRequest(TwoFactorMethodEnum Method, string Code, bool RememberBrowser);
