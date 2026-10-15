using CRMTeamBenavides.Api.Features.Yamaha;

namespace CRMTeamBenavides.Api.Services.Yamaha.Exportadores;

/// <summary>
/// Extractor de datos para el Servicio 2 — Stock de Yamaha v3.
/// Extrae el catálogo activo de repuestos desde PostgreSQL y los proyecta a YamahaStockV3Dto.
/// </summary>
public interface IYamahaStockExporter
{
    /// <summary>
    /// Extrae la foto completa de stock activo de repuestos.
    /// </summary>
    /// <param name="soloMarcaYamaha">Si es true, filtra únicamente productos con Marca == "YAMAHA" (aislando D-07).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<YamahaExportacionResultado<YamahaStockV3Dto>> ExportarStockAsync(
        bool soloMarcaYamaha = false,
        CancellationToken cancellationToken = default);
}
