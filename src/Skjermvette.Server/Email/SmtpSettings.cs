namespace Skjermvette.Server.Email;

/// <summary>The SMTP server emails are sent through. Port 465 uses TLS from the start; any other port requires STARTTLS.</summary>
public sealed record SmtpSettings(string Host, int Port, string? User, string? Password);
