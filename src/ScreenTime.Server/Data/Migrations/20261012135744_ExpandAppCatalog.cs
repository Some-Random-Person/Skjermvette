using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ScreenTime.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandAppCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AppDefinitions",
                columns: new[] { "Id", "FamilyId", "Name" },
                values: new object[,]
                {
                    { new Guid("5e1f0001-0000-4000-8000-000000000008"), null, "Fortnite" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000009"), null, "Instagram" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000000a"), null, "Snapchat" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000000b"), null, "Facebook" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000000c"), null, "WhatsApp" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000000d"), null, "Telegram" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000000e"), null, "Reddit" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000000f"), null, "X (Twitter)" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000010"), null, "Pinterest" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000011"), null, "Twitch" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000012"), null, "YouTube Kids" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000013"), null, "Disney+" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000014"), null, "Prime Video" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000015"), null, "Spotify" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000016"), null, "Steam" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000017"), null, "Among Us" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000018"), null, "Brawl Stars" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000019"), null, "Clash Royale" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000001a"), null, "Genshin Impact" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000001b"), null, "Valorant" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000001c"), null, "League of Legends" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000001d"), null, "Rocket League" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000001e"), null, "Zoom" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000001f"), null, "Google Classroom" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000020"), null, "Duolingo" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000021"), null, "Khan Academy" }
                });

            migrationBuilder.InsertData(
                table: "AppIdentifiers",
                columns: new[] { "Id", "AppDefinitionId", "FamilyId", "Kind", "Value" },
                values: new object[,]
                {
                    { new Guid("5e1f0001-0000-4000-8000-000000000108"), new Guid("5e1f0001-0000-4000-8000-000000000008"), null, 1, "fortniteclient-win64-shipping.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000109"), new Guid("5e1f0001-0000-4000-8000-000000000009"), null, 4, "instagram.com" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000010a"), new Guid("5e1f0001-0000-4000-8000-00000000000a"), null, 4, "snapchat.com" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000010b"), new Guid("5e1f0001-0000-4000-8000-00000000000b"), null, 4, "facebook.com" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000010c"), new Guid("5e1f0001-0000-4000-8000-00000000000c"), null, 1, "whatsapp.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000010d"), new Guid("5e1f0001-0000-4000-8000-00000000000d"), null, 1, "telegram.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000010e"), new Guid("5e1f0001-0000-4000-8000-00000000000e"), null, 4, "reddit.com" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000010f"), new Guid("5e1f0001-0000-4000-8000-00000000000f"), null, 4, "x.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000110"), new Guid("5e1f0001-0000-4000-8000-000000000010"), null, 4, "pinterest.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000111"), new Guid("5e1f0001-0000-4000-8000-000000000011"), null, 4, "twitch.tv" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000112"), new Guid("5e1f0001-0000-4000-8000-000000000012"), null, 4, "youtubekids.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000113"), new Guid("5e1f0001-0000-4000-8000-000000000013"), null, 4, "disneyplus.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000114"), new Guid("5e1f0001-0000-4000-8000-000000000014"), null, 4, "primevideo.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000115"), new Guid("5e1f0001-0000-4000-8000-000000000015"), null, 1, "spotify.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000116"), new Guid("5e1f0001-0000-4000-8000-000000000016"), null, 1, "steam.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000117"), new Guid("5e1f0001-0000-4000-8000-000000000017"), null, 1, "among us.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000118"), new Guid("5e1f0001-0000-4000-8000-000000000018"), null, 2, "com.supercell.brawlstars" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000119"), new Guid("5e1f0001-0000-4000-8000-000000000019"), null, 2, "com.supercell.clashroyale" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000011a"), new Guid("5e1f0001-0000-4000-8000-00000000001a"), null, 1, "genshinimpact.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000011b"), new Guid("5e1f0001-0000-4000-8000-00000000001b"), null, 1, "valorant-win64-shipping.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000011c"), new Guid("5e1f0001-0000-4000-8000-00000000001c"), null, 1, "league of legends.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000011d"), new Guid("5e1f0001-0000-4000-8000-00000000001d"), null, 1, "rocketleague.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000011e"), new Guid("5e1f0001-0000-4000-8000-00000000001e"), null, 1, "zoom.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000011f"), new Guid("5e1f0001-0000-4000-8000-00000000001f"), null, 4, "classroom.google.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000120"), new Guid("5e1f0001-0000-4000-8000-000000000020"), null, 4, "duolingo.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000121"), new Guid("5e1f0001-0000-4000-8000-000000000021"), null, 4, "khanacademy.org" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000208"), new Guid("5e1f0001-0000-4000-8000-000000000008"), null, 4, "fortnite.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000209"), new Guid("5e1f0001-0000-4000-8000-000000000009"), null, 2, "com.instagram.android" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000020a"), new Guid("5e1f0001-0000-4000-8000-00000000000a"), null, 2, "com.snapchat.android" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000020b"), new Guid("5e1f0001-0000-4000-8000-00000000000b"), null, 2, "com.facebook.katana" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000020c"), new Guid("5e1f0001-0000-4000-8000-00000000000c"), null, 4, "whatsapp.com" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000020d"), new Guid("5e1f0001-0000-4000-8000-00000000000d"), null, 4, "telegram.org" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000020e"), new Guid("5e1f0001-0000-4000-8000-00000000000e"), null, 2, "com.reddit.frontpage" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000020f"), new Guid("5e1f0001-0000-4000-8000-00000000000f"), null, 4, "twitter.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000210"), new Guid("5e1f0001-0000-4000-8000-000000000010"), null, 2, "com.pinterest" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000211"), new Guid("5e1f0001-0000-4000-8000-000000000011"), null, 2, "tv.twitch.android.app" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000212"), new Guid("5e1f0001-0000-4000-8000-000000000012"), null, 2, "com.google.android.apps.youtube.kids" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000213"), new Guid("5e1f0001-0000-4000-8000-000000000013"), null, 2, "com.disney.disneyplus" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000214"), new Guid("5e1f0001-0000-4000-8000-000000000014"), null, 2, "com.amazon.avod.thirdpartyclient" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000215"), new Guid("5e1f0001-0000-4000-8000-000000000015"), null, 4, "spotify.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000216"), new Guid("5e1f0001-0000-4000-8000-000000000016"), null, 1, "steamwebhelper.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000217"), new Guid("5e1f0001-0000-4000-8000-000000000017"), null, 2, "com.innersloth.spacemafia" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000218"), new Guid("5e1f0001-0000-4000-8000-000000000018"), null, 3, "com.supercell.laser" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000219"), new Guid("5e1f0001-0000-4000-8000-000000000019"), null, 3, "com.supercell.scroll" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000021a"), new Guid("5e1f0001-0000-4000-8000-00000000001a"), null, 2, "com.miHoYo.GenshinImpact" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000021e"), new Guid("5e1f0001-0000-4000-8000-00000000001e"), null, 4, "zoom.us" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000021f"), new Guid("5e1f0001-0000-4000-8000-00000000001f"), null, 2, "com.google.android.apps.classroom" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000220"), new Guid("5e1f0001-0000-4000-8000-000000000020"), null, 2, "com.duolingo" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000221"), new Guid("5e1f0001-0000-4000-8000-000000000021"), null, 2, "org.khanacademy.android" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000308"), new Guid("5e1f0001-0000-4000-8000-000000000008"), null, 2, "com.epicgames.fortnite" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000309"), new Guid("5e1f0001-0000-4000-8000-000000000009"), null, 3, "com.burbn.instagram" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000030a"), new Guid("5e1f0001-0000-4000-8000-00000000000a"), null, 3, "com.toyopagroup.picaboo" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000030b"), new Guid("5e1f0001-0000-4000-8000-00000000000b"), null, 3, "com.facebook.Facebook" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000030c"), new Guid("5e1f0001-0000-4000-8000-00000000000c"), null, 2, "com.whatsapp" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000030d"), new Guid("5e1f0001-0000-4000-8000-00000000000d"), null, 2, "org.telegram.messenger" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000030e"), new Guid("5e1f0001-0000-4000-8000-00000000000e"), null, 3, "com.reddit.Reddit" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000030f"), new Guid("5e1f0001-0000-4000-8000-00000000000f"), null, 2, "com.twitter.android" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000310"), new Guid("5e1f0001-0000-4000-8000-000000000010"), null, 3, "pinterest" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000311"), new Guid("5e1f0001-0000-4000-8000-000000000011"), null, 3, "tv.twitch" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000312"), new Guid("5e1f0001-0000-4000-8000-000000000012"), null, 3, "com.google.ios.youtubekids" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000313"), new Guid("5e1f0001-0000-4000-8000-000000000013"), null, 3, "com.disney.disneyplus" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000314"), new Guid("5e1f0001-0000-4000-8000-000000000014"), null, 3, "com.amazon.aiv.AIVApp" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000315"), new Guid("5e1f0001-0000-4000-8000-000000000015"), null, 2, "com.spotify.music" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000316"), new Guid("5e1f0001-0000-4000-8000-000000000016"), null, 4, "steampowered.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000317"), new Guid("5e1f0001-0000-4000-8000-000000000017"), null, 3, "com.innersloth.amongus" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000031a"), new Guid("5e1f0001-0000-4000-8000-00000000001a"), null, 3, "com.miHoYo.GenshinImpact" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000031e"), new Guid("5e1f0001-0000-4000-8000-00000000001e"), null, 2, "us.zoom.videomeetings" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000031f"), new Guid("5e1f0001-0000-4000-8000-00000000001f"), null, 3, "com.google.Classroom" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000320"), new Guid("5e1f0001-0000-4000-8000-000000000020"), null, 3, "com.duolingo.DuolingoMobile" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000321"), new Guid("5e1f0001-0000-4000-8000-000000000021"), null, 3, "org.khanacademy.Khan-Academy" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000040c"), new Guid("5e1f0001-0000-4000-8000-00000000000c"), null, 3, "net.whatsapp.WhatsApp" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000040d"), new Guid("5e1f0001-0000-4000-8000-00000000000d"), null, 3, "ph.telegra.Telegraph" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000040f"), new Guid("5e1f0001-0000-4000-8000-00000000000f"), null, 3, "com.atebits.Tweetie2" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000415"), new Guid("5e1f0001-0000-4000-8000-000000000015"), null, 3, "com.spotify.client" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000416"), new Guid("5e1f0001-0000-4000-8000-000000000016"), null, 4, "steamcommunity.com" },
                    { new Guid("5e1f0001-0000-4000-8000-00000000041e"), new Guid("5e1f0001-0000-4000-8000-00000000001e"), null, 3, "us.zoom.videomeetings" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000516"), new Guid("5e1f0001-0000-4000-8000-000000000016"), null, 2, "com.valvesoftware.android.steam.community" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000616"), new Guid("5e1f0001-0000-4000-8000-000000000016"), null, 3, "com.valvesoftware.Steam" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000108"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000109"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000010a"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000010b"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000010c"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000010d"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000010e"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000010f"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000110"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000111"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000112"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000113"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000114"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000115"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000116"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000117"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000118"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000119"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000011a"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000011b"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000011c"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000011d"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000011e"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000011f"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000120"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000121"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000208"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000209"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000020a"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000020b"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000020c"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000020d"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000020e"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000020f"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000210"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000211"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000212"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000213"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000214"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000215"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000216"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000217"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000218"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000219"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000021a"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000021e"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000021f"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000220"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000221"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000308"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000309"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000030a"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000030b"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000030c"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000030d"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000030e"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000030f"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000310"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000311"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000312"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000313"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000314"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000315"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000316"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000317"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000031a"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000031e"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000031f"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000320"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000321"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000040c"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000040d"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000040f"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000415"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000416"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000041e"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000516"));

            migrationBuilder.DeleteData(
                table: "AppIdentifiers",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000616"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000008"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000009"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000000a"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000000b"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000000c"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000000d"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000000e"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000000f"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000010"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000011"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000012"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000013"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000014"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000015"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000016"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000017"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000018"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000019"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000001a"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000001b"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000001c"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000001d"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000001e"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-00000000001f"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000020"));

            migrationBuilder.DeleteData(
                table: "AppDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("5e1f0001-0000-4000-8000-000000000021"));
        }
    }
}
