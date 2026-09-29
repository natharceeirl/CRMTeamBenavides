using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Ventas;

public record CreateVentaRequest(
    Guid ClienteId,
    Guid? OrdenServicioId = null,
    List<CreateDetalleVentaRequest>? Detalles = null,
    bool EsCotizacion = false);

public record CreateDetalleVentaRequest(
    Guid ProductoId,
    int Cantidad,
    decimal? PrecioUnitario = null,
    decimal? Descuento = null,
    TipoAfectacionIgv? TipoAfectacionIgv = null);

public record VentaResponse(
    Guid Id,
    Guid ClienteId,
    string ClienteNombre,
    Guid? OrdenServicioId,
    string Estado,
    int EstadoId,
    DateTime Fecha,
    decimal Total,
    int CantidadItems,
    bool Activo,
    decimal SubtotalGravado = 0m,
    decimal SubtotalExonerado = 0m,
    decimal SubtotalInafecto = 0m,
    decimal MontoIgv = 0m,
    decimal TotalPagado = 0m,
    decimal Saldo = 0m,
    string EstadoPago = "Pendiente");

public record VentaDetalleResponse(
    Guid Id,
    Guid ClienteId,
    string ClienteNombre,
    string? ClienteDocumento,
    string? ClienteTelefono,
    Guid? OrdenServicioId,
    string Estado,
    int EstadoId,
    DateTime Fecha,
    decimal Total,
    List<DetalleVentaResponse> Detalles,
    bool Activo,
    ComprobanteResponse? Comprobante = null,
    decimal SubtotalGravado = 0m,
    decimal SubtotalExonerado = 0m,
    decimal SubtotalInafecto = 0m,
    decimal MontoIgv = 0m,
    decimal TotalPagado = 0m,
    decimal Saldo = 0m,
    string EstadoPago = "Pendiente",
    List<PagoResponse>? Pagos = null);

public record DetalleVentaResponse(
    Guid Id,
    Guid? ProductoId,
    string? ProductoCodigo,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal,
    int TipoItem = 0,
    string? TipoItemNombre = "Repuesto",
    Guid? ServicioId = null,
    decimal CostoUnitarioHistorico = 0m,
    int TipoAfectacionIgv = 0,
    string? TipoAfectacionIgvNombre = "Gravado",
    decimal SubtotalGravado = 0m,
    decimal PorcentajeIgvAplicado = 18m,
    decimal MontoIgv = 0m,
    decimal Total = 0m);

public record ComprobanteResponse(
    Guid Id,
    Guid VentaId,
    string Tipo,
    string? Serie,
    string? Numero,
    string Estado,
    DateTime FechaCreacion,
    bool Activo,
    decimal SubtotalGravado = 0m,
    decimal SubtotalExonerado = 0m,
    decimal SubtotalInafecto = 0m,
    decimal PorcentajeIgv = 18m,
    decimal MontoIgv = 0m,
    decimal Total = 0m,
    string? MetodoPagoPrincipal = null,
    string? Observaciones = null,
    Guid? OrdenServicioId = null);

public record RegistrarComprobanteRequest(
    string Tipo,
    string? Serie = null,
    string? Numero = null,
    string? MetodoPagoPrincipal = null,
    string? Observaciones = null);

public record MetodoPagoResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    bool Activo);

public record RegistrarPagoRequest(
    decimal Monto,
    Guid MetodoPagoId,
    string? Referencia = null,
    bool EsAnticipo = false,
    string? Observaciones = null);

public record PagoResponse(
    Guid Id,
    decimal Monto,
    Guid MetodoPagoId,
    string MetodoPagoNombre,
    string MetodoPagoCodigo,
    DateTime Fecha,
    string? Referencia,
    bool EsAnticipo,
    Guid? VentaId,
    Guid? OrdenServicioId,
    Guid? UsuarioId,
    string? UsuarioNombre,
    string? Observaciones,
    bool Activo);
