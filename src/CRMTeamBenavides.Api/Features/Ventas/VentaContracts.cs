namespace CRMTeamBenavides.Api.Features.Ventas;

public record CreateVentaRequest(
    Guid ClienteId,
    Guid? OrdenServicioId,
    List<CreateDetalleVentaRequest> Detalles,
    bool EsCotizacion);

public record CreateDetalleVentaRequest(
    Guid ProductoId,
    int Cantidad);

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
    bool Activo);

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
    bool Activo);

public record DetalleVentaResponse(
    Guid Id,
    Guid ProductoId,
    string ProductoCodigo,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal);
