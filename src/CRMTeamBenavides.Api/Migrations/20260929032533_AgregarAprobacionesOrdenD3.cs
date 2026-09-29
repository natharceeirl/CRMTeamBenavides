using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAprobacionesOrdenD3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstadoAprobacionGerencia",
                table: "OrdenesServicio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EstadoPresupuestoCliente",
                table: "OrdenesServicio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAprobacionGerencia",
                table: "OrdenesServicio",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRespuestaCliente",
                table: "OrdenesServicio",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionesAprobacionGerencia",
                table: "OrdenesServicio",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionesPresupuestoCliente",
                table: "OrdenesServicio",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioAprobacionGerenciaId",
                table: "OrdenesServicio",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesServicio_UsuarioAprobacionGerenciaId",
                table: "OrdenesServicio",
                column: "UsuarioAprobacionGerenciaId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesServicio_Usuarios_UsuarioAprobacionGerenciaId",
                table: "OrdenesServicio",
                column: "UsuarioAprobacionGerenciaId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesServicio_Usuarios_UsuarioAprobacionGerenciaId",
                table: "OrdenesServicio");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesServicio_UsuarioAprobacionGerenciaId",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "EstadoAprobacionGerencia",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "EstadoPresupuestoCliente",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "FechaAprobacionGerencia",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "FechaRespuestaCliente",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "ObservacionesAprobacionGerencia",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "ObservacionesPresupuestoCliente",
                table: "OrdenesServicio");

            migrationBuilder.DropColumn(
                name: "UsuarioAprobacionGerenciaId",
                table: "OrdenesServicio");
        }
    }
}
