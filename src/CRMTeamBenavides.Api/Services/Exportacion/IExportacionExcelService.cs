namespace CRMTeamBenavides.Api.Services.Exportacion;

public record CitaExcelDto(
    string NumeroCita,
    DateTime FechaHoraProgramada,
    string ClienteNombre,
    string ClienteDocumento,
    string VehiculoInfo,
    string Placa,
    string Motivo,
    string Estado,
    int DuracionMinutos,
    string? Observaciones);

public record PedidoLimaExcelDto(
    string NumeroPedido,
    DateTime Fecha,
    string ClienteNombre,
    string ClienteDocumento,
    string EmpresaTransporte,
    string NumeroGuia,
    int CantidadItems,
    decimal SubtotalGravado,
    decimal MontoIgv,
    decimal Total,
    string Estado,
    DateTime? FechaEstimadaLlegada,
    DateTime? FechaLlegada,
    DateTime? FechaEntrega,
    string? Observaciones);

public record VentaExcelDto(
    Guid Id,
    DateTime Fecha,
    string ClienteNombre,
    string Estado,
    string EstadoPago,
    string EstadoComprobante,
    int CantidadItems,
    decimal Total,
    decimal TotalPagado,
    decimal Saldo,
    decimal? CostoTotal = null,
    decimal? Utilidad = null,
    decimal? MargenPorcentaje = null);

public record OrdenServicioExcelDto(
    Guid Id,
    string? NumeroOrden,
    DateTime FechaIngreso,
    DateTime? FechaSalida,
    string ClienteNombre,
    string? Placa,
    string? Modelo,
    string Estado,
    string? TecnicoNombre,
    decimal Total,
    decimal? CostoTotal = null,
    decimal? Utilidad = null,
    decimal? MargenPorcentaje = null);

/// <summary>Total, pagado y saldo en la moneda de la compra; TotalSoles con su tipo de cambio.</summary>
public record CompraExcelDto(
    string NumeroCompra,
    DateOnly FechaEmision,
    string Comprobante,
    string ProveedorNombre,
    string ProveedorDocumento,
    string Moneda,
    decimal TipoCambio,
    decimal Total,
    decimal TotalSoles,
    decimal TotalPagado,
    decimal Saldo,
    string Estado,
    DateOnly? FechaVencimiento,
    string? NumeroPedidoLima);

public interface IExportacionExcelService
{
    byte[] GenerarExcelCompras(IEnumerable<CompraExcelDto> compras);
    byte[] GenerarExcelCitas(IEnumerable<CitaExcelDto> citas);
    byte[] GenerarExcelPedidosLima(IEnumerable<PedidoLimaExcelDto> pedidos);
    byte[] GenerarExcelVentas(IEnumerable<VentaExcelDto> ventas, bool incluirFinanciero = false);
    byte[] GenerarExcelOrdenesServicio(IEnumerable<OrdenServicioExcelDto> ordenes, bool incluirFinanciero = false);
}
