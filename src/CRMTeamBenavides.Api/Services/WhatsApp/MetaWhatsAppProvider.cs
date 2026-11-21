using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CRMTeamBenavides.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

/// <summary>
/// Implementación oficial para Meta WhatsApp Business Cloud API (<c>POST /{version}/{phone-number-id}/messages</c>).
/// Permanece deshabilitada por defecto (<c>WhatsApp:Enabled = false</c>) y aplica política conservadora ante
/// resultados ambiguos (timeout, error de red, 500/502/504 o 2xx sin wamid) porque Meta no ofrece clave de
/// idempotencia del lado servidor para <c>POST /messages</c>.
/// </summary>
public sealed class MetaWhatsAppProvider : IWhatsAppProvider
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<WhatsAppSettings> _settingsMonitor;
    private readonly ILogger<MetaWhatsAppProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public MetaWhatsAppProvider(
        HttpClient httpClient,
        IOptionsMonitor<WhatsAppSettings> settingsMonitor,
        ILogger<MetaWhatsAppProvider> logger)
    {
        _httpClient = httpClient;
        _settingsMonitor = settingsMonitor;
        _logger = logger;
    }

    public string NombreProveedor => WhatsAppSettings.ProviderMeta;

    public async Task<WhatsAppSendResult> EnviarTextoAsync(
        WhatsAppSendTextRequest request,
        CancellationToken ct = default)
    {
        var settings = _settingsMonitor.CurrentValue;

        var errorConfig = settings.ValidarConfiguracionEnvio();
        if (errorConfig is not null)
        {
            return new WhatsAppSendResult(
                Exitoso: false,
                ExternalMessageId: null,
                CodigoHttp: null,
                Error: errorConfig,
                Clasificacion: TipoResultadoEnvioWhatsApp.ErrorDefinitivo,
                Proveedor: NombreProveedor);
        }

        if (!settings.UsaProveedorMeta)
        {
            return new WhatsAppSendResult(
                Exitoso: false,
                ExternalMessageId: null,
                CodigoHttp: null,
                Error: $"El proveedor configurado es '{settings.Provider}', no 'Meta'.",
                Clasificacion: TipoResultadoEnvioWhatsApp.ErrorDefinitivo,
                Proveedor: NombreProveedor);
        }

        var norm = WhatsAppPhoneNormalizer.NormalizarE164(request.DestinatarioE164, settings.DefaultCountryCode);
        if (!norm.EsValido || string.IsNullOrEmpty(norm.WhatsAppId))
        {
            return new WhatsAppSendResult(
                Exitoso: false,
                ExternalMessageId: null,
                CodigoHttp: 400,
                Error: norm.Error ?? "Número de destino inválido.",
                Clasificacion: TipoResultadoEnvioWhatsApp.ErrorDefinitivo,
                Proveedor: NombreProveedor);
        }

        if (string.IsNullOrWhiteSpace(request.Texto))
        {
            return new WhatsAppSendResult(
                Exitoso: false,
                ExternalMessageId: null,
                CodigoHttp: 400,
                Error: "El contenido de texto es obligatorio.",
                Clasificacion: TipoResultadoEnvioWhatsApp.ErrorDefinitivo,
                Proveedor: NombreProveedor);
        }

        var baseUrl = (settings.GraphApiBaseUrl ?? "https://graph.facebook.com").TrimEnd('/');
        var version = settings.GraphApiVersion.Trim().Trim('/');
        var phoneNumberId = Uri.EscapeDataString(settings.PhoneNumberId.Trim());
        var endpointUri = $"{baseUrl}/{version}/{phoneNumberId}/messages";

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = norm.WhatsAppId,
            type = "text",
            text = new
            {
                preview_url = false,
                body = request.Texto
            }
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken.Trim());

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, ct);
            var statusCode = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var externalId = ExtraerExternalMessageId(body);
                if (string.IsNullOrWhiteSpace(externalId))
                {
                    // Resultado ambiguo: Meta respondió 2xx pero sin wamid; no reintentar ciegamente.
                    _logger.LogWarning(
                        "Meta WhatsApp Cloud API respondió HTTP {StatusCode} sin identificador de mensaje (wamid). Marcado como ResultadoAmbiguo. IdempotencyKey={IdempotencyKey}",
                        statusCode,
                        request.IdempotencyKey);

                    return new WhatsAppSendResult(
                        Exitoso: false,
                        ExternalMessageId: null,
                        CodigoHttp: statusCode,
                        Error: "Meta respondió 2xx sin identificador de mensaje (wamid). Resultado ambiguo: no se reintenta automáticamente para evitar duplicados.",
                        Clasificacion: TipoResultadoEnvioWhatsApp.ResultadoAmbiguo,
                        Proveedor: NombreProveedor);
                }

                _logger.LogInformation(
                    "Meta WhatsApp Cloud API envió mensaje correctamente. IdempotencyKey={IdempotencyKey}, ExternalId={ExternalId}",
                    request.IdempotencyKey,
                    externalId);

                return new WhatsAppSendResult(
                    Exitoso: true,
                    ExternalMessageId: externalId,
                    CodigoHttp: statusCode,
                    Error: null,
                    Clasificacion: TipoResultadoEnvioWhatsApp.Exitoso,
                    Proveedor: NombreProveedor);
            }

            var retryAfterSegundos = ExtraerRetryAfterSegundos(response);
            var clasificacion = ClasificarErrorHttp(response.StatusCode);
            var errorDetalle = ExtraerErrorMetaSeguro(body, statusCode, settings.AccessToken, settings.AppSecret);

            _logger.LogWarning(
                "Meta WhatsApp Cloud API no aceptó el envío. HTTP={StatusCode}, Clasificacion={Clasificacion}, IdempotencyKey={IdempotencyKey}, Detalle={Detalle}",
                statusCode,
                clasificacion,
                request.IdempotencyKey,
                errorDetalle);

            return new WhatsAppSendResult(
                Exitoso: false,
                ExternalMessageId: null,
                CodigoHttp: statusCode,
                Error: errorDetalle,
                Clasificacion: clasificacion,
                Proveedor: NombreProveedor,
                RetryAfterSegundos: retryAfterSegundos);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout HTTP: Meta pudo haber procesado y enviado el mensaje antes de que venciera el socket.
            // Política conservadora: ResultadoAmbiguo (jamás reintentar ciegamente).
            _logger.LogWarning(
                "Timeout al contactar Meta WhatsApp Cloud API (ResultadoAmbiguo; sin reintento automático). IdempotencyKey={IdempotencyKey}",
                request.IdempotencyKey);

            return new WhatsAppSendResult(
                Exitoso: false,
                ExternalMessageId: null,
                CodigoHttp: null,
                Error: "Timeout al esperar respuesta de Meta WhatsApp Cloud API. Resultado ambiguo: el mensaje pudo haberse entregado; no se reintenta automáticamente.",
                Clasificacion: TipoResultadoEnvioWhatsApp.ResultadoAmbiguo,
                Proveedor: NombreProveedor);
        }
        catch (HttpRequestException ex)
        {
            // Error de red: puede ocurrir tras enviar el POST antes de recibir respuesta.
            // Política conservadora: ResultadoAmbiguo para impedir duplicados silenciosos.
            var errorSeguro = WhatsAppWebhookSecurity.SanitizarMensajeError(
                $"Error de red al contactar Meta WhatsApp Cloud API (resultado ambiguo): {ex.Message}",
                settings.AccessToken,
                settings.AppSecret);

            _logger.LogWarning(
                "Error de red en Meta WhatsApp Cloud API (ResultadoAmbiguo; sin reintento automático). IdempotencyKey={IdempotencyKey}, Detalle={Detalle}",
                request.IdempotencyKey,
                errorSeguro);

            return new WhatsAppSendResult(
                Exitoso: false,
                ExternalMessageId: null,
                CodigoHttp: null,
                Error: errorSeguro,
                Clasificacion: TipoResultadoEnvioWhatsApp.ResultadoAmbiguo,
                Proveedor: NombreProveedor);
        }
    }

    /// <summary>
    /// Solo HTTP 429 (Rate Limit) y HTTP 503 (Service Unavailable) representan rechazos previos al despacho
    /// seguros de reintentar. HTTP 500/502/504 se tratan como ambiguos para evitar duplicados silenciosos.
    /// </summary>
    internal static TipoResultadoEnvioWhatsApp ClasificarErrorHttp(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.TooManyRequests => TipoResultadoEnvioWhatsApp.ErrorReintentableSeguro,
        HttpStatusCode.ServiceUnavailable => TipoResultadoEnvioWhatsApp.ErrorReintentableSeguro,
        HttpStatusCode.InternalServerError => TipoResultadoEnvioWhatsApp.ResultadoAmbiguo,
        HttpStatusCode.BadGateway => TipoResultadoEnvioWhatsApp.ResultadoAmbiguo,
        HttpStatusCode.GatewayTimeout => TipoResultadoEnvioWhatsApp.ResultadoAmbiguo,
        _ when (int)statusCode >= 500 => TipoResultadoEnvioWhatsApp.ResultadoAmbiguo,
        _ => TipoResultadoEnvioWhatsApp.ErrorDefinitivo
    };

    private static int? ExtraerRetryAfterSegundos(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null) return null;

        if (retryAfter.Delta.HasValue)
        {
            return Math.Clamp((int)Math.Ceiling(retryAfter.Delta.Value.TotalSeconds), 5, 3600);
        }

        if (retryAfter.Date.HasValue)
        {
            var diff = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            return Math.Clamp((int)Math.Ceiling(diff.TotalSeconds), 5, 3600);
        }

        return null;
    }

    private static string? ExtraerExternalMessageId(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0)
            {
                var first = messages[0];
                if (first.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                {
                    return idProp.GetString()?.Trim();
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string ExtraerErrorMetaSeguro(string? body, int statusCode, params string?[] secretos)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
                {
                    var code = err.TryGetProperty("code", out var c) ? c.ToString() : "?";
                    var type = err.TryGetProperty("type", out var t) ? t.GetString() : "MetaError";
                    var msg = err.TryGetProperty("message", out var m) ? m.GetString() : "Error de API";
                    return WhatsAppWebhookSecurity.SanitizarMensajeError(
                        $"Meta HTTP {statusCode} [{type}/{code}]: {msg}",
                        secretos);
                }
            }
            catch (JsonException)
            {
                // Fallback a resumen HTTP
            }
        }

        return WhatsAppWebhookSecurity.SanitizarMensajeError(
            $"Meta WhatsApp Cloud API respondió HTTP {statusCode}.",
            secretos);
    }
}
