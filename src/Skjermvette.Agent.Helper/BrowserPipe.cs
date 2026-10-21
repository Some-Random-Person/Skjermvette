using System.Text.Json;
using System.Text.RegularExpressions;
using Skjermvette.Core;

namespace Skjermvette.Agent.Helper;

/// <summary>
/// How the browser extension's reports reach the Helper: the browser starts the native messaging host
/// (<see cref="BrowserHost"/>), which passes each report on over this pipe as one message of JSON (<see cref="BrowserReport"/>).
/// The Helper answers with the websites to block (<see cref="WebsiteBlocks"/>), which the host passes on to the extension.
/// </summary>
public static partial class BrowserPipe
{
    /// <summary>The name the native messaging host is registered under in Chrome and Edge.</summary>
    public const string HostName = "no.skjermvette.agent";

    /// <summary>Longer messages are refused, so a bad client can't make the Helper buffer without limit.</summary>
    public const int MaxMessageBytes = 8192;

    public const int MaxAudible = 32;

    /// <summary>The most a native messaging host may send the extension in one message.</summary>
    public const int MaxWebsitesBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Pipe names are shared by every session, so each session's Helper has its own.</summary>
    public static string Name(int sessionId) => $"Skjermvette.Agent.Browser.{sessionId}";

    public static byte[] Encode(WebsiteBlocks websites) => JsonSerializer.SerializeToUtf8Bytes(websites, Json);

    /// <summary>The report in <paramref name="message"/>, or null if it isn't a valid one.</summary>
    public static BrowserReport? Decode(ReadOnlySpan<byte> message)
    {
        BrowserReport? report;
        try
        {
            report = JsonSerializer.Deserialize<BrowserReport>(message, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        var valid = report is { Browser: not null, Audible.Length: <= MaxAudible } && Program().IsMatch(report.Browser)
            && (report.Active is null || Domain().IsMatch(report.Active)) && report.Audible.All(d => d is not null && Domain().IsMatch(d));
        return valid ? report : null;
    }

    [GeneratedRegex(@"^[a-z0-9_.-]{1,60}\.exe$")]
    private static partial Regex Program();

    /// <summary>A lowercase host name, as the extension takes it from the tab's URL (the extension checks the same).</summary>
    [GeneratedRegex("^[a-z0-9.-]{1,253}$")]
    private static partial Regex Domain();
}
