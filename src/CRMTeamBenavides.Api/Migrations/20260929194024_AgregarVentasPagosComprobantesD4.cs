using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarVentasPagosComprobantesD4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Total",
                table: "Ventas",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "MontoIgv",
                table: "Ventas",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalExonerado",
                table: "Ventas",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalGravado",
                table: "Ventas",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalInafecto",
                table: "Ventas",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductoId",
                table: "DetallesVenta",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioUnitario",
                table: "DetallesVenta",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitarioHistorico",
                table: "DetallesVenta",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "DetalleServicioOrigenId",
                table: "DetallesVenta",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoIgv",
                table: "DetallesVenta",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeIgvAplicado",
                table: "DetallesVenta",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ServicioId",
                table: "DetallesVenta",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalGravado",
                table: "DetallesVenta",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TipoAfectacionIgv",
                table: "DetallesVenta",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipoItem",
                table: "DetallesVenta",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "DetallesVenta",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MetodoPagoPrincipal",
                table: "Comprobantes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoIgv",
                table: "Comprobantes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Observaciones",
                table: "Comprobantes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrdenServicioId",
                table: "Comprobantes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeIgv",
                table: "Comprobantes",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalExonerado",
                table: "Comprobantes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalGravado",
                table: "Comprobantes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalInafecto",
                table: "Comprobantes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Comprobantes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "MetodosPago",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetodosPago", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pagos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MetodoPagoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Referencia = table.Column<string>(type: "text", nullable: true),
                    VentaId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrdenServicioId = table.Column<Guid>(type: "uuid", nullable: true),
                    EsAnticipo = table.Column<bool>(type: "boolean", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagos_MetodosPago_MetodoPagoId",
                        column: x => x.MetodoPagoId,
                        principalTable: "MetodosPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_OrdenesServicio_OrdenServicioId",
                        column: x => x.OrdenServicioId,
                        principalTable: "OrdenesServicio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pagos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Pagos_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_ServicioId",
                table: "DetallesVenta",
                column: "ServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_OrdenServicioId",
                table: "Comprobantes",
                column: "OrdenServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_MetodosPago_Codigo",
                table: "MetodosPago",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_Fecha",
                table: "Pagos",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_MetodoPagoId",
                table: "Pagos",
                column: "MetodoPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_OrdenServicioId",
                table: "Pagos",
                column: "OrdenServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_UsuarioId",
                table: "Pagos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_VentaId",
                table: "Pagos",
                column: "VentaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comprobantes_OrdenesServicio_OrdenServicioId",
                table: "Comprobantes",
                column: "OrdenServicioId",
                principalTable: "OrdenesServicio",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesVenta_Servicios_ServicioId",
                table: "DetallesVenta",
                column: "ServicioId",
                principalTable: "Servicios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comprobantes_OrdenesServicio_OrdenServicioId",
                table: "Comprobantes");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesVenta_Servicios_ServicioId",
                table: "DetallesVenta");

            migrationBuilder.DropTable(
                name: "Pagos");

            migrationBuilder.DropTable(
                name: "MetodosPago");

            migrationBuilder.DropIndex(
                name: "IX_DetallesVenta_ServicioId",
                table: "DetallesVenta");

            migrationBuilder.DropIndex(
                name: "IX_Comprobantes_OrdenServicioId",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "MontoIgv",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "SubtotalExonerado",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "SubtotalGravado",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "SubtotalInafecto",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CostoUnitarioHistorico",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "DetalleServicioOrigenId",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "MontoIgv",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "PorcentajeIgvAplicado",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "ServicioId",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "SubtotalGravado",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "TipoAfectacionIgv",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "TipoItem",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "MetodoPagoPrincipal",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "MontoIgv",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "Observaciones",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "OrdenServicioId",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "PorcentajeIgv",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "SubtotalExonerado",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "SubtotalGravado",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "SubtotalInafecto",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Comprobantes");

            migrationBuilder.AlterColumn<decimal>(
                name: "Total",
                table: "Ventas",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductoId",
                table: "DetallesVenta",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioUnitario",
                table: "DetallesVenta",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);
        }
    }
}
