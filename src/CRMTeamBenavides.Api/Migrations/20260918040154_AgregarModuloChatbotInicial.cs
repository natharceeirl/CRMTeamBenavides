using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarModuloChatbotInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FaqItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Pregunta = table.Column<string>(type: "text", nullable: false),
                    Respuesta = table.Column<string>(type: "text", nullable: false),
                    PalabrasClave = table.Column<string>(type: "text", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    VecesConsultada = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsultasChatbot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Canal = table.Column<string>(type: "text", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: true),
                    NombreContacto = table.Column<string>(type: "text", nullable: true),
                    TelefonoContacto = table.Column<string>(type: "text", nullable: true),
                    MensajeConsulta = table.Column<string>(type: "text", nullable: false),
                    FaqItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiereAtencionAgente = table.Column<bool>(type: "boolean", nullable: false),
                    EstadoAtencion = table.Column<int>(type: "integer", nullable: false),
                    AgenteAsignadoId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotasAgente = table.Column<string>(type: "text", nullable: true),
                    FechaDerivacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultasChatbot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultasChatbot_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsultasChatbot_FaqItems_FaqItemId",
                        column: x => x.FaqItemId,
                        principalTable: "FaqItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsultasChatbot_Usuarios_AgenteAsignadoId",
                        column: x => x.AgenteAsignadoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasChatbot_AgenteAsignadoId",
                table: "ConsultasChatbot",
                column: "AgenteAsignadoId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasChatbot_ClienteId",
                table: "ConsultasChatbot",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultasChatbot_FaqItemId",
                table: "ConsultasChatbot",
                column: "FaqItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsultasChatbot");

            migrationBuilder.DropTable(
                name: "FaqItems");
        }
    }
}
