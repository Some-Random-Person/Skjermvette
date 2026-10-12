using Microsoft.EntityFrameworkCore;
using ScreenTime.Core.Models;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Data;

/// <summary>Built-in apps available to every family. Ids are fixed so migrations stay stable.</summary>
public static class SeedCatalog
{
    public static readonly Guid YouTube = Guid.Parse("5e1f0001-0000-4000-8000-000000000001");
    public static readonly Guid Teams = Guid.Parse("5e1f0001-0000-4000-8000-000000000002");
    public static readonly Guid TikTok = Guid.Parse("5e1f0001-0000-4000-8000-000000000003");
    public static readonly Guid Roblox = Guid.Parse("5e1f0001-0000-4000-8000-000000000004");
    public static readonly Guid Minecraft = Guid.Parse("5e1f0001-0000-4000-8000-000000000005");
    public static readonly Guid Discord = Guid.Parse("5e1f0001-0000-4000-8000-000000000006");
    public static readonly Guid Netflix = Guid.Parse("5e1f0001-0000-4000-8000-000000000007");
    public static readonly Guid Fortnite = Guid.Parse("5e1f0001-0000-4000-8000-000000000008");

    private static Guid Id(int n) => Guid.Parse($"5e1f0001-0000-4000-8000-{n:x12}");

