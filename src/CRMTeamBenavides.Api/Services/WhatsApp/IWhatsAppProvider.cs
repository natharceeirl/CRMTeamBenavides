namespace CRMTeamBenavides.Api.Services.WhatsApp;

/// <summary>
/// Clasificación estricta del resultado de un intento de envío hacia el proveedor de WhatsApp.
/// Permite diferenciar rechazos seguros reintentables de resultados ambiguos donde Meta podría
/// haber despachado el mensaje sin entregar la respuesta HTTP al CRM.
/// </summary>
public enum TipoResultadoEnvioWhatsApp
{
    Exitoso = 0,

    /// <summary>
    /// Rechazo explícito previo al despacho (ej. HTTP 429 Rate Limit o HTTP 503 Service Unavailable).
    /// Seguro de reintentar con backoff controlado.
    /// </summary>
    ErrorReintentableSeguro = 1,

    /// <summary>
    /// Resultado incierto (timeout HTTP, corte de red, HTTP 500/502/504 o HTTP 2xx sin wamid).
    /// NO debe reintentarse automáticamente porque Meta Cloud API no garantiza idempotencia en POST /messages.
    /// </summary>
    ResultadoAmbiguo = 2,

    /// <summary>
    /// Rechazo permanente (HTTP 4xx distinto de 429, número inválido o configuración incompleta).
    /// </summary>
    ErrorDefinitivo = 3
}

public sealed record WhatsAppSendTextRequest(
    string DestinatarioE164,
    string Texto,
    string IdempotencyKey);

public sealed record WhatsAppSendResult(
    bool Exitoso,
    string? ExternalMessageId,
    int? CodigoHttp,
    string? Error,
    TipoResultadoEnvioWhatsApp Clasificacion,
    string Proveedor,
    int? RetryAfterSegundos = null)
{
    public bool EsReintentableSeguro => Clasificacion == TipoResultadoEnvioWhatsApp.ErrorReintentableSeguro;
    public bool EsAmbiguo => Clasificacion == TipoResultadoEnvioWhatsApp.ResultadoAmbiguo;
}

/// <summary>
/// Contrato desacoplado para el proveedor de envío de mensajes de WhatsApp
/// (Cloud API oficial de Meta o proveedor simulado local).
/// </summary>
public interface IWhatsAppProvider
{
    string NombreProveedor { get; }

    Task<WhatsAppSendResult> EnviarTextoAsync(
        WhatsAppSendTextRequest request,
        CancellationToken ct = default);
}
