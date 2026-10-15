namespace ScreenTime.Server.Contracts;

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
