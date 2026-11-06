using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMotosAlInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Dua",
                table: "Vehiculos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaInicioGarantia",
                table: "Vehiculos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemDua",
                table: "Vehiculos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroTarjetaPropiedad",
                table: "Vehiculos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "Productos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "UnidadesInventarioIds",
                table: "DetallesVenta",
                type: "uuid[]",
                nullable: false,
                // Las líneas que ya existen no eligen motos.
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.CreateTable(
                name: "UnidadesInventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroSerieVIN = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NumeroMotor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Anio = table.Column<int>(type: "integer", nullable: true),
                    Color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Dua = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ItemDua = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CostoUnitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    CompraId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetalleCompraId = table.Column<Guid>(type: "uuid", nullable: true),
                    VentaId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetalleVentaId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaVenta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VehiculoId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadesInventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnidadesInventario_Compras_CompraId",
                        column: x => x.CompraId,
                        principalTable: "Compras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnidadesInventario_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnidadesInventario_Vehiculos_VehiculoId",
                        column: x => x.VehiculoId,
                        principalTable: "Vehiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UnidadesInventario_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesInventario_CompraId",
                table: "UnidadesInventario",
                column: "CompraId");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesInventario_NumeroSerieVIN",
                table: "UnidadesInventario",
                column: "NumeroSerieVIN",
                unique: true,
                filter: "\"Activo\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesInventario_ProductoId_Estado",
                table: "UnidadesInventario",
                columns: new[] { "ProductoId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesInventario_VehiculoId",
                table: "UnidadesInventario",
                column: "VehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesInventario_VentaId",
                table: "UnidadesInventario",
                column: "VentaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnidadesInventario");

            migrationBuilder.DropColumn(
                name: "Dua",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "FechaInicioGarantia",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "ItemDua",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "NumeroTarjetaPropiedad",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "UnidadesInventarioIds",
                table: "DetallesVenta");
        }
    }
}
