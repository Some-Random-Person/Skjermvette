namespace Skjermvette.Server.Contracts;

public sealed record ChangeEmailRequest(string NewEmail, string CurrentPassword);
