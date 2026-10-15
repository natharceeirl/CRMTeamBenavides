using CRMTeamBenavides.Api.Features.Yamaha;

namespace CRMTeamBenavides.Api.Services.Yamaha.Exportadores;

/// <summary>
/// Extractor de datos para el Servicio 1 — Ventas de Yamaha v3.
/// Extrae ventas confirmadas con comprobante emitido desde PostgreSQL y las proyecta a YamahaVentaV3Dto.
/// </summary>
public interface IYamahaVentasExporter
{
    /// <summary>
    /// Extrae ventas en un rango temporal UTC especificado.
    /// </summary>
    /// <param name="desdeUtc">Marca temporal inicial en UTC (si es nulo, no tiene límite inferior).</param>
    /// <param name="hastaUtc">Marca temporal final en UTC (si es nulo, toma DateTime.UtcNow).</param>
    /// <param name="soloVentasMostrador">
    /// Si es true, extrae exclusivamente ventas directas de mostrador (OrdenServicioId == null) para
    /// aislar la Decisión D-02 y evitar duplicidad con Servicio Técnico. Si es false, incluye también
    /// ventas derivadas de órdenes de servicio.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<YamahaExportacionResultado<YamahaVentaV3Dto>> ExportarVentasAsync(
        DateTime? desdeUtc = null,
        DateTime? hastaUtc = null,
        bool soloVentasMostrador = true,
        CancellationToken cancellationToken = default);
}
