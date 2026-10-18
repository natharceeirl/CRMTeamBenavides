using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Implementación HTTP del cliente Yamaha API v3.
///
/// Maneja: timeout, cancelación, errores HTTP, respuestas no-2xx, reintentos transitorios
/// y redacción obligatoria de secretos (<c>?code={Function_Key}</c>) antes de escribir en <see cref="ILogger"/>.
/// </summary>
public sealed class YamahaApiClient : IYamahaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YamahaApiClient> _logger;
    private readonly YamahaSettings? _settings;
    private readonly IReadOnlyList<string> _secretosConfigurados;

    public YamahaApiClient(
        HttpClient httpClient,
        ILogger<YamahaApiClient> logger,
        IOptions<YamahaSettings>? settings = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings?.Value;
        _secretosConfigurados = _settings?.ObtenerSecretosConfigurados() ?? Array.Empty<string>();
    }

    /// <inheritdoc />
    public Task<bool> VerificarConectividadAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "El endpoint de verificación de conectividad con Yamaha API aún no ha sido " +
            "definido. Esta operación debe implementarse cuando se reciba la documentación " +
            "oficial, credenciales y permisos de Yamaha.");
    }

    /// <inheritdoc />
    public Task<YamahaHttpRespuestaDto> EnviarVentasAsync(
        IReadOnlyList<YamahaVentaV3Dto> registros,
        CancellationToken cancellationToken = default)
    {
        VerificarIntegracionHabilitada();
        var urlBase = _settings?.VentasEndpointUrl;
        var functionKey = _settings?.ObtenerFunctionKeyVentas();
        var targetUri = ConstruirUriConClave(urlBase, functionKey, "ReceiveRetailVentas");
        var json = YamahaJsonSerializer.Serializar(registros, indented: false);

        return DespacharPayloadAsync(targetUri, json, "ReceiveRetailVentas", cancellationToken);
    }

    /// <inheritdoc />
    public Task<YamahaHttpRespuestaDto> EnviarStockAsync(
        IReadOnlyList<YamahaStockV3Dto> registros,
        CancellationToken cancellationToken = default)
    {
        VerificarIntegracionHabilitada();
        var urlBase = _settings?.StockEndpointUrl;
        var functionKey = _settings?.ObtenerFunctionKeyStock();
        var targetUri = ConstruirUriConClave(urlBase, functionKey, "ReceiveRetailStock");
        var json = YamahaJsonSerializer.Serializar(registros, indented: false);

        return DespacharPayloadAsync(targetUri, json, "ReceiveRetailStock", cancellationToken);
    }

    /// <inheritdoc />
    public Task<YamahaHttpRespuestaDto> EnviarServicioTecnicoAsync(
        IReadOnlyList<YamahaServicioTecnicoV3Dto> registros,
        CancellationToken cancellationToken = default)
    {
        VerificarIntegracionHabilitada();
        var urlBase = _settings?.ServicioTecnicoEndpointUrl;
        var functionKey = _settings?.ObtenerFunctionKeyServicioTecnico();
        var targetUri = ConstruirUriConClave(urlBase, functionKey, "ReceiveRetailServicioTecnico");
        var json = YamahaJsonSerializer.Serializar(registros, indented: false);

        return DespacharPayloadAsync(targetUri, json, "ReceiveRetailServicioTecnico", cancellationToken);
    }

    private void VerificarIntegracionHabilitada()
    {
        if (_settings?.Enabled != true)
        {
            throw new InvalidOperationException(
                "La integración saliente con Yamaha está desactivada en la configuración (YamahaSettings.Enabled = false). " +
                "No se autoriza ningún envío HTTP real a los servidores de Yamaha.");
        }
    }

    private static Uri ConstruirUriConClave(string? urlBase, string? functionKey, string nombreServicio)
    {
        if (string.IsNullOrWhiteSpace(urlBase))
        {
            throw new InvalidOperationException(
                $"La URL del endpoint '{nombreServicio}' no está configurada en YamahaSettings.");
        }

        var uriBuilder = new UriBuilder(urlBase.Trim());
        if (!string.IsNullOrWhiteSpace(functionKey))
        {
            var queryExistente = uriBuilder.Query.TrimStart('?');
            var paramCode = $"code={Uri.EscapeDataString(functionKey.Trim())}";
            uriBuilder.Query = string.IsNullOrEmpty(queryExistente)
                ? paramCode
                : $"{queryExistente}&{paramCode}";
        }

        return uriBuilder.Uri;
    }

    private async Task<YamahaHttpRespuestaDto> DespacharPayloadAsync(
        Uri targetUri,
        string jsonPayload,
        string nombreServicio,
        CancellationToken cancellationToken)
    {
        var uriSanitizada = YamahaSecurityHelper.SanitizarUri(targetUri, _secretosConfigurados);
        var stopwatch = Stopwatch.StartNew();

        // Los bytes del cuerpo se fijan una sola vez: el hash auditado es el de lo que realmente viaja
        // y los reintentos reenvían exactamente el mismo contenido.
        var cuerpo = YamahaJsonSerializer.ABytes(jsonPayload);
        var hashEnviado = YamahaJsonSerializer.CalcularHash(cuerpo);

        const int maxIntentos = 3;
        int intento = 0;

        while (true)
        {
            intento++;
            using var request = new HttpRequestMessage(HttpMethod.Post, targetUri)
            {
                Content = CrearContenidoJson(cuerpo)
            };

            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);
                var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var bodySanitizado = YamahaSecurityHelper.SanitizarTexto(rawBody, _secretosConfigurados);
                int statusCode = (int)response.StatusCode;

                // Errores transitorios que admiten reintento
                bool esErrorTransitorio = statusCode is 408 or 429 or 500 or 502 or 503 or 504;

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Yamaha API [{Servicio}] respondió status {StatusCode} (Intento {Intento}/{Max}). Body: {Body}",
                        nombreServicio,
                        statusCode,
                        intento,
                        maxIntentos,
                        bodySanitizado);

                    if (esErrorTransitorio && intento < maxIntentos)
                    {
                        var delayMs = intento * 500;
                        await Task.Delay(delayMs, cancellationToken);
                        continue;
                    }

                    return new YamahaHttpRespuestaDto(
                        statusCode,
                        EsExitoso: false,
                        bodySanitizado,
                        MensajeError: $"Yamaha API respondió código HTTP {statusCode}.",
                        stopwatch.Elapsed,
                        hashEnviado);
                }

                _logger.LogInformation(
                    "Yamaha API [{Servicio}] respondió exitosamente con status {StatusCode} en {Ms}ms.",
                    nombreServicio,
                    statusCode,
                    stopwatch.ElapsedMilliseconds);

                return new YamahaHttpRespuestaDto(
                    statusCode,
                    EsExitoso: true,
                    bodySanitizado,
                    MensajeError: null,
                    stopwatch.Elapsed,
                    hashEnviado);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout en petición a Yamaha API [{Servicio}] ({Uri}).", nombreServicio, uriSanitizada);
                if (intento < maxIntentos)
                {
                    await Task.Delay(intento * 500, cancellationToken);
                    continue;
                }

                return new YamahaHttpRespuestaDto(
                    408,
                    EsExitoso: false,
                    CuerpoRespuesta: null,
                    MensajeError: $"Timeout al conectar con Yamaha API ({uriSanitizada}).",
                    stopwatch.Elapsed,
                    hashEnviado);
            }
            catch (HttpRequestException ex)
            {
                var errorSanitizado = YamahaSecurityHelper.SanitizarTexto(ex.Message, _secretosConfigurados);
                _logger.LogError(
                    "Error de red hacia Yamaha API [{Servicio}] ({Uri}) intento {Intento}/{Max}. Detalle: {Error}",
                    nombreServicio,
                    uriSanitizada,
                    intento,
                    maxIntentos,
                    errorSanitizado);

                if (intento < maxIntentos)
                {
                    await Task.Delay(intento * 500, cancellationToken);
                    continue;
                }

                return new YamahaHttpRespuestaDto(
                    503,
                    EsExitoso: false,
                    CuerpoRespuesta: null,
                    MensajeError: $"Error de red al conectar con Yamaha API: {errorSanitizado}",
                    stopwatch.Elapsed,
                    hashEnviado);
            }
        }
    }

    /// <summary>Mismo encabezado que producía <c>StringContent(json, UTF8, "application/json")</c>.</summary>
    private static ByteArrayContent CrearContenidoJson(byte[] cuerpo)
    {
        var contenido = new ByteArrayContent(cuerpo);
        contenido.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        return contenido;
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
