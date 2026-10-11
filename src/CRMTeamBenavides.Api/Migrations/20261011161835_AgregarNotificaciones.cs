using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEnvioPresupuesto",
                table: "OrdenesServicio",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DispositivosNotificacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Plataforma = table.Column<int>(type: "integer", nullable: false),
                    UltimoRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispositivosNotificacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DispositivosNotificacion_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Mensaje = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Entidad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EntidadId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaveAgrupacion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    FechaLeida = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstadoEnvio = table.Column<int>(type: "integer", nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    ProximoIntento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaEnvio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimoError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificaciones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosNotificacion_Token",
                table: "DispositivosNotificacion",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosNotificacion_UsuarioId",
                table: "DispositivosNotificacion",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_EstadoEnvio_ProximoIntento",
                table: "Notificaciones",
                columns: new[] { "EstadoEnvio", "ProximoIntento" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_UsuarioId_ClaveAgrupacion",
                table: "Notificaciones",
                columns: new[] { "UsuarioId", "ClaveAgrupacion" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_UsuarioId_FechaLeida",
                table: "Notificaciones",
                columns: new[] { "UsuarioId", "FechaLeida" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispositivosNotificacion");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "FechaEnvioPresupuesto",
                table: "OrdenesServicio");
        }
    }
}
