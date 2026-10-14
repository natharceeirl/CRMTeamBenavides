using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarYamahaSyncLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YamahaSyncLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Servicio = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegistrosProcesados = table.Column<int>(type: "integer", nullable: false),
                    RegistrosValidos = table.Column<int>(type: "integer", nullable: false),
                    RegistrosRechazados = table.Column<int>(type: "integer", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CodigoHttp = table.Column<int>(type: "integer", nullable: true),
                    RespuestaCuerpo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MensajeError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Exitoso = table.Column<bool>(type: "boolean", nullable: false),
                    EsSimulacion = table.Column<bool>(type: "boolean", nullable: false),
                    ModoEnvio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YamahaSyncLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YamahaSyncLogs_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YamahaSyncLogs_Estado",
                table: "YamahaSyncLogs",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_YamahaSyncLogs_Exitoso",
                table: "YamahaSyncLogs",
                column: "Exitoso");

            migrationBuilder.CreateIndex(
                name: "IX_YamahaSyncLogs_FechaInicio",
                table: "YamahaSyncLogs",
                column: "FechaInicio");

            migrationBuilder.CreateIndex(
                name: "IX_YamahaSyncLogs_Servicio_FechaInicio",
                table: "YamahaSyncLogs",
                columns: new[] { "Servicio", "FechaInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_YamahaSyncLogs_UsuarioId",
                table: "YamahaSyncLogs",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YamahaSyncLogs");
        }
    }
}