    private static readonly (Guid App, string Name, (MatcherKindEnum Kind, string Value)[] Ids)[] Apps =
    [
        (YouTube, "YouTube",
        [
            (MatcherKindEnum.Domain, "youtube.com"), (MatcherKindEnum.Domain, "youtu.be"),
            (MatcherKindEnum.Domain, "youtube-nocookie.com"),
            (MatcherKindEnum.Package, "com.google.android.youtube"), (MatcherKindEnum.Bundle, "com.google.ios.youtube"),
        ]),
        (Teams, "Microsoft Teams",
        [
            (MatcherKindEnum.Process, "ms-teams.exe"), (MatcherKindEnum.Process, "teams.exe"),
            (MatcherKindEnum.Domain, "teams.microsoft.com"), (MatcherKindEnum.Domain, "teams.live.com"),
            (MatcherKindEnum.Package, "com.microsoft.teams"), (MatcherKindEnum.Bundle, "com.microsoft.skype.teams"),
        ]),
        (TikTok, "TikTok",
        [
            (MatcherKindEnum.Domain, "tiktok.com"),
            (MatcherKindEnum.Package, "com.zhiliaoapp.musically"), (MatcherKindEnum.Bundle, "com.zhiliaoapp.musically"),
        ]),
        (Roblox, "Roblox",
        [
            (MatcherKindEnum.Process, "robloxplayerbeta.exe"), (MatcherKindEnum.Domain, "roblox.com"),
            (MatcherKindEnum.Package, "com.roblox.client"), (MatcherKindEnum.Bundle, "com.roblox.robloxmobile"),
        ]),
        (Minecraft, "Minecraft",
        [
            (MatcherKindEnum.Process, "minecraft.windows.exe"),
            (MatcherKindEnum.Package, "com.mojang.minecraftpe"), (MatcherKindEnum.Bundle, "com.mojang.minecraftpe"),
        ]),
        (Discord, "Discord",
        [
            (MatcherKindEnum.Process, "discord.exe"), (MatcherKindEnum.Domain, "discord.com"),
            (MatcherKindEnum.Package, "com.discord"), (MatcherKindEnum.Bundle, "com.hammerandchisel.discord"),
        ]),
        (Netflix, "Netflix",
        [
            (MatcherKindEnum.Domain, "netflix.com"),
            (MatcherKindEnum.Package, "com.netflix.mediaclient"), (MatcherKindEnum.Bundle, "com.netflix.Netflix"),
        ]),

        // Added in the second catalog release. Ids continue the sequence; never reuse or renumber them.
        (Fortnite, "Fortnite",
        [
            (MatcherKindEnum.Process, "fortniteclient-win64-shipping.exe"), (MatcherKindEnum.Domain, "fortnite.com"),
            (MatcherKindEnum.Package, "com.epicgames.fortnite"),
        ]),
        (Id(9), "Instagram",
        [
            (MatcherKindEnum.Domain, "instagram.com"),
            (MatcherKindEnum.Package, "com.instagram.android"), (MatcherKindEnum.Bundle, "com.burbn.instagram"),
        ]),
        (Id(10), "Snapchat",
        [
            (MatcherKindEnum.Domain, "snapchat.com"),
            (MatcherKindEnum.Package, "com.snapchat.android"), (MatcherKindEnum.Bundle, "com.toyopagroup.picaboo"),
        ]),
        (Id(11), "Facebook",
        [
            (MatcherKindEnum.Domain, "facebook.com"),
            (MatcherKindEnum.Package, "com.facebook.katana"), (MatcherKindEnum.Bundle, "com.facebook.Facebook"),
        ]),
        (Id(12), "WhatsApp",
        [
            (MatcherKindEnum.Process, "whatsapp.exe"), (MatcherKindEnum.Domain, "whatsapp.com"),
            (MatcherKindEnum.Package, "com.whatsapp"), (MatcherKindEnum.Bundle, "net.whatsapp.WhatsApp"),
        ]),
        (Id(13), "Telegram",
        [
            (MatcherKindEnum.Process, "telegram.exe"), (MatcherKindEnum.Domain, "telegram.org"),
            (MatcherKindEnum.Package, "org.telegram.messenger"), (MatcherKindEnum.Bundle, "ph.telegra.Telegraph"),
        ]),
        (Id(14), "Reddit",
        [
            (MatcherKindEnum.Domain, "reddit.com"),
            (MatcherKindEnum.Package, "com.reddit.frontpage"), (MatcherKindEnum.Bundle, "com.reddit.Reddit"),
        ]),
        (Id(15), "X (Twitter)",
        [
            (MatcherKindEnum.Domain, "x.com"), (MatcherKindEnum.Domain, "twitter.com"),
            (MatcherKindEnum.Package, "com.twitter.android"), (MatcherKindEnum.Bundle, "com.atebits.Tweetie2"),
        ]),
        (Id(16), "Pinterest",
        [
            (MatcherKindEnum.Domain, "pinterest.com"),
            (MatcherKindEnum.Package, "com.pinterest"), (MatcherKindEnum.Bundle, "pinterest"),
        ]),
        (Id(17), "Twitch",
        [
            (MatcherKindEnum.Domain, "twitch.tv"),
            (MatcherKindEnum.Package, "tv.twitch.android.app"), (MatcherKindEnum.Bundle, "tv.twitch"),
        ]),
        (Id(18), "YouTube Kids",
        [
            (MatcherKindEnum.Domain, "youtubekids.com"),
            (MatcherKindEnum.Package, "com.google.android.apps.youtube.kids"), (MatcherKindEnum.Bundle, "com.google.ios.youtubekids"),
        ]),
        (Id(19), "Disney+",
        [
            (MatcherKindEnum.Domain, "disneyplus.com"),
            (MatcherKindEnum.Package, "com.disney.disneyplus"), (MatcherKindEnum.Bundle, "com.disney.disneyplus"),
        ]),
        (Id(20), "Prime Video",
        [
            (MatcherKindEnum.Domain, "primevideo.com"),
            (MatcherKindEnum.Package, "com.amazon.avod.thirdpartyclient"), (MatcherKindEnum.Bundle, "com.amazon.aiv.AIVApp"),
        ]),
        (Id(21), "Spotify",
        [
            (MatcherKindEnum.Process, "spotify.exe"), (MatcherKindEnum.Domain, "spotify.com"),
            (MatcherKindEnum.Package, "com.spotify.music"), (MatcherKindEnum.Bundle, "com.spotify.client"),
        ]),
        (Id(22), "Steam",
        [
            (MatcherKindEnum.Process, "steam.exe"), (MatcherKindEnum.Process, "steamwebhelper.exe"),
            (MatcherKindEnum.Domain, "steampowered.com"), (MatcherKindEnum.Domain, "steamcommunity.com"),
            (MatcherKindEnum.Package, "com.valvesoftware.android.steam.community"), (MatcherKindEnum.Bundle, "com.valvesoftware.Steam"),
        ]),
        (Id(23), "Among Us",
        [
            (MatcherKindEnum.Process, "among us.exe"),
            (MatcherKindEnum.Package, "com.innersloth.spacemafia"), (MatcherKindEnum.Bundle, "com.innersloth.amongus"),
        ]),
        (Id(24), "Brawl Stars",
        [
            (MatcherKindEnum.Package, "com.supercell.brawlstars"), (MatcherKindEnum.Bundle, "com.supercell.laser"),
        ]),
        (Id(25), "Clash Royale",
        [
            (MatcherKindEnum.Package, "com.supercell.clashroyale"), (MatcherKindEnum.Bundle, "com.supercell.scroll"),
        ]),
        (Id(26), "Genshin Impact",
        [
            (MatcherKindEnum.Process, "genshinimpact.exe"),
            (MatcherKindEnum.Package, "com.miHoYo.GenshinImpact"), (MatcherKindEnum.Bundle, "com.miHoYo.GenshinImpact"),
        ]),
        (Id(27), "Valorant", [(MatcherKindEnum.Process, "valorant-win64-shipping.exe")]),
        (Id(28), "League of Legends", [(MatcherKindEnum.Process, "league of legends.exe")]),
        (Id(29), "Rocket League", [(MatcherKindEnum.Process, "rocketleague.exe")]),
        (Id(30), "Zoom",
        [
            (MatcherKindEnum.Process, "zoom.exe"), (MatcherKindEnum.Domain, "zoom.us"),
            (MatcherKindEnum.Package, "us.zoom.videomeetings"), (MatcherKindEnum.Bundle, "us.zoom.videomeetings"),
        ]),
        (Id(31), "Google Classroom",
        [
            (MatcherKindEnum.Domain, "classroom.google.com"),
            (MatcherKindEnum.Package, "com.google.android.apps.classroom"), (MatcherKindEnum.Bundle, "com.google.Classroom"),
        ]),
        (Id(32), "Duolingo",
        [
            (MatcherKindEnum.Domain, "duolingo.com"),
            (MatcherKindEnum.Package, "com.duolingo"), (MatcherKindEnum.Bundle, "com.duolingo.DuolingoMobile"),
        ]),
        (Id(33), "Khan Academy",
        [
            (MatcherKindEnum.Domain, "khanacademy.org"),
            (MatcherKindEnum.Package, "org.khanacademy.android"), (MatcherKindEnum.Bundle, "org.khanacademy.Khan-Academy"),
        ]),
    ];

    public static void Apply(ModelBuilder builder)
    {
        builder.Entity<AppDefinition>().HasData(Apps.Select(a => new { Id = a.App, a.Name, FamilyId = (Guid?)null }));

        builder.Entity<AppIdentifier>().HasData(Apps.SelectMany(a => a.Ids.Select((id, i) => new
        {
            // Deterministic ids: the app id (which differs in its last byte) with the identifier's index in the byte before.
            Id = new Guid([.. a.App.ToByteArray()[..14], (byte)(i + 1), a.App.ToByteArray()[15]]),
            AppDefinitionId = a.App,
            FamilyId = (Guid?)null,
            id.Kind,
            id.Value,
        })));
    }
}
