using Microsoft.Extensions.Configuration;
using ScreenTime.Server.Email;

namespace ScreenTime.Server.Tests;

public class EmailSettingsTests
{
    private static readonly string ContentRoot = Path.GetTempPath();

    private static EmailSettings Read(Dictionary<string, string?> values) =>
        EmailSettings.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), ContentRoot);

    private static Dictionary<string, string?> Valid() => new()
    {
        ["App:PublicUrl"] = "https://example.com/screentime",
        ["Email:From"] = "Screen Time <screentime@example.com>",
        ["Email:Smtp:Host"] = "smtp.example.com",
    };

    [Fact]
    public void Smtp_DefaultsToPort587_AndLinksResolveUnderThePublicUrl()
    {
        var settings = Read(Valid());

        Assert.Equal(new SmtpSettings("smtp.example.com", 587, null, null), settings.Smtp);
        Assert.Equal("https://example.com/screentime/reset-password", new Uri(settings.PublicUrl, "reset-password").ToString());
    }

    [Theory]
    [InlineData("App:PublicUrl", null)]
    [InlineData("App:PublicUrl", "screentime.example.com")]
    [InlineData("App:PublicUrl", "ftp://example.com")]
    [InlineData("Email:From", null)]
    [InlineData("Email:From", "not an address")]
    [InlineData("Email:Smtp:Host", null)]
    public void MissingOrInvalidSettings_FailAtStartup(string key, string? value)
    {
        var values = Valid();
        values[key] = value;

        var error = Assert.Throws<InvalidOperationException>(() => Read(values));

        Assert.Contains(key.Split(':')[0], error.Message);
    }

    [Fact]
    public void WithoutSmtp_APickupDirectoryWritesEmailsToFiles()
    {
        var values = Valid();
        values["Email:Smtp:Host"] = null;
        values["Email:PickupDirectory"] = "emails";

        var settings = Read(values);

        Assert.Null(settings.Smtp);
        Assert.Equal(Path.Combine(ContentRoot, "emails"), settings.PickupDirectory);
    }
}
