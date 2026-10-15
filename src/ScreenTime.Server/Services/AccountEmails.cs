using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using MimeKit;
using ScreenTime.Server.Data.Entities;
using ScreenTime.Server.Email;

namespace ScreenTime.Server.Services;

/// <summary>Emails about a parent's account, each with a link to a dashboard page that carries an Identity token.</summary>
public class AccountEmails(UserManager<ParentUser> users, EmailSettings settings, EmailOutbox outbox)
{
    public async Task SendConfirmationAsync(ParentUser user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        Send(user.Email!, "confirmEmail", Link("confirm-email", user, token));
    }

    public async Task SendPasswordResetAsync(ParentUser user)
    {
        var token = await users.GeneratePasswordResetTokenAsync(user);
        Send(user.Email!, "resetPassword", Link("reset-password", user, token));
    }

    /// <summary>
    /// Reads a token back from a link. Tokens are Base64Url-encoded so they survive in a URL;
    /// null when it isn't valid Base64Url, which Identity then treats like any other bad token.
    /// </summary>
    public static string? DecodeToken(string? token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token ?? ""));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private string Link(string page, ParentUser user, string token)
    {
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return new Uri(settings.PublicUrl, $"{page}?userId={user.Id}&token={encoded}").ToString();
    }

    private void Send(string to, string template, string link)
    {
        var values = new Dictionary<string, string> { ["link"] = link };
        var message = new MimeMessage
        {
            Subject = EmailTemplates.Text($"{template}.subject", values),
            Body = new TextPart("plain") { Text = EmailTemplates.Text($"{template}.body", values) },
        };
        message.From.Add(settings.From);
        message.To.Add(new MailboxAddress(null, to));
        outbox.Enqueue(message);
    }
}
