namespace Skjermvette.Server.Contracts;

public sealed record ConfirmEmailChangeRequest(Guid UserId, string Email, string Token);
