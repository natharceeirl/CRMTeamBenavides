using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Implementación HTTP del cliente Yamaha API.
///
/// Maneja: timeout, cancelación, errores HTTP, respuestas no-2xx y redacción obligatoria
/// de secretos (<c>?code={Function_Key}</c>) antes de escribir en <see cref="ILogger"/>.
/// </summary>
public sealed class YamahaApiClient : IYamahaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YamahaApiClient> _logger;
    private readonly IReadOnlyList<string> _secretosConfigurados;

    public YamahaApiClient(
        HttpClient httpClient,
        ILogger<YamahaApiClient> logger,
        IOptions<YamahaSettings>? settings = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _secretosConfigurados = settings?.Value.ObtenerSecretosConfigurados() ?? Array.Empty<string>();
    }

    /// <inheritdoc />
    /// <remarks>
    /// La especificación v3 de Yamaha define únicamente los tres endpoints de recepción (POST);
    /// no existe un endpoint oficial de ping/healthcheck sin enviar datos.
    /// Esta implementación mantiene <see cref="NotSupportedException"/> hasta que se habilite
    /// el envío real autorizado.
    /// </remarks>
    public Task<bool> VerificarConectividadAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "El endpoint de verificación de conectividad con Yamaha API aún no ha sido " +
            "definido. Esta operación debe implementarse cuando se reciba la documentación " +
            "oficial, credenciales y permisos de Yamaha.");
    }

    /// <summary>
    /// Ejecuta una petición HTTP con manejo centralizado de timeout, cancelación,
    /// respuestas no-2xx y sanitización de credenciales en URLs y logs.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uriSanitizada = YamahaSecurityHelper.SanitizarUri(request.RequestUri, _secretosConfigurados);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var bodySanitizado = YamahaSecurityHelper.SanitizarTexto(body, _secretosConfigurados);

                _logger.LogWarning(
                    "Yamaha API respondió con status {StatusCode} para {Method} {Uri}. Body: {Body}",
                    (int)response.StatusCode,
                    request.Method,
                    uriSanitizada,
                    bodySanitizado);
            }

            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout al conectar con Yamaha API ({Uri}).", uriSanitizada);
            throw new TimeoutException($"La petición a Yamaha API superó el tiempo de espera configurado ({uriSanitizada}).");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Petición a Yamaha API cancelada ({Uri}).", uriSanitizada);
            throw;
        }
        catch (HttpRequestException ex)
        {
            var mensajeExcepcionSanitizado = YamahaSecurityHelper.SanitizarTexto(ex.Message, _secretosConfigurados);
            _logger.LogError(
                "Error de red al conectar con Yamaha API ({Uri}). Detalle: {ErrorMessage}",
                uriSanitizada,
                mensajeExcepcionSanitizado);
            throw;
        }
    }
}
