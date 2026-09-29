using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCajaChicaTipoCambioConfigD5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "ConfiguracionesEmpresa",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "ConfiguracionesEmpresa",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaActualizacionTipoCambio",
                table: "ConfiguracionesEmpresa",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MonedaBase",
                table: "ConfiguracionesEmpresa",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RazonSocial",
                table: "ConfiguracionesEmpresa",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "ConfiguracionesEmpresa",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TipoCambioVigente",
                table: "ConfiguracionesEmpresa",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CajasChicas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MontoApertura = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MontoCierre = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    SaldoCalculado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    FechaApertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    ObservacionesApertura = table.Column<string>(type: "text", nullable: true),
                    ObservacionesCierre = table.Column<string>(type: "text", nullable: true),
                    UsuarioAperturaId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsuarioCierreId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CajasChicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CajasChicas_Usuarios_UsuarioAperturaId",
                        column: x => x.UsuarioAperturaId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CajasChicas_Usuarios_UsuarioCierreId",
                        column: x => x.UsuarioCierreId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HistorialTiposCambio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonedaOrigen = table.Column<string>(type: "text", nullable: false),
                    MonedaDestino = table.Column<string>(type: "text", nullable: false),
                    ValorCompra = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    ValorVenta = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    FechaVigencia = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialTiposCambio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialTiposCambio_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCajaChica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CajaChicaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Concepto = table.Column<string>(type: "text", nullable: false),
                    Referencia = table.Column<string>(type: "text", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCajaChica", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosCajaChica_CajasChicas_CajaChicaId",
                        column: x => x.CajaChicaId,
                        principalTable: "CajasChicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimientosCajaChica_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CajasChicas_Estado",
                table: "CajasChicas",
                column: "Estado",
                unique: true,
                filter: "\"Estado\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CajasChicas_FechaApertura",
                table: "CajasChicas",
                column: "FechaApertura");

            migrationBuilder.CreateIndex(
                name: "IX_CajasChicas_UsuarioAperturaId",
                table: "CajasChicas",
                column: "UsuarioAperturaId");

            migrationBuilder.CreateIndex(
                name: "IX_CajasChicas_UsuarioCierreId",
                table: "CajasChicas",
                column: "UsuarioCierreId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialTiposCambio_FechaVigencia",
                table: "HistorialTiposCambio",
                column: "FechaVigencia");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialTiposCambio_UsuarioId",
                table: "HistorialTiposCambio",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCajaChica_CajaChicaId",
                table: "MovimientosCajaChica",
                column: "CajaChicaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCajaChica_Fecha",
                table: "MovimientosCajaChica",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCajaChica_Tipo",
                table: "MovimientosCajaChica",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCajaChica_UsuarioId",
                table: "MovimientosCajaChica",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialTiposCambio");

            migrationBuilder.DropTable(
                name: "MovimientosCajaChica");

            migrationBuilder.DropTable(
                name: "CajasChicas");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "ConfiguracionesEmpresa");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "ConfiguracionesEmpresa");

            migrationBuilder.DropColumn(
                name: "FechaActualizacionTipoCambio",
                table: "ConfiguracionesEmpresa");

            migrationBuilder.DropColumn(
                name: "MonedaBase",
                table: "ConfiguracionesEmpresa");

            migrationBuilder.DropColumn(
                name: "RazonSocial",
                table: "ConfiguracionesEmpresa");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "ConfiguracionesEmpresa");

            migrationBuilder.DropColumn(
                name: "TipoCambioVigente",
                table: "ConfiguracionesEmpresa");
        }
    }
}
