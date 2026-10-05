using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAprobacionesPreciosCajaAdelantosD10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstadoAprobacionGerencia",
                table: "Ventas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAprobacionGerencia",
                table: "Ventas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionesAprobacionGerencia",
                table: "Ventas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioAprobacionGerenciaId",
                table: "Ventas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoUrl",
                table: "Productos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Marca",
                table: "Productos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstadoAprobacionGerencia",
                table: "PedidosLima",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAprobacionGerencia",
                table: "PedidosLima",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionesAprobacionGerencia",
                table: "PedidosLima",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioAprobacionGerenciaId",
                table: "PedidosLima",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PedidoLimaId",
                table: "Pagos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MetodoPagoId",
                table: "MovimientosCajaChica",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetodoPagoNombre",
                table: "MovimientosCajaChica",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PagoId",
                table: "MovimientosCajaChica",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SolicitudesAprobacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Entidad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntidadId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioSolicitanteId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsuarioSolicitanteNombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FechaSolicitud = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    DetalleCambio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ValorAnterior = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ValorSolicitado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UsuarioAprobadorId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsuarioAprobadorNombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FechaRespuesta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObservacionesRespuesta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesAprobacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesAprobacion_Usuarios_UsuarioAprobadorId",
                        column: x => x.UsuarioAprobadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SolicitudesAprobacion_Usuarios_UsuarioSolicitanteId",
                        column: x => x.UsuarioSolicitanteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_UsuarioAprobacionGerenciaId",
                table: "Ventas",
                column: "UsuarioAprobacionGerenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosLima_UsuarioAprobacionGerenciaId",
                table: "PedidosLima",
                column: "UsuarioAprobacionGerenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_PedidoLimaId",
                table: "Pagos",
                column: "PedidoLimaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCajaChica_MetodoPagoId",
                table: "MovimientosCajaChica",
                column: "MetodoPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCajaChica_PagoId",
                table: "MovimientosCajaChica",
                column: "PagoId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAprobacion_Entidad",
                table: "SolicitudesAprobacion",
                column: "Entidad");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAprobacion_EntidadId",
                table: "SolicitudesAprobacion",
                column: "EntidadId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAprobacion_Estado",
                table: "SolicitudesAprobacion",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAprobacion_FechaSolicitud",
                table: "SolicitudesAprobacion",
                column: "FechaSolicitud");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAprobacion_UsuarioAprobadorId",
                table: "SolicitudesAprobacion",
                column: "UsuarioAprobadorId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAprobacion_UsuarioSolicitanteId",
                table: "SolicitudesAprobacion",
                column: "UsuarioSolicitanteId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCajaChica_MetodosPago_MetodoPagoId",
                table: "MovimientosCajaChica",
                column: "MetodoPagoId",
                principalTable: "MetodosPago",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCajaChica_Pagos_PagoId",
                table: "MovimientosCajaChica",
                column: "PagoId",
                principalTable: "Pagos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_PedidosLima_PedidoLimaId",
                table: "Pagos",
                column: "PedidoLimaId",
                principalTable: "PedidosLima",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosLima_Usuarios_UsuarioAprobacionGerenciaId",
                table: "PedidosLima",
                column: "UsuarioAprobacionGerenciaId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Usuarios_UsuarioAprobacionGerenciaId",
                table: "Ventas",
                column: "UsuarioAprobacionGerenciaId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCajaChica_MetodosPago_MetodoPagoId",
                table: "MovimientosCajaChica");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCajaChica_Pagos_PagoId",
                table: "MovimientosCajaChica");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_PedidosLima_PedidoLimaId",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_PedidosLima_Usuarios_UsuarioAprobacionGerenciaId",
                table: "PedidosLima");

            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Usuarios_UsuarioAprobacionGerenciaId",
                table: "Ventas");

            migrationBuilder.DropTable(
                name: "SolicitudesAprobacion");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_UsuarioAprobacionGerenciaId",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_PedidosLima_UsuarioAprobacionGerenciaId",
                table: "PedidosLima");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_PedidoLimaId",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCajaChica_MetodoPagoId",
                table: "MovimientosCajaChica");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCajaChica_PagoId",
                table: "MovimientosCajaChica");

            migrationBuilder.DropColumn(
                name: "EstadoAprobacionGerencia",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "FechaAprobacionGerencia",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "ObservacionesAprobacionGerencia",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "UsuarioAprobacionGerenciaId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "FotoUrl",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Marca",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "EstadoAprobacionGerencia",
                table: "PedidosLima");

            migrationBuilder.DropColumn(
                name: "FechaAprobacionGerencia",
                table: "PedidosLima");

            migrationBuilder.DropColumn(
                name: "ObservacionesAprobacionGerencia",
                table: "PedidosLima");

            migrationBuilder.DropColumn(
                name: "UsuarioAprobacionGerenciaId",
                table: "PedidosLima");

            migrationBuilder.DropColumn(
                name: "PedidoLimaId",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "MetodoPagoId",
                table: "MovimientosCajaChica");

            migrationBuilder.DropColumn(
                name: "MetodoPagoNombre",
                table: "MovimientosCajaChica");

            migrationBuilder.DropColumn(
                name: "PagoId",
                table: "MovimientosCajaChica");
        }
    }
}
