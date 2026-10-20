using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Contrato para exclusión mutua distribuida entre múltiples procesos del backend para Yamaha v3.
/// Garantiza que dos instancias independientes no ejecuten concurrentemente la sincronización del mismo servicio.
/// </summary>
public interface IYamahaDistributedLock
{
    /// <summary>
    /// Intenta adquirir el bloqueo distribuido para el servicio especificado durante el tiempo indicado.
    /// Retorna un handle descartable (<see cref="IAsyncDisposable"/>) si se obtuvo el bloqueo,
    /// o <c>null</c> si el servicio ya está siendo sincronizado por otra instancia.
    /// </summary>
    Task<IAsyncDisposable?> IntentarAdquirirBloqueoAsync(
        YamahaServicioSincronizacion servicio,
        TimeSpan tiempoEspera,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Comprueba si el bloqueo para el servicio indicado está actualmente retenido por algún proceso.
    /// </summary>
    Task<bool> EstaBloqueadoAsync(
        YamahaServicioSincronizacion servicio,
        CancellationToken cancellationToken = default);
}
