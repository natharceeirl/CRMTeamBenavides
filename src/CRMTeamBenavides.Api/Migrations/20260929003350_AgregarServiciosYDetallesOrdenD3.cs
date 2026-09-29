using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarServiciosYDetallesOrdenD3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "HorasUsoIngreso",
                table: "OrdenesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LecturaMedidorIngreso",
                table: "OrdenesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoIgv",
                table: "OrdenesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalExonerado",
                table: "OrdenesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalGravado",
                table: "OrdenesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalInafecto",
                table: "OrdenesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "OrdenesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioUnitario",
                table: "DetallesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitarioHistorico",
                table: "DetallesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoIgv",
                table: "DetallesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeIgvAplicado",
                table: "DetallesServicio",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ServicioId",
                table: "DetallesServicio",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalGravado",
                table: "DetallesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TipoAfectacionIgv",
                table: "DetallesServicio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipoItem",
                table: "DetallesServicio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "DetallesServicio",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ConfiguracionesEmpresa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreEmpresa = table.Column<string>(type: "text", nullable: false),
                    Ruc = table.Column<string>(type: "text", nullable: true),
                    PorcentajeIgv = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesEmpresa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Servicios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    PrecioSugerido = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TipoAfectacionIgv = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servicios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesServicio_ServicioId",
                table: "DetallesServicio",
                column: "ServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_Servicios_Nombre",
                table: "Servicios",
                column: "Nombre");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesServicio_Servicios_ServicioId",
                table: "DetallesServicio",
                column: "ServicioId",
                principalTable: "Servicios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
                INSERT INTO ""ConfiguracionesEmpresa"" (""Id"", ""NombreEmpresa"", ""Ruc"", ""PorcentajeIgv"", ""FechaCreacion"", ""Activo"")
                SELECT gen_random_uuid(), 'Team Benavides', '20000000001', 18.00, NOW(), true
                WHERE NOT EXISTS (SELECT 1 FROM ""ConfiguracionesEmpresa"");
            ");

            migrationBuilder.Sql(@"
                UPDATE ""DetallesServicio"" d
                SET ""TipoItem"" = CASE WHEN d.""ProductoId"" IS NOT NULL THEN 0 ELSE 2 END,
                    ""CostoUnitarioHistorico"" = COALESCE((SELECT p.""Costo"" FROM ""Productos"" p WHERE p.""Id"" = d.""ProductoId""), 0),
                    ""TipoAfectacionIgv"" = 0,
                    ""PorcentajeIgvAplicado"" = 18.00,
                    ""SubtotalGravado"" = ROUND(d.""Cantidad"" * d.""PrecioUnitario"", 2),
                    ""MontoIgv"" = ROUND((d.""Cantidad"" * d.""PrecioUnitario"") * 0.18, 2),
                    ""Total"" = ROUND((d.""Cantidad"" * d.""PrecioUnitario"") * 1.18, 2)
                WHERE d.""Total"" = 0;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""OrdenesServicio"" o
                SET ""SubtotalGravado"" = COALESCE((SELECT SUM(d.""SubtotalGravado"") FROM ""DetallesServicio"" d WHERE d.""OrdenServicioId"" = o.""Id"" AND d.""Activo""), 0),
                    ""SubtotalExonerado"" = 0,
                    ""SubtotalInafecto"" = 0,
                    ""MontoIgv"" = COALESCE((SELECT SUM(d.""MontoIgv"") FROM ""DetallesServicio"" d WHERE d.""OrdenServicioId"" = o.""Id"" AND d.""Activo""), 0),
                    ""Total"" = COALESCE((SELECT SUM(d.""Total"") FROM ""DetallesServicio"" d WHERE d.""OrdenServicioId"" = o.""Id"" AND d.""Activo""), 0)
                WHERE o.""Total"" = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesServicio_Servicios_ServicioId",
                table: "DetallesServicio");

            migrationBuilder.DropTable(
                name: "ConfiguracionesEmpresa");

            migrationBuilder.DropTable(
                name: "Servicios");

            migrationBuilder.DropIndex(
                name: "IX_DetallesServicio_ServicioId",
                table: "DetallesServicio");

            migrationBuilder.DropColumn(
                name: "LecturaMedidorIngreso",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "MontoIgv",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "SubtotalExonerado",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "SubtotalGravado",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "SubtotalInafecto",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "CostoUnitarioHistorico",
                table: "DetallesServicio");

            migrationBuilder.DropColumn(
                name: "MontoIgv",
                table: "DetallesServicio");

            migrationBuilder.DropColumn(
                name: "PorcentajeIgvAplicado",
                table: "DetallesServicio");

            migrationBuilder.DropColumn(
                name: "ServicioId",
                table: "DetallesServicio");

            migrationBuilder.DropColumn(
                name: "SubtotalGravado",
                table: "DetallesServicio");

            migrationBuilder.DropColumn(
                name: "TipoAfectacionIgv",
                table: "DetallesServicio");

            migrationBuilder.DropColumn(
                name: "TipoItem",
                table: "DetallesServicio");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "DetallesServicio");

            migrationBuilder.AlterColumn<decimal>(
                name: "HorasUsoIngreso",
                table: "OrdenesServicio",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioUnitario",
                table: "DetallesServicio",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);
        }
    }
}
