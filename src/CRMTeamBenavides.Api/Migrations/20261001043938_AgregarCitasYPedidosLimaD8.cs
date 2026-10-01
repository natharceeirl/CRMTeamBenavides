using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMTeamBenavides.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCitasYPedidosLimaD8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "CitaNumeroSeq");

            migrationBuilder.CreateSequence(
                name: "PedidoLimaNumeroSeq");

            migrationBuilder.AddColumn<Guid>(
                name: "PedidoLimaId",
                table: "MovimientosInventario",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Citas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroCita = table.Column<string>(type: "text", nullable: true),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehiculoId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaHoraProgramada = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DuracionMinutos = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: false),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    MotivoCancelacion = table.Column<string>(type: "text", nullable: true),
                    OrdenServicioId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Citas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Citas_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Citas_OrdenesServicio_OrdenServicioId",
                        column: x => x.OrdenServicioId,
                        principalTable: "OrdenesServicio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Citas_Vehiculos_VehiculoId",
                        column: x => x.VehiculoId,
                        principalTable: "Vehiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PedidosLima",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroPedido = table.Column<string>(type: "text", nullable: true),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    EmpresaTransporte = table.Column<string>(type: "text", nullable: true),
                    NumeroGuia = table.Column<string>(type: "text", nullable: true),
                    FechaEstimadaLlegada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaLlegada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaEntrega = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubtotalGravado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SubtotalExonerado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SubtotalInafecto = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PorcentajeIgv = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MontoIgv = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    MotivoCancelacion = table.Column<string>(type: "text", nullable: true),
                    StockDeducido = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosLima", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidosLima_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialEstadosCita",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CitaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstadoAnterior = table.Column<int>(type: "integer", nullable: true),
                    EstadoNuevo = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Observacion = table.Column<string>(type: "text", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadosCita", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosCita_Citas_CitaId",
                        column: x => x.CitaId,
                        principalTable: "Citas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosCita_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DetallesPedidoLima",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PedidoLimaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CostoUnitarioHistorico = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TipoAfectacionIgv = table.Column<int>(type: "integer", nullable: false),
                    SubtotalGravado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PorcentajeIgvAplicado = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MontoIgv = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesPedidoLima", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesPedidoLima_PedidosLima_PedidoLimaId",
                        column: x => x.PedidoLimaId,
                        principalTable: "PedidosLima",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesPedidoLima_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialEstadosPedidoLima",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PedidoLimaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstadoAnterior = table.Column<int>(type: "integer", nullable: true),
                    EstadoNuevo = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Observacion = table.Column<string>(type: "text", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadosPedidoLima", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosPedidoLima_PedidosLima_PedidoLimaId",
                        column: x => x.PedidoLimaId,
                        principalTable: "PedidosLima",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosPedidoLima_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Citas_ClienteId",
                table: "Citas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Citas_Estado",
                table: "Citas",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Citas_FechaHoraProgramada",
                table: "Citas",
                column: "FechaHoraProgramada");

            migrationBuilder.CreateIndex(
                name: "IX_Citas_NumeroCita",
                table: "Citas",
                column: "NumeroCita",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Citas_OrdenServicioId",
                table: "Citas",
                column: "OrdenServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_Citas_VehiculoId",
                table: "Citas",
                column: "VehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedidoLima_PedidoLimaId",
                table: "DetallesPedidoLima",
                column: "PedidoLimaId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedidoLima_ProductoId",
                table: "DetallesPedidoLima",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosCita_CitaId",
                table: "HistorialEstadosCita",
                column: "CitaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosCita_Fecha",
                table: "HistorialEstadosCita",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosCita_UsuarioId",
                table: "HistorialEstadosCita",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosPedidoLima_Fecha",
                table: "HistorialEstadosPedidoLima",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosPedidoLima_PedidoLimaId",
                table: "HistorialEstadosPedidoLima",
                column: "PedidoLimaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosPedidoLima_UsuarioId",
                table: "HistorialEstadosPedidoLima",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosLima_ClienteId",
                table: "PedidosLima",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosLima_Estado",
                table: "PedidosLima",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosLima_Fecha",
                table: "PedidosLima",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosLima_NumeroGuia",
                table: "PedidosLima",
                column: "NumeroGuia");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosLima_NumeroPedido",
                table: "PedidosLima",
                column: "NumeroPedido",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesPedidoLima");

            migrationBuilder.DropTable(
                name: "HistorialEstadosCita");

            migrationBuilder.DropTable(
                name: "HistorialEstadosPedidoLima");

            migrationBuilder.DropTable(
                name: "Citas");

            migrationBuilder.DropTable(
                name: "PedidosLima");

            migrationBuilder.DropColumn(
                name: "PedidoLimaId",
                table: "MovimientosInventario");

            migrationBuilder.DropSequence(
                name: "CitaNumeroSeq");

            migrationBuilder.DropSequence(
                name: "PedidoLimaNumeroSeq");
        }
    }
}
