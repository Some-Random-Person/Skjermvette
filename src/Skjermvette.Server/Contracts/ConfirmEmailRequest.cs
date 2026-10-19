namespace Skjermvette.Server.Contracts;

public sealed record ConfirmEmailRequest(Guid UserId, string Token);
