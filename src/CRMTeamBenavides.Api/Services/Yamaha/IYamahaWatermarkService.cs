using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Información de estado de la marca de agua (watermark) de un servicio Yamaha v3.
/// </summary>
public sealed record YamahaWatermarkEstadoDto(
    YamahaServicioSincronizacion Servicio,
    DateTime PuntoCorteUtc,
    bool EsCargaInicialTresMeses,
    DateTime? UltimaEjecucionExitosaUtc,
    Guid? UltimoSyncLogId
);

/// <summary>
/// Servicio de gestión de marcas de agua e incrementalidad temporal para Yamaha v3.
/// Garantiza que el watermark NUNCA avance ante fallos, simulaciones o respuestas rechazadas.
/// </summary>
public interface IYamahaWatermarkService
{
    /// <summary>
    /// Resuelve el punto temporal inicial (UTC) a partir del cual se deben extraer registros del CRM.
    /// Si existe una sincronización real y exitosa previa, toma su marca temporal; de lo contrario,
    /// retorna la ventana de 3 meses reglamentaria (Sección 3.1).
    /// </summary>
    Task<DateTime> ObtenerPuntoCorteDesdeUtcAsync(
        YamahaServicioSincronizacion servicio,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el diagnóstico actual de marcas de agua para los tres servicios de datos.
    /// </summary>
    Task<IReadOnlyDictionary<YamahaServicioSincronizacion, YamahaWatermarkEstadoDto>> ObtenerDiagnosticoWatermarksAsync(
        CancellationToken cancellationToken = default);
}
