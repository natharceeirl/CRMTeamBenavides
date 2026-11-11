using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Portal;

public record PortalResumenResponse(
    Guid ClienteId,
    string ClienteNombre,
    int CantidadUnidades,
    int CantidadOrdenesActivas,
    int CantidadPresupuestosPendientes,
    decimal SaldoPendienteTotal);

public record PortalComprobanteResponse(
    Guid Id,
    Guid VentaId,
    Guid? OrdenServicioId,
    string? NumeroOrden,
    string Tipo,
    string? Serie,
    string? Numero,
    DateTime Fecha,
    decimal SubtotalGravado,
    decimal SubtotalExonerado,
    decimal SubtotalInafecto,
    decimal PorcentajeIgv,
    decimal MontoIgv,
    decimal Total,
    string Estado,
    string? MetodoPagoPrincipal,
    string? Observaciones,
    // Op. gratuitas: lo que valen las líneas gratuitas, con IGV. No se suma al total.
    decimal SubtotalGratuito = 0m);

public record HistorialServicioItemResponse(
    Guid Id,
    string Descripcion,
    string? ServicioNombre,
    string? ProductoCodigo,
    TipoItemServicio TipoItem,
    string TipoItemNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Total,
    // Una línea gratuita se cobra S/ 0.00.
    bool EsGratuita = false);

public record AtencionServicioUnidadResponse(
    Guid OrdenServicioId,
    string? NumeroOrden,
    DateTime FechaIngreso,
    DateTime? FechaSalida,
    string Estado,
    int EstadoId,
    int? KilometrajeIngreso,
    decimal? HorasUsoIngreso,
    decimal? LecturaMedidorIngreso,
    string? TipoMedidor,
    string? MotivoFalla,
    string? Solucion,
    string? ObservacionesCliente,
    decimal Total,
    List<HistorialServicioItemResponse> Items);
