namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Resultado del despacho HTTP hacia los endpoints Azure Functions de Yamaha v3.
/// </summary>
public sealed record YamahaHttpRespuestaDto(
    int CodigoHttp,
    bool EsExitoso,
    string? CuerpoRespuesta,
    string? MensajeError,
    TimeSpan Duracion
);

/// <summary>
/// Abstracción del cliente HTTP para los tres servicios de datos de Yamaha Motor del Perú v3
/// (ReceiveRetailVentas, ReceiveRetailStock y ReceiveRetailServicioTecnico).
/// </summary>
public interface IYamahaApiClient
{
    /// <summary>
    /// Verifica la conectividad básica con la API de Yamaha.
    /// </summary>
    Task<bool> VerificarConectividadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía un lote de ventas hacia el endpoint ReceiveRetailVentas de Yamaha v3.
    /// </summary>
    Task<YamahaHttpRespuestaDto> EnviarVentasAsync(
        IReadOnlyList<YamahaVentaV3Dto> registros,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía un lote de inventario diario hacia el endpoint ReceiveRetailStock de Yamaha v3.
    /// </summary>
    Task<YamahaHttpRespuestaDto> EnviarStockAsync(
        IReadOnlyList<YamahaStockV3Dto> registros,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía un lote de órdenes de taller hacia el endpoint ReceiveRetailServicioTecnico de Yamaha v3.
    /// </summary>
    Task<YamahaHttpRespuestaDto> EnviarServicioTecnicoAsync(
        IReadOnlyList<YamahaServicioTecnicoV3Dto> registros,
        CancellationToken cancellationToken = default);
}
