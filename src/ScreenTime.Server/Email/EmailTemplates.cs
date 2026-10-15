using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScreenTime.Server.Email;

/// <summary>
/// Email texts, kept in <c>emails.en.json</c> (embedded in the build) so they can be translated, with
/// <c>{name}</c> placeholders like the dashboard's strings.
/// </summary>
public static partial class EmailTemplates
{
    private static readonly Dictionary<string, string> Strings = Load();

    public static string Text(string key, IReadOnlyDictionary<string, string> values) =>
        Placeholder().Replace(Strings[key], m => values.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);

    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(EmailTemplates).Assembly.GetManifestResourceStream("emails.en.json")
            ?? throw new InvalidOperationException("The embedded resource emails.en.json is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}
