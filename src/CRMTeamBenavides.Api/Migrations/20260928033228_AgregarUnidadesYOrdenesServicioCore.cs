using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUnidadesYOrdenesServicioCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_OrdenesServicio_OrdenServicioId",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Vehiculos_Placa",
                table: "Vehiculos");

            migrationBuilder.AlterColumn<string>(
                name: "Placa",
                table: "Vehiculos",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<decimal>(
                name: "HorasUso",
                table: "Vehiculos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroMotor",
                table: "Vehiculos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroSerieVIN",
                table: "Vehiculos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoMedidor",
                table: "Vehiculos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipoUnidad",
                table: "Vehiculos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorEstimado",
                table: "Vehiculos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClienteId",
                table: "OrdenesServicio",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEstimadaEntrega",
                table: "OrdenesServicio",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaIngreso",
                table: "OrdenesServicio",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSalida",
                table: "OrdenesServicio",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HorasUsoIngreso",
                table: "OrdenesServicio",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KilometrajeIngreso",
                table: "OrdenesServicio",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModalidadAtencion",
                table: "OrdenesServicio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MotivoFalla",
                table: "OrdenesServicio",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroOrden",
                table: "OrdenesServicio",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Solucion",
                table: "OrdenesServicio",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoAtencion",
                table: "OrdenesServicio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipoFalla",
                table: "OrdenesServicio",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HistorialEstadosOrden",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdenServicioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstadoAnterior = table.Column<int>(type: "integer", nullable: true),
                    EstadoNuevo = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaCambio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadosOrden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosOrden_OrdenesServicio_OrdenServicioId",
                        column: x => x.OrdenServicioId,
                        principalTable: "OrdenesServicio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosOrden_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_NumeroSerieVIN",
                table: "Vehiculos",
                column: "NumeroSerieVIN");

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_Placa",
                table: "Vehiculos",
                column: "Placa",
                unique: true,
                filter: "\"Activo\" = true AND \"Placa\" IS NOT NULL AND \"Placa\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesServicio_ClienteId",
                table: "OrdenesServicio",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesServicio_FechaIngreso",
                table: "OrdenesServicio",
                column: "FechaIngreso");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesServicio_NumeroOrden",
                table: "OrdenesServicio",
                column: "NumeroOrden");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosOrden_FechaCambio",
                table: "HistorialEstadosOrden",
                column: "FechaCambio");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosOrden_OrdenServicioId",
                table: "HistorialEstadosOrden",
                column: "OrdenServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosOrden_UsuarioId",
                table: "HistorialEstadosOrden",
                column: "UsuarioId");

            // Backfill existing OrdenesServicio with ClienteId from Vehiculos, FechaIngreso from FechaApertura, and sequential NumeroOrden
            migrationBuilder.Sql("UPDATE \"OrdenesServicio\" SET \"ClienteId\" = v.\"ClienteId\" FROM \"Vehiculos\" v WHERE \"OrdenesServicio\".\"VehiculoId\" = v.\"Id\";");
            migrationBuilder.Sql("UPDATE \"OrdenesServicio\" SET \"FechaIngreso\" = \"FechaApertura\" WHERE \"FechaIngreso\" = '0001-01-01 00:00:00+00';");
            migrationBuilder.Sql("UPDATE \"OrdenesServicio\" o SET \"NumeroOrden\" = 'OS-' || LPAD(sub.rn::text, 6, '0') FROM (SELECT \"Id\", ROW_NUMBER() OVER (ORDER BY \"FechaApertura\") as rn FROM \"OrdenesServicio\") sub WHERE o.\"Id\" = sub.\"Id\" AND o.\"NumeroOrden\" IS NULL;");

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesServicio_Clientes_ClienteId",
                table: "OrdenesServicio",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_OrdenesServicio_OrdenServicioId",
                table: "Ventas",
                column: "OrdenServicioId",
                principalTable: "OrdenesServicio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesServicio_Clientes_ClienteId",
                table: "OrdenesServicio");

            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_OrdenesServicio_OrdenServicioId",
                table: "Ventas");

            migrationBuilder.DropTable(
                name: "HistorialEstadosOrden");

            migrationBuilder.DropIndex(
                name: "IX_Vehiculos_NumeroSerieVIN",
                table: "Vehiculos");

            migrationBuilder.DropIndex(
                name: "IX_Vehiculos_Placa",
                table: "Vehiculos");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesServicio_ClienteId",
                table: "OrdenesServicio");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesServicio_FechaIngreso",
                table: "OrdenesServicio");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesServicio_NumeroOrden",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "HorasUso",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "NumeroMotor",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "NumeroSerieVIN",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "TipoMedidor",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "TipoUnidad",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "ValorEstimado",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "FechaEstimadaEntrega",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "FechaIngreso",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "FechaSalida",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "HorasUsoIngreso",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "KilometrajeIngreso",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "ModalidadAtencion",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "MotivoFalla",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "NumeroOrden",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "Solucion",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "TipoAtencion",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "TipoFalla",
                table: "OrdenesServicio");

            migrationBuilder.AlterColumn<string>(
                name: "Placa",
                table: "Vehiculos",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_Placa",
                table: "Vehiculos",
                column: "Placa",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_OrdenesServicio_OrdenServicioId",
                table: "Ventas",
                column: "OrdenServicioId",
                principalTable: "OrdenesServicio",
                principalColumn: "Id");
        }
    }
}
