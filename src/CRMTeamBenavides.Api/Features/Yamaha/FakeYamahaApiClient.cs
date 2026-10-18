namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Implementación fake (stub) de <see cref="IYamahaApiClient"/> para uso en pruebas.
///
/// Permite testear todo el código que depende de <see cref="IYamahaApiClient"/>
/// sin requerir acceso real a la API de Yamaha.
/// </summary>
public sealed class FakeYamahaApiClient : IYamahaApiClient
{
    /// <summary>
    /// Resultado que devolverá <see cref="VerificarConectividadAsync"/>.
    /// </summary>
    public bool ResultadoConectividad { get; set; } = true;

    /// <summary>
    /// Si se asigna, las operaciones lanzarán esta excepción.
    /// </summary>
    public Exception? ExcepcionSimulada { get; set; }

    /// <summary>
    /// Respuesta HTTP predeterminada a devolver en los envíos.
    /// </summary>
    public YamahaHttpRespuestaDto RespuestaSimulada { get; set; } =
        new(200, true, "{\"status\":\"ok\",\"mensaje\":\"Lote recibido exitosamente por el simulador\"}", null, TimeSpan.FromMilliseconds(15));

    /// <summary>Último lote de ventas recibido en pruebas.</summary>
    public IReadOnlyList<YamahaVentaV3Dto>? UltimoLoteVentas { get; private set; }

    /// <summary>Último lote de stock recibido en pruebas.</summary>
    public IReadOnlyList<YamahaStockV3Dto>? UltimoLoteStock { get; private set; }

    /// <summary>Último lote de servicio técnico recibido en pruebas.</summary>
    public IReadOnlyList<YamahaServicioTecnicoV3Dto>? UltimoLoteServicioTecnico { get; private set; }

    /// <summary>Contador de llamadas totales a métodos de envío.</summary>
    public int TotalLlamadasEnvio { get; private set; }

    /// <inheritdoc />
    public Task<bool> VerificarConectividadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ExcepcionSimulada is not null)
            throw ExcepcionSimulada;

        return Task.FromResult(ResultadoConectividad);
    }

    /// <inheritdoc />
    public Task<YamahaHttpRespuestaDto> EnviarVentasAsync(
        IReadOnlyList<YamahaVentaV3Dto> registros,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ExcepcionSimulada is not null)
            throw ExcepcionSimulada;

        UltimoLoteVentas = registros;
        TotalLlamadasEnvio++;
        return Task.FromResult(ConHashEnviado(YamahaJsonSerializer.Serializar(registros)));
    }

    /// <inheritdoc />
    public Task<YamahaHttpRespuestaDto> EnviarStockAsync(
        IReadOnlyList<YamahaStockV3Dto> registros,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ExcepcionSimulada is not null)
            throw ExcepcionSimulada;

        UltimoLoteStock = registros;
        TotalLlamadasEnvio++;
        return Task.FromResult(ConHashEnviado(YamahaJsonSerializer.Serializar(registros)));
    }

    /// <inheritdoc />
    public Task<YamahaHttpRespuestaDto> EnviarServicioTecnicoAsync(
        IReadOnlyList<YamahaServicioTecnicoV3Dto> registros,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ExcepcionSimulada is not null)
            throw ExcepcionSimulada;

        UltimoLoteServicioTecnico = registros;
        TotalLlamadasEnvio++;
        return Task.FromResult(ConHashEnviado(YamahaJsonSerializer.Serializar(registros)));
    }

    /// <summary>
    /// Igual que el cliente real, informa el hash del cuerpo que habría enviado,
    /// salvo que <see cref="RespuestaSimulada"/> ya traiga uno (para simular una deriva).
    /// </summary>
    private YamahaHttpRespuestaDto ConHashEnviado(string json) =>
        RespuestaSimulada with
        {
            PayloadHashEnviado = RespuestaSimulada.PayloadHashEnviado ?? YamahaJsonSerializer.CalcularHash(json)
        };

    public void LimpiarHistorial()
    {
        UltimoLoteVentas = null;
        UltimoLoteStock = null;
        UltimoLoteServicioTecnico = null;
        TotalLlamadasEnvio = 0;
        ExcepcionSimulada = null;
    }
}
