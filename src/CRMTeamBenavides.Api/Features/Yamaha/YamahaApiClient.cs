using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Implementación HTTP del cliente Yamaha API.
///
/// Maneja: timeout, cancelación, errores HTTP, respuestas no-2xx.
/// La lógica de negocio real se agregará cuando el cliente provea
/// documentación, credenciales y contratos verificados.
/// </summary>
public sealed class YamahaApiClient : IYamahaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YamahaApiClient> _logger;

    public YamahaApiClient(HttpClient httpClient, ILogger<YamahaApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Endpoint de conectividad aún no definido — requiere documentación oficial de Yamaha.
    /// Esta implementación lanza <see cref="NotSupportedException"/> hasta que se obtenga
    /// el endpoint correcto.
    /// </remarks>
    public Task<bool> VerificarConectividadAsync(CancellationToken cancellationToken = default)
    {
        // PENDIENTE: reemplazar con el endpoint de healthcheck/ping real de Yamaha
        // cuando el cliente entregue la documentación.
        throw new NotSupportedException(
            "El endpoint de verificación de conectividad con Yamaha API aún no ha sido " +
            "definido. Esta operación debe implementarse cuando se reciba la documentación " +
            "oficial, credenciales y permisos de Yamaha.");
    }

    // -------------------------------------------------------------------------
    // Métodos de negocio futuros
    // -------------------------------------------------------------------------
    // Agregar aquí ÚNICAMENTE cuando el cliente entregue documentación, credenciales
    // y contratos verificados. Ejemplos de lo que NO debe asumirse:
    //   - Consulta de repuestos / precios / stock.
    //   - Consulta de vehículos / modelos / catálogo.
    //   - Cualquier operación CRUD sobre recursos de Yamaha.
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // Helpers internos de error handling
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ejecuta una petición HTTP con manejo centralizado de timeout, cancelación
    /// y respuestas no-2xx.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Yamaha API respondió con status {StatusCode} para {Method} {Uri}. Body: {Body}",
                    (int)response.StatusCode, request.Method, request.RequestUri, body);
            }

            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout al conectar con Yamaha API ({Uri}).", request.RequestUri);
            throw new TimeoutException($"La petición a Yamaha API superó el tiempo de espera configurado ({request.RequestUri}).");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Petición a Yamaha API cancelada ({Uri}).", request.RequestUri);
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de red al conectar con Yamaha API ({Uri}).", request.RequestUri);
            throw;
        }
    }
}
