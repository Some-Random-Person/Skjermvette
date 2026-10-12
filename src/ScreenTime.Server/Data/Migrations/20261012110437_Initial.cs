using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ScreenTime.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Families",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Families", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppDefinitions_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Children",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResetHour = table.Column<int>(type: "integer", nullable: false),
                    DailyTotal = table.Column<TimeSpan>(type: "interval", nullable: false),
                    OfflineAllowance = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Children", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Children_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppIdentifiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppIdentifiers_AppDefinitions_AppDefinitionId",
                        column: x => x.AppDefinitionId,
                        principalTable: "AppDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppIdentifiers_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Limit = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppRules_AppDefinitions_AppDefinitionId",
                        column: x => x.AppDefinitionId,
                        principalTable: "AppDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppRules_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PairedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfflineAllowanceOverride = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExtraTimeGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<TimeSpan>(type: "interval", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtraTimeGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtraTimeGrants_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PairingCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PairingCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PairingCodes_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceLeases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Total = table.Column<TimeSpan>(type: "interval", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLeases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceLeases_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RawIdentifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageRecords_AppDefinitions_AppDefinitionId",
                        column: x => x.AppDefinitionId,
                        principalTable: "AppDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UsageRecords_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsageRecords_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceLeaseApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLeaseApp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceLeaseApp_DeviceLeases_LeaseId",
                        column: x => x.LeaseId,
                        principalTable: "DeviceLeases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppDefinitions",
                columns: new[] { "Id", "FamilyId", "Name" },
                values: new object[,]
                {
                    { new Guid("5e1f0001-0000-4000-8000-000000000001"), null, "YouTube" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000002"), null, "Microsoft Teams" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000003"), null, "TikTok" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000004"), null, "Roblox" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000005"), null, "Minecraft" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000006"), null, "Discord" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000007"), null, "Netflix" }
                });

            migrationBuilder.InsertData(
                table: "AppIdentifiers",
                columns: new[] { "Id", "AppDefinitionId", "FamilyId", "Kind", "Value" },
                values: new object[,]
                {
                    { new Guid("5e1f0001-0000-4000-8000-000000000101"), new Guid("5e1f0001-0000-4000-8000-000000000001"), null, 4, "youtube.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000102"), new Guid("5e1f0001-0000-4000-8000-000000000002"), null, 1, "ms-teams.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000103"), new Guid("5e1f0001-0000-4000-8000-000000000003"), null, 4, "tiktok.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000104"), new Guid("5e1f0001-0000-4000-8000-000000000004"), null, 1, "robloxplayerbeta.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000105"), new Guid("5e1f0001-0000-4000-8000-000000000005"), null, 1, "minecraft.windows.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000106"), new Guid("5e1f0001-0000-4000-8000-000000000006"), null, 1, "discord.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000107"), new Guid("5e1f0001-0000-4000-8000-000000000007"), null, 4, "netflix.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000201"), new Guid("5e1f0001-0000-4000-8000-000000000001"), null, 4, "youtu.be" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000202"), new Guid("5e1f0001-0000-4000-8000-000000000002"), null, 1, "teams.exe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000203"), new Guid("5e1f0001-0000-4000-8000-000000000003"), null, 2, "com.zhiliaoapp.musically" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000204"), new Guid("5e1f0001-0000-4000-8000-000000000004"), null, 4, "roblox.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000205"), new Guid("5e1f0001-0000-4000-8000-000000000005"), null, 2, "com.mojang.minecraftpe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000206"), new Guid("5e1f0001-0000-4000-8000-000000000006"), null, 4, "discord.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000207"), new Guid("5e1f0001-0000-4000-8000-000000000007"), null, 2, "com.netflix.mediaclient" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000301"), new Guid("5e1f0001-0000-4000-8000-000000000001"), null, 4, "youtube-nocookie.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000302"), new Guid("5e1f0001-0000-4000-8000-000000000002"), null, 4, "teams.microsoft.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000303"), new Guid("5e1f0001-0000-4000-8000-000000000003"), null, 3, "com.zhiliaoapp.musically" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000304"), new Guid("5e1f0001-0000-4000-8000-000000000004"), null, 2, "com.roblox.client" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000305"), new Guid("5e1f0001-0000-4000-8000-000000000005"), null, 3, "com.mojang.minecraftpe" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000306"), new Guid("5e1f0001-0000-4000-8000-000000000006"), null, 2, "com.discord" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000307"), new Guid("5e1f0001-0000-4000-8000-000000000007"), null, 3, "com.netflix.Netflix" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000401"), new Guid("5e1f0001-0000-4000-8000-000000000001"), null, 2, "com.google.android.youtube" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000402"), new Guid("5e1f0001-0000-4000-8000-000000000002"), null, 4, "teams.live.com" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000404"), new Guid("5e1f0001-0000-4000-8000-000000000004"), null, 3, "com.roblox.robloxmobile" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000406"), new Guid("5e1f0001-0000-4000-8000-000000000006"), null, 3, "com.hammerandchisel.discord" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000501"), new Guid("5e1f0001-0000-4000-8000-000000000001"), null, 3, "com.google.ios.youtube" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000502"), new Guid("5e1f0001-0000-4000-8000-000000000002"), null, 2, "com.microsoft.teams" },
                    { new Guid("5e1f0001-0000-4000-8000-000000000602"), new Guid("5e1f0001-0000-4000-8000-000000000002"), null, 3, "com.microsoft.skype.teams" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDefinitions_FamilyId",
                table: "AppDefinitions",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppIdentifiers_AppDefinitionId",
                table: "AppIdentifiers",
                column: "AppDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppIdentifiers_FamilyId_Kind_Value",
                table: "AppIdentifiers",
                columns: new[] { "FamilyId", "Kind", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_AppRules_AppDefinitionId",
                table: "AppRules",
                column: "AppDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRules_ChildId_AppDefinitionId",
                table: "AppRules",
                columns: new[] { "ChildId", "AppDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_FamilyId",
                table: "AspNetUsers",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Children_FamilyId",
                table: "Children",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLeaseApp_LeaseId",
                table: "DeviceLeaseApp",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLeases_DeviceId",
                table: "DeviceLeases",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ChildId",
                table: "Devices",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TokenHash",
                table: "Devices",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExtraTimeGrants_ChildId_DayStartUtc",
                table: "ExtraTimeGrants",
                columns: new[] { "ChildId", "DayStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PairingCodes_ChildId",
                table: "PairingCodes",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_PairingCodes_Code",
                table: "PairingCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_AppDefinitionId",
                table: "UsageRecords",
                column: "AppDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_ChildId_StartUtc",
                table: "UsageRecords",
                columns: new[] { "ChildId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_DeviceId",
                table: "UsageRecords",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppIdentifiers");

            migrationBuilder.DropTable(
                name: "AppRules");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "DeviceLeaseApp");

            migrationBuilder.DropTable(
                name: "ExtraTimeGrants");

            migrationBuilder.DropTable(
                name: "PairingCodes");

            migrationBuilder.DropTable(
                name: "UsageRecords");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "DeviceLeases");

            migrationBuilder.DropTable(
                name: "AppDefinitions");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Children");

            migrationBuilder.DropTable(
                name: "Families");
        }
    }
}
