namespace ScreenTime.Server.Contracts;

/// <summary><see cref="Code"/> is a code from the app, or a recovery code.</summary>
public sealed record DisableAuthenticatorRequest(string CurrentPassword, string Code);
