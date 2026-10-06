using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.PedidosLima;

public record CrearDetallePedidoLimaRequest(
    [Required] Guid ProductoId,
    [Required] [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")] int Cantidad,
    decimal? PrecioUnitario,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TipoAfectacionIgv? TipoAfectacionIgv);

public record CrearPedidoLimaRequest(
    Guid? ClienteId,
    string? EmpresaTransporte,
    string? NumeroGuia,
    DateTime? FechaEstimadaLlegada,
    string? Observaciones,
    [Required] List<CrearDetallePedidoLimaRequest> Detalles);

public record ActualizarPedidoLimaRequest(
    string? EmpresaTransporte,
    string? NumeroGuia,
    DateTime? FechaEstimadaLlegada,
    DateTime? FechaLlegada,
    DateTime? FechaEntrega,
    string? Observaciones);

public record ActualizarPrecioDetallePedidoLimaRequest(
    [Required] [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser mayor o igual a 0.")] decimal PrecioUnitario,
    int? Cantidad = null);

public record CambiarEstadoPedidoLimaRequest(
    [Required] EstadoPedidoLima NuevoEstado,
    string? Observacion);

public record CancelarPedidoLimaRequest(
    [Required] string MotivoCancelacion);

public record DetallePedidoLimaResponse(
    Guid Id,
    Guid ProductoId,
    string ProductoCodigo,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal CostoUnitarioHistorico,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TipoAfectacionIgv TipoAfectacionIgv,
    string TipoAfectacionIgvDescripcion,
    decimal SubtotalGravado,
    decimal PorcentajeIgvAplicado,
    decimal MontoIgv,
    decimal Total);

public record HistorialEstadoPedidoLimaResponse(
    Guid Id,
    EstadoPedidoLima? EstadoAnterior,
    string? EstadoAnteriorDescripcion,
    EstadoPedidoLima EstadoNuevo,
    string EstadoNuevoDescripcion,
    Guid? UsuarioId,
    string? UsuarioNombre,
    DateTime Fecha,
    string? Observacion);

public record PedidoLimaResponse(
    Guid Id,
    string NumeroPedido,
    Guid ClienteId,
    string ClienteNombre,
    string? ClienteDocumento,
    string? ClienteTelefono,
    DateTime Fecha,
    EstadoPedidoLima Estado,
    string EstadoDescripcion,
    string? EmpresaTransporte,
    string? NumeroGuia,
    DateTime? FechaEstimadaLlegada,
    DateTime? FechaLlegada,
    DateTime? FechaEntrega,
    decimal SubtotalGravado,
    decimal SubtotalExonerado,
    decimal SubtotalInafecto,
    decimal PorcentajeIgv,
    decimal MontoIgv,
    decimal Total,
    string? Observaciones,
    string? MotivoCancelacion,
    bool StockDeducido,
    List<DetallePedidoLimaResponse> Detalles,
    List<HistorialEstadoPedidoLimaResponse> Historial,
    DateTime FechaCreacion,
    decimal TotalPagado = 0m,
    decimal Saldo = 0m,
    string EstadoPago = "Pendiente",
    int EstadoAprobacionGerenciaId = 0,
    string EstadoAprobacionGerencia = "NoAplica",
    List<CRMTeamBenavides.Api.Features.Ventas.PagoResponse>? Pagos = null);

public record AprobacionGerenciaPedidoLimaRequest(
    EstadoAprobacionGerencia Estado,
    string? Observaciones);
