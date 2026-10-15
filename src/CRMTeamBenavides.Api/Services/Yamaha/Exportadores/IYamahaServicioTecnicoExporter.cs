using CRMTeamBenavides.Api.Features.Yamaha;

namespace CRMTeamBenavides.Api.Services.Yamaha.Exportadores;

/// <summary>
/// Extractor de datos para el Servicio 3 — Servicio Técnico de Yamaha v3.
/// Extrae órdenes de trabajo facturadas desde PostgreSQL y las proyecta a YamahaServicioTecnicoV3Dto.
/// </summary>
public interface IYamahaServicioTecnicoExporter
{
    /// <summary>
    /// Extrae órdenes de servicio facturadas en un rango temporal UTC especificado.
    /// </summary>
    /// <param name="desdeUtc">Marca temporal inicial en UTC (si es nulo, no tiene límite inferior).</param>
    /// <param name="hastaUtc">Marca temporal final en UTC (si es nulo, toma DateTime.UtcNow).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<YamahaExportacionResultado<YamahaServicioTecnicoV3Dto>> ExportarServicioTecnicoAsync(
        DateTime? desdeUtc = null,
        DateTime? hastaUtc = null,
        CancellationToken cancellationToken = default);
}
