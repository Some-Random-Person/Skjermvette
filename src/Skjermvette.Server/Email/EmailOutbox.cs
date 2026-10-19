using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Skjermvette.Server.Email;

/// <summary>
/// Sends emails in the background, one at a time. Requests don't wait for the SMTP server, and how long a
/// request takes doesn't reveal whether an email was sent (e.g. whether an address has an account).
/// Emails still queued when the server stops are lost.
/// </summary>
public class EmailOutbox(EmailSettings settings, TimeProvider time, ILogger<EmailOutbox> logger) : BackgroundService
{
    private readonly Channel<MimeMessage> _queue = Channel.CreateUnbounded<MimeMessage>();

    public void Enqueue(MimeMessage message) => _queue.Writer.TryWrite(message);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await (settings.Smtp is { } smtp ? SendAsync(smtp, message, stoppingToken) : WriteAsync(message, stoppingToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Couldn't send the email \"{Subject}\".", message.Subject);
            }
        }
    }

    private static async Task SendAsync(SmtpSettings smtp, MimeMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient();
        // Never fall back to an unencrypted connection.
        var security = smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(smtp.Host, smtp.Port, security, ct);
        if (!string.IsNullOrEmpty(smtp.User))
            await client.AuthenticateAsync(smtp.User, smtp.Password ?? "", ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    private async Task WriteAsync(MimeMessage message, CancellationToken ct)
    {
        Directory.CreateDirectory(settings.PickupDirectory!);
        var path = Path.Combine(settings.PickupDirectory!, $"{time.GetUtcNow():yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");
        // Written under another name first, so nothing reads a half-written file.
        await message.WriteToAsync(path + ".tmp", ct);
        File.Move(path + ".tmp", path);
        logger.LogInformation("Email \"{Subject}\" written to {Path}", message.Subject, path);
    }
}
