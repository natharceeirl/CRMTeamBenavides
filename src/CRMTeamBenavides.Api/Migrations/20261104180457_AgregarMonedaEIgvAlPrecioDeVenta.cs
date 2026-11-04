using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMonedaEIgvAlPrecioDeVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonedaPrecio",
                table: "Productos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PrecioIncluyeIgv",
                table: "Productos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioUnitarioConIgv",
                table: "DetallesVenta",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioUnitarioConIgv",
                table: "DetallesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioUnitarioConIgv",
                table: "DetallesPedidoLima",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonedaPrecio",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "PrecioIncluyeIgv",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "PrecioUnitarioConIgv",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "PrecioUnitarioConIgv",
                table: "DetallesServicio");

            migrationBuilder.DropColumn(
                name: "PrecioUnitarioConIgv",
                table: "DetallesPedidoLima");
        }
    }
}
