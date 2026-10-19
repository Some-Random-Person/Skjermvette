using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skjermvette.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceUnpairedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UnpairedAtUtc",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnpairedAtUtc",
                table: "Devices");
        }
    }
}
