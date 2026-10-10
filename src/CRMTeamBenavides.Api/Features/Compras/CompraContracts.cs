using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Compras;

// --- Proveedores ---

public record GuardarProveedorRequest(
    TipoDocumentoCliente TipoDocumento,
    string NumeroDocumento,
    string RazonSocial,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Contacto);

public record ProveedorResponse(
    Guid Id,
    TipoDocumentoCliente TipoDocumento,
    string NumeroDocumento,
    string RazonSocial,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Contacto,
    DateTime FechaCreacion);

// --- Compras ---

/// <summary>Un repuesto del catálogo (ProductoId) o un concepto libre (Descripcion), como un flete.</summary>
public record DetalleCompraRequest(
    Guid? ProductoId,
    string? Descripcion,
    int Cantidad,
    decimal PrecioUnitario,
    TipoAfectacionIgv TipoAfectacionIgv);

/// <summary>Monto en la moneda de la compra.</summary>
public record RegistrarPagoCompraRequest(
    decimal Monto,
    Guid MetodoPagoId,
    string? Referencia,
    string? Observaciones);

public record CrearCompraRequest(
    Guid ProveedorId,
    TipoComprobanteCompra TipoComprobante,
    string Serie,
    string Numero,
    DateOnly FechaEmision,
    DateOnly? FechaVencimiento,
    MonedaCompra Moneda,
    decimal? TipoCambio,
    decimal? PorcentajeIgv,
    bool PreciosIncluyenIgv,
    Guid? PedidoLimaId,
    string? GuiaRemision,
    string? Observaciones,
    List<DetalleCompraRequest> Detalles,
    List<RegistrarPagoCompraRequest>? Pagos);

/// <summary>
/// Solo los datos del comprobante. Líneas, moneda, tipo de cambio, IGV y tipo de
/// comprobante (define si el IGV es costo) ya movieron stock y costo: para
/// cambiarlos se anula la compra.
/// </summary>
public record ActualizarCompraRequest(
    Guid ProveedorId,
    string Serie,
    string Numero,
    DateOnly FechaEmision,
    DateOnly? FechaVencimiento,
    string? GuiaRemision,
    string? Observaciones);

public record AnularRequest(string Motivo);

/// <summary>Estado de pago de una compra registrada; nulo si está anulada.</summary>
public static class EstadosPagoCompra
{
    public const string Pendiente = "Pendiente";
    public const string Parcial = "Parcial";
    public const string Pagada = "Pagada";

    /// <summary>Filtros de la lista: con saldo (pendiente o parcial) y vencidas.</summary>
    public const string PorPagar = "PorPagar";
    public const string Vencida = "Vencida";
}

public record CompraResumenResponse(
    Guid Id,
    string NumeroCompra,
    Guid ProveedorId,
    string ProveedorNombre,
    string ProveedorDocumento,
    TipoComprobanteCompra TipoComprobante,
    string Serie,
    string Numero,
    DateOnly FechaEmision,
    DateOnly? FechaVencimiento,
    MonedaCompra Moneda,
    decimal TipoCambio,
    decimal Total,
    decimal TotalSoles,
    decimal TotalPagado,
    decimal Saldo,
    string? EstadoPago,
    bool Vencida,
    EstadoCompra Estado,
    Guid? PedidoLimaId,
    string? NumeroPedidoLima,
    int CantidadLineas,
    DateTime FechaCreacion);

public record DetalleCompraResponse(
    Guid Id,
    Guid? ProductoId,
    string? ProductoCodigo,
    string Descripcion,
    int Cantidad,
    decimal PrecioUnitario,
    TipoAfectacionIgv TipoAfectacionIgv,
    decimal Subtotal,
    decimal MontoIgv,
    decimal Total,
    decimal CostoUnitarioSoles,
    bool MueveStock);

public record PagoCompraResponse(
    Guid Id,
    decimal Monto,
    decimal MontoSoles,
    Guid MetodoPagoId,
    string MetodoPagoNombre,
    string MetodoPagoCodigo,
    DateTime Fecha,
    string? Referencia,
    string? Observaciones,
    string? UsuarioNombre,
    bool SalioDeCaja,
    bool Anulado,
    DateTime? FechaAnulacion,
    string? MotivoAnulacion,
    string? UsuarioAnulacionNombre);

public record CompraResponse(
    Guid Id,
    string NumeroCompra,
    Guid ProveedorId,
    string ProveedorNombre,
    string ProveedorDocumento,
    TipoComprobanteCompra TipoComprobante,
    string Serie,
    string Numero,
    DateOnly FechaEmision,
    DateOnly? FechaVencimiento,
    MonedaCompra Moneda,
    decimal TipoCambio,
    decimal PorcentajeIgv,
    bool PreciosIncluyenIgv,
    decimal SubtotalGravado,
    decimal SubtotalExonerado,
    decimal SubtotalInafecto,
    decimal MontoIgv,
    decimal Total,
    decimal TotalSoles,
    decimal TotalPagado,
    decimal Saldo,
    string? EstadoPago,
    bool Vencida,
    EstadoCompra Estado,
    Guid? PedidoLimaId,
    string? NumeroPedidoLima,
    string? GuiaRemision,
    string? Observaciones,
    string? UsuarioNombre,
    DateTime FechaCreacion,
    DateTime? FechaAnulacion,
    string? MotivoAnulacion,
    string? UsuarioAnulacionNombre,
    List<DetalleCompraResponse> Detalles,
    List<PagoCompraResponse> Pagos);
