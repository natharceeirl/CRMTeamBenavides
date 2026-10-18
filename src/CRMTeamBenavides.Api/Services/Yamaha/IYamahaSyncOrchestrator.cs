using System.Text.Json.Serialization;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Desenlace de una ejecución de sincronización. Separa los fallos reales de los casos que no son error
/// (otra ejecución en curso, lote sin registros), para que el scheduler no aplique enfriamientos
/// ni consuma turnos por algo que no falló.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum YamahaResultadoSincronizacion
{
    /// <summary>El lote se validó y se envió (o se simuló) con éxito.</summary>
    Exitoso = 1,

    /// <summary>No había registros que enviar: no se llamó a Yamaha (plan v3, sección 5.2).</summary>
    SinCambios = 2,

    /// <summary>Falló la validación de contrato, el envío HTTP o la orquestación.</summary>
    Fallido = 3,

    /// <summary>Otra sincronización del mismo servicio estaba en curso: no se ejecutó nada ni se creó log.</summary>
    OmitidoPorConcurrencia = 4
}

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
    TimeSpan Duracion,
    YamahaResultadoSincronizacion Resultado
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

    /// <summary>
    /// Recupera registros de sincronizacion que quedaron en estado 'EnProceso' debido a caidas abruptas
    /// del proceso o reinicios del servidor, marcandolos como 'Fallido' si superan el tiempo de expiracion
    /// configurado en <see cref="YamahaSettings.TiempoExpiracionEnProcesoMinutos"/>.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <returns>Total de registros recuperados y marcados como fallidos.</returns>
    Task<int> RecuperarEjecucionesAbandonadasAsync(CancellationToken cancellationToken = default);
}
