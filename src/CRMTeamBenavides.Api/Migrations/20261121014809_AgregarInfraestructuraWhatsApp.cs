using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarInfraestructuraWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversacionesWhatsApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelefonoE164 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WhatsAppId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NombrePerfilWhatsApp = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: true),
                    AsociacionClienteAmbigua = table.Column<bool>(type: "boolean", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    ModoAtencion = table.Column<int>(type: "integer", nullable: false),
                    AgenteAsignadoId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaUltimoMensaje = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaUltimoMensajeEntrante = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaCambioModoHumano = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversacionesWhatsApp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversacionesWhatsApp_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConversacionesWhatsApp_Usuarios_AgenteAsignadoId",
                        column: x => x.AgenteAsignadoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EventosWebhookWhatsApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHashSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TipoEvento = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CantidadMensajes = table.Column<int>(type: "integer", nullable: false),
                    CantidadEstados = table.Column<int>(type: "integer", nullable: false),
                    ProcesadoConExito = table.Column<bool>(type: "boolean", nullable: false),
                    Resumen = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FechaRecepcionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosWebhookWhatsApp", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MensajesWhatsApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Direccion = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    ContenidoTexto = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaEventoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaRecepcionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaUltimoEstadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnviadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    MediaId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MediaMimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MediaSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MediaNombreArchivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MetadataTecnicaJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorDetalle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesWhatsApp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensajesWhatsApp_ConversacionesWhatsApp_ConversacionId",
                        column: x => x.ConversacionId,
                        principalTable: "ConversacionesWhatsApp",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MensajesWhatsApp_Usuarios_EnviadoPorUsuarioId",
                        column: x => x.EnviadoPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMensajesWhatsApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MensajeWhatsAppId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinatarioE164 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContenidoTexto = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    MaxIntentos = table.Column<int>(type: "integer", nullable: false),
                    ProximoIntentoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReservadoHastaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaProcesadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimoError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UltimoCodigoHttp = table.Column<int>(type: "integer", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMensajesWhatsApp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxMensajesWhatsApp_MensajesWhatsApp_MensajeWhatsAppId",
                        column: x => x.MensajeWhatsAppId,
                        principalTable: "MensajesWhatsApp",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversacionesWhatsApp_AgenteAsignadoId",
                table: "ConversacionesWhatsApp",
                column: "AgenteAsignadoId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversacionesWhatsApp_ClienteId",
                table: "ConversacionesWhatsApp",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversacionesWhatsApp_Estado_ModoAtencion_FechaUltimoMensa~",
                table: "ConversacionesWhatsApp",
                columns: new[] { "Estado", "ModoAtencion", "FechaUltimoMensaje" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversacionesWhatsApp_FechaUltimoMensaje",
                table: "ConversacionesWhatsApp",
                column: "FechaUltimoMensaje");

            migrationBuilder.CreateIndex(
                name: "IX_ConversacionesWhatsApp_TelefonoE164",
                table: "ConversacionesWhatsApp",
                column: "TelefonoE164",
                unique: true,
                filter: "\"Activo\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ConversacionesWhatsApp_WhatsAppId",
                table: "ConversacionesWhatsApp",
                column: "WhatsAppId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosWebhookWhatsApp_ExternalMessageId",
                table: "EventosWebhookWhatsApp",
                column: "ExternalMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosWebhookWhatsApp_FechaRecepcionUtc",
                table: "EventosWebhookWhatsApp",
                column: "FechaRecepcionUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EventosWebhookWhatsApp_PayloadHashSha256",
                table: "EventosWebhookWhatsApp",
                column: "PayloadHashSha256");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesWhatsApp_ConversacionId_FechaEventoUtc",
                table: "MensajesWhatsApp",
                columns: new[] { "ConversacionId", "FechaEventoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesWhatsApp_ConversacionId_FechaRecepcionUtc",
                table: "MensajesWhatsApp",
                columns: new[] { "ConversacionId", "FechaRecepcionUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesWhatsApp_EnviadoPorUsuarioId",
                table: "MensajesWhatsApp",
                column: "EnviadoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesWhatsApp_Estado",
                table: "MensajesWhatsApp",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesWhatsApp_ExternalMessageId",
                table: "MensajesWhatsApp",
                column: "ExternalMessageId",
                unique: true,
                filter: "\"ExternalMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesWhatsApp_IdempotencyKey",
                table: "MensajesWhatsApp",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMensajesWhatsApp_ConversacionId",
                table: "OutboxMensajesWhatsApp",
                column: "ConversacionId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMensajesWhatsApp_Estado_ProximoIntentoUtc_ReservadoHa~",
                table: "OutboxMensajesWhatsApp",
                columns: new[] { "Estado", "ProximoIntentoUtc", "ReservadoHastaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMensajesWhatsApp_IdempotencyKey",
                table: "OutboxMensajesWhatsApp",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMensajesWhatsApp_MensajeWhatsAppId",
                table: "OutboxMensajesWhatsApp",
                column: "MensajeWhatsAppId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventosWebhookWhatsApp");

            migrationBuilder.DropTable(
                name: "OutboxMensajesWhatsApp");

            migrationBuilder.DropTable(
                name: "MensajesWhatsApp");

            migrationBuilder.DropTable(
                name: "ConversacionesWhatsApp");
        }
    }
}
