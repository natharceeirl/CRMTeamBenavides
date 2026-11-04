using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class HacerUnicaLaSerieDeLaUnidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehiculos_NumeroSerieVIN",
                table: "Vehiculos");

            // Las series guardadas antes quedan como se escriben ahora: sin espacios, en
            // mayúsculas y nula si estaba vacía. Así «abc1 » y «ABC1» no esquivan el índice.
            migrationBuilder.Sql("""
                UPDATE "Vehiculos"
                SET "NumeroSerieVIN" = NULLIF(UPPER(BTRIM("NumeroSerieVIN")), '')
                WHERE "NumeroSerieVIN" IS DISTINCT FROM NULLIF(UPPER(BTRIM("NumeroSerieVIN")), '');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_NumeroSerieVIN",
                table: "Vehiculos",
                column: "NumeroSerieVIN",
                unique: true,
                filter: "\"Activo\" = true AND \"NumeroSerieVIN\" IS NOT NULL AND \"NumeroSerieVIN\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehiculos_NumeroSerieVIN",
                table: "Vehiculos");

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_NumeroSerieVIN",
                table: "Vehiculos",
                column: "NumeroSerieVIN");
        }
    }
}
