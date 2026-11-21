using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarHistorialIntentosOutboxWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HistorialIntentosJson",
                table: "OutboxMensajesWhatsApp",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HistorialIntentosJson",
                table: "OutboxMensajesWhatsApp");
        }
    }
}
