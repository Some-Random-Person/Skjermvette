namespace Skjermvette.Server.Contracts;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
