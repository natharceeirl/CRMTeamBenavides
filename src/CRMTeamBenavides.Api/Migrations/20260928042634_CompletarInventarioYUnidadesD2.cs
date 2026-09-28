using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class CompletarInventarioYUnidadesD2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ValorEstimado",
                table: "Vehiculos",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "HorasUso",
                table: "Vehiculos",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LecturaMedidorActual",
                table: "Vehiculos",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "StockMinimo",
                table: "Productos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioVenta",
                table: "Productos",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "Costo",
                table: "Productos",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitario",
                table: "MovimientosInventario",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroDocumento",
                table: "Clientes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoDocumento",
                table: "Clientes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockMinimoDefault",
                table: "CategoriasProducto",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_Marca",
                table: "Vehiculos",
                column: "Marca");

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_Modelo",
                table: "Vehiculos",
                column: "Modelo");

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_NumeroMotor",
                table: "Vehiculos",
                column: "NumeroMotor");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_NumeroDocumento",
                table: "Clientes",
                column: "NumeroDocumento",
                unique: true,
                filter: "\"Activo\" = true AND \"NumeroDocumento\" IS NOT NULL AND \"NumeroDocumento\" <> ''");

            // Backfill 1: Migrar DocumentoIdentidad a NumeroDocumento e inferir TipoDocumento (DNI: 8 dígitos, RUC: 11 dígitos, Otro: resto)
            migrationBuilder.Sql(@"
UPDATE ""Clientes""
SET ""NumeroDocumento"" = TRIM(""DocumentoIdentidad""),
    ""TipoDocumento"" = CASE 
        WHEN LENGTH(TRIM(""DocumentoIdentidad"")) = 8 THEN 0 
        WHEN LENGTH(TRIM(""DocumentoIdentidad"")) = 11 THEN 1 
        ELSE 2 
    END
WHERE ""NumeroDocumento"" IS NULL AND ""DocumentoIdentidad"" IS NOT NULL;");

            // Backfill 2: Poblar LecturaMedidorActual a partir de Kilometraje u HorasUso existentes
            migrationBuilder.Sql(@"
UPDATE ""Vehiculos""
SET ""LecturaMedidorActual"" = CASE 
    WHEN ""TipoMedidor"" = 0 THEN ""Kilometraje"" 
    ELSE ""HorasUso"" 
END
WHERE ""LecturaMedidorActual"" IS NULL;");

            // Backfill 3: Asignar costo base referencial (70% del precio de venta) a productos históricos con costo 0
            migrationBuilder.Sql(@"
UPDATE ""Productos""
SET ""Costo"" = ROUND(""PrecioVenta"" * 0.70, 2)
WHERE ""Costo"" = 0;");

            // Backfill 4: Poblar CostoUnitario en movimientos de inventario históricos a partir del costo del producto
            migrationBuilder.Sql(@"
UPDATE ""MovimientosInventario"" m
SET ""CostoUnitario"" = p.""Costo""
FROM ""Productos"" p
WHERE m.""ProductoId"" = p.""Id"" AND m.""CostoUnitario"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehiculos_Marca",
                table: "Vehiculos");

            migrationBuilder.DropIndex(
                name: "IX_Vehiculos_Modelo",
                table: "Vehiculos");

            migrationBuilder.DropIndex(
                name: "IX_Vehiculos_NumeroMotor",
                table: "Vehiculos");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_NumeroDocumento",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "LecturaMedidorActual",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "Costo",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CostoUnitario",
                table: "MovimientosInventario");

            migrationBuilder.DropColumn(
                name: "NumeroDocumento",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "TipoDocumento",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "StockMinimoDefault",
                table: "CategoriasProducto");

            migrationBuilder.AlterColumn<decimal>(
                name: "ValorEstimado",
                table: "Vehiculos",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "HorasUso",
                table: "Vehiculos",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "StockMinimo",
                table: "Productos",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioVenta",
                table: "Productos",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);
        }
    }
}
