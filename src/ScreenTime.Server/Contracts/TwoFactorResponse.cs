namespace ScreenTime.Server.Contracts;

/// <summary>The parent's second step: the authenticator app when <see cref="AppEnabled"/>, otherwise an emailed code.</summary>
public sealed record TwoFactorResponse(bool AppEnabled, int RecoveryCodesLeft);
