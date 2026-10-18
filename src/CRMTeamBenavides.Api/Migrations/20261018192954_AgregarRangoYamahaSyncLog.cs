using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRangoYamahaSyncLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RangoDesdeUtc",
                table: "YamahaSyncLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RangoHastaUtc",
                table: "YamahaSyncLogs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RangoDesdeUtc",
                table: "YamahaSyncLogs");

            migrationBuilder.DropColumn(
                name: "RangoHastaUtc",
                table: "YamahaSyncLogs");
        }
    }
}
