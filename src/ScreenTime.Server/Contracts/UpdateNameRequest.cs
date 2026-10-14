namespace ScreenTime.Server.Contracts;

/// <summary>An empty or null name removes it, so the email is shown instead.</summary>
public sealed record UpdateNameRequest(string? Name);
