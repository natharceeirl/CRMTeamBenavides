using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class FortalecerCorrelativoOrdenServicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrdenesServicio_NumeroOrden",
                table: "OrdenesServicio");

            migrationBuilder.CreateSequence(
                name: "OrdenServicioNumeroSeq");

            // Inicializar la secuencia basándose en el máximo NumeroOrden existente (ej: si existe OS-000007, el siguiente será OS-000008)
            migrationBuilder.Sql(@"
DO $$
DECLARE
    max_val BIGINT;
BEGIN
    SELECT COALESCE(MAX(CAST(SUBSTRING(""NumeroOrden"", 4) AS BIGINT)), 0)
    INTO max_val
    FROM ""OrdenesServicio""
    WHERE ""NumeroOrden"" LIKE 'OS-%';

    IF max_val > 0 THEN
        PERFORM setval('""OrdenServicioNumeroSeq""', max_val, true);
    ELSE
        PERFORM setval('""OrdenServicioNumeroSeq""', 1, false);
    END IF;
END $$;");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesServicio_NumeroOrden",
                table: "OrdenesServicio",
                column: "NumeroOrden",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrdenesServicio_NumeroOrden",
                table: "OrdenesServicio");

            migrationBuilder.DropSequence(
                name: "OrdenServicioNumeroSeq");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesServicio_NumeroOrden",
                table: "OrdenesServicio",
                column: "NumeroOrden");
        }
    }
}
