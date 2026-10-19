namespace Skjermvette.Server.Contracts;

/// <summary><see cref="Code"/> is a code from the authenticator app, a recovery code, or an emailed code.</summary>
public sealed record DeleteAccountRequest(string CurrentPassword, string Code);
