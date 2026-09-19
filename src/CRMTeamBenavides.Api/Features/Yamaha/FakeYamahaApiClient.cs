namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Implementación fake (stub) de <see cref="IYamahaApiClient"/> para uso en pruebas.
///
/// Permite testear todo el código que depende de <see cref="IYamahaApiClient"/>
/// sin requerir acceso real a la API de Yamaha.
///
/// Comportamiento por defecto:
///   - <see cref="VerificarConectividadAsync"/>: retorna <c>true</c> (simulando éxito).
///
/// Las pruebas pueden asignar <see cref="ResultadoConectividad"/> para simular éxito o fallo,
/// y <see cref="ExcepcionSimulada"/> para simular errores de red, timeouts u otras excepciones.
/// </summary>
public sealed class FakeYamahaApiClient : IYamahaApiClient
{
    /// <summary>
    /// Resultado que devolverá <see cref="VerificarConectividadAsync"/>.
    /// Las pruebas pueden cambiar este valor antes de invocar el método.
    /// </summary>
    public bool ResultadoConectividad { get; set; } = true;

    /// <summary>
    /// Si se asigna, <see cref="VerificarConectividadAsync"/> lanzará esta excepción.
    /// Útil para simular errores de red o timeouts en pruebas.
    /// </summary>
    public Exception? ExcepcionSimulada { get; set; }

    /// <inheritdoc />
    public Task<bool> VerificarConectividadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ExcepcionSimulada is not null)
            throw ExcepcionSimulada;

        return Task.FromResult(ResultadoConectividad);
    }
}
