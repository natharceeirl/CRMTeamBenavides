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

public interface IExportacionExcelService
{
    byte[] GenerarExcelCitas(IEnumerable<CitaExcelDto> citas);
    byte[] GenerarExcelPedidosLima(IEnumerable<PedidoLimaExcelDto> pedidos);
}
