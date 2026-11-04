using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class PermitirVariosTiposDeAtencion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "TiposAtencion",
                table: "OrdenesServicio",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            // Cada orden que ya existe lleva su único tipo en la lista.
            migrationBuilder.Sql("""
                UPDATE "OrdenesServicio"
                SET "TiposAtencion" = ARRAY["TipoAtencion"]
                WHERE cardinality("TiposAtencion") = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TiposAtencion",
                table: "OrdenesServicio");
        }
    }
}
