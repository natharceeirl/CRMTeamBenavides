using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <summary>
    /// Las órdenes abiertas antes del formato de atención quedaron con FechaIngreso
    /// en 0001-01-01, el valor por defecto de la columna. Para esas, el ingreso de
    /// la unidad es la apertura de la orden.
    /// </summary>
    public partial class CompletarFechaIngresoOrdenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""OrdenesServicio""
                SET ""FechaIngreso"" = ""FechaApertura""
                WHERE ""FechaIngreso"" < TIMESTAMPTZ '1900-01-01 00:00:00+00';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta atrás: la fecha de 0001-01-01 no era un dato real.
        }
    }
}
