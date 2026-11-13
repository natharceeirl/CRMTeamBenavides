using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSeleccionExplicitaYamaha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReportarAYamaha",
                table: "Ventas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReportarAYamaha",
                table: "OrdenesServicio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_ReportarAYamaha_Activo",
                table: "Ventas",
                columns: new[] { "ReportarAYamaha", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesServicio_ReportarAYamaha_Activo",
                table: "OrdenesServicio",
                columns: new[] { "ReportarAYamaha", "Activo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ventas_ReportarAYamaha_Activo",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesServicio_ReportarAYamaha_Activo",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "ReportarAYamaha",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "ReportarAYamaha",
                table: "OrdenesServicio");
        }
    }
}
