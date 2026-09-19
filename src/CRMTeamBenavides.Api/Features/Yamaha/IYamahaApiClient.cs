namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Abstracción del cliente HTTP para la API de Yamaha.
///
/// Esta interfaz define únicamente la superficie mínima necesaria para validar
/// la infraestructura del conector (configuración, DI, error handling, cancelación).
///
/// Las operaciones de negocio reales (consulta de repuestos, precios, vehículos,
/// stock, etc.) deben agregarse ÚNICAMENTE cuando el cliente entregue:
///   - Documentación oficial de la API de Yamaha.
///   - Credenciales y permisos activos.
///   - Contratos de request/response verificados.
///
/// NO agregar métodos a esta interfaz basándose en suposiciones.
/// </summary>
public interface IYamahaApiClient
{
    /// <summary>
    /// Verifica la conectividad básica con la API de Yamaha.
    /// El comportamiento exacto (endpoint, response) depende de la documentación real.
    /// En ausencia de documentación, los implementadores deben lanzar
    /// <see cref="NotSupportedException"/> o retornar un resultado indicativo.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <c>true</c> si la API respondió con éxito; <c>false</c> si respondió
    /// con error HTTP no crítico. Lanza excepción ante error de red o timeout.
    /// </returns>
    Task<bool> VerificarConectividadAsync(CancellationToken cancellationToken = default);
}
