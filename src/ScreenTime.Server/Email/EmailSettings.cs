using MimeKit;

namespace ScreenTime.Server.Email;

/// <summary>
/// Where emails go: through <see cref="Smtp"/> when it's set, otherwise into <see cref="PickupDirectory"/>
/// as .eml files (development and tests). Links in emails start with <see cref="PublicUrl"/>, since the
/// request URL isn't the public one behind the Cloudflare Tunnel.
/// </summary>
public sealed record EmailSettings(Uri PublicUrl, MailboxAddress From, SmtpSettings? Smtp, string? PickupDirectory)
{
    /// <summary>Reads the settings, failing fast at startup when something is missing.</summary>
    public static EmailSettings FromConfiguration(IConfiguration config, string contentRoot)
    {
        var publicUrl = config["App:PublicUrl"];
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Missing or invalid configuration: App__PublicUrl (e.g. https://screentime.example.com).");
        // A trailing slash so links are resolved under the URL's path, not next to it.
        if (!url.AbsolutePath.EndsWith('/'))
            url = new Uri(url + "/");

        if (!MailboxAddress.TryParse(config["Email:From"], out var from))
            throw new InvalidOperationException("Missing or invalid configuration: Email__From (e.g. Screen Time <screentime@example.com>).");

        var host = config["Email:Smtp:Host"];
        var smtp = string.IsNullOrWhiteSpace(host)
            ? null
            : new SmtpSettings(host, config.GetValue("Email:Smtp:Port", 587), config["Email:Smtp:User"], config["Email:Smtp:Password"]);

        var pickup = config["Email:PickupDirectory"];
        if (smtp is null && string.IsNullOrWhiteSpace(pickup))
            throw new InvalidOperationException("Missing configuration: Email__Smtp__Host (or Email__PickupDirectory to write emails to files instead).");

        return new EmailSettings(url, from, smtp, string.IsNullOrWhiteSpace(pickup) ? null : Path.GetFullPath(pickup, contentRoot));
    }
}
