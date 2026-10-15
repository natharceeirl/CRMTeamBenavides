using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Resultado integral de una ejecución de sincronización orquestada con Yamaha v3.
/// </summary>
public sealed record YamahaSyncEjecucionResultado(
    Guid SyncLogId,
    YamahaServicioSincronizacion Servicio,
    bool Exitoso,
    bool EsSimulacion,
    int TotalLeidos,
    int TotalValidos,
    int TotalRechazados,
    string? PayloadHash,
    int? CodigoHttp,
    string? Mensaje,
    IReadOnlyList<string> ErroresOAdvertencias,
    TimeSpan Duracion
);

/// <summary>
/// Orquestador central de sincronización y auditoría para Yamaha Motor del Perú v3.
/// Coordina: extracción PostgreSQL, mapeo, validación de contrato, hash SHA-256,
/// auditoría append-only en YamahaSyncLog y despacho condicional.
/// </summary>
public interface IYamahaSyncOrchestrator
{
    /// <summary>
    /// Ejecuta un ciclo de sincronización completo para el servicio indicado.
    /// </summary>
    /// <param name="servicio">Ventas, Stock o ServicioTecnico.</param>
    /// <param name="esSimulacion">True para simular en memoria/dry-run sin invocar HTTP real.</param>
    /// <param name="modoEnvio">"Manual", "Programado", "DryRun", etc.</param>
    /// <param name="usuarioId">Identificador del usuario que disparó la acción (si fue manual).</param>
    /// <param name="rangoDesdeUtc">Punto temporal inicial (si es nulo, usa el watermark persistido).</param>
    /// <param name="rangoHastaUtc">Punto temporal final (si es nulo, usa DateTime.UtcNow).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<YamahaSyncEjecucionResultado> SincronizarServicioAsync(
        YamahaServicioSincronizacion servicio,
        bool esSimulacion = false,
        string modoEnvio = "Manual",
        Guid? usuarioId = null,
        DateTime? rangoDesdeUtc = null,
        DateTime? rangoHastaUtc = null,
        CancellationToken cancellationToken = default);
}
