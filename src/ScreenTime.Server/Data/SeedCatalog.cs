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
