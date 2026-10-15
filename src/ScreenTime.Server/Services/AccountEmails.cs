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
        Send(user.Email!, "confirmEmail", new() { ["link"] = Link("confirm-email", user, token) });
    }

    public async Task SendPasswordResetAsync(ParentUser user)
    {
        Send(user.Email!, "resetPassword", new() { ["link"] = await ResetLinkAsync(user) });
    }

    /// <summary>Tells an address that already has an account that someone tried to create another one with it.</summary>
    public void SendRegisterAttempt(ParentUser user)
    {
        Send(user.Email!, "registerAttempt", new()
        {
            ["signInLink"] = new Uri(settings.PublicUrl, "login").ToString(),
            ["forgotLink"] = new Uri(settings.PublicUrl, "forgot-password").ToString(),
        });
    }

    /// <summary>
    /// Sends a confirmation link to the new address, and tells the current address about the request,
    /// with a link to reset the password if it wasn't them. A reset cancels the change, since it changes
    /// the security stamp the confirmation link depends on.
    /// </summary>
    public async Task SendEmailChangeAsync(ParentUser user, string newEmail)
    {
        var token = await users.GenerateChangeEmailTokenAsync(user, newEmail);
        Send(newEmail, "changeEmail", new() { ["link"] = Link("confirm-email-change", user, token, ("email", newEmail)) });
        Send(user.Email!, "emailChangeRequested", new() { ["newEmail"] = newEmail, ["link"] = await ResetLinkAsync(user) });
    }

    /// <summary>Tells the parent their password changed, with a link to reset it if it wasn't them.</summary>
    public async Task SendPasswordChangedAsync(ParentUser user)
    {
        Send(user.Email!, "passwordChanged", new()
        {
            ["link"] = await ResetLinkAsync(user),
            ["forgotLink"] = new Uri(settings.PublicUrl, "forgot-password").ToString(),
        });
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

    private async Task<string> ResetLinkAsync(ParentUser user) =>
        Link("reset-password", user, await users.GeneratePasswordResetTokenAsync(user));

    private string Link(string page, ParentUser user, string token, params (string Name, string Value)[] extra)
    {
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var query = string.Concat(extra.Select(p => $"&{p.Name}={Uri.EscapeDataString(p.Value)}"));
        return new Uri(settings.PublicUrl, $"{page}?userId={user.Id}{query}&token={encoded}").ToString();
    }

    private void Send(string to, string template, Dictionary<string, string> values)
    {
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
