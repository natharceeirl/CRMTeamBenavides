namespace CRMTeamBenavides.Api.Features.WhatsApp;

/// <summary>
/// Configuración segura de la integración con WhatsApp Business Cloud API (Meta).
/// Permanece deshabilitada por defecto (<c>Enabled = false</c>) y con proveedor simulado (<c>Mock</c>).
/// Los secretos (<c>AccessToken</c>, <c>AppSecret</c>, <c>WebhookVerifyToken</c>) deben inyectarse
/// exclusivamente mediante variables de entorno o User Secrets, nunca en <c>appsettings.json</c>.
/// </summary>
public sealed class WhatsAppSettings
{
    public const string SectionName = "WhatsApp";

    public const string ProviderMock = "Mock";
    public const string ProviderMeta = "Meta";

    /// <summary>Habilita la recepción por webhook y el envío saliente. Por defecto <c>false</c>.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Proveedor activo: <c>Mock</c> (local sin llamadas externas) o <c>Meta</c> (Cloud API).</summary>
    public string Provider { get; set; } = ProviderMock;

    /// <summary>Identificador del número de teléfono en Meta WhatsApp Cloud API.</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>Identificador de la cuenta comercial de WhatsApp (WABA).</summary>
    public string BusinessAccountId { get; set; } = string.Empty;

    /// <summary>Token Bearer permanente de usuario del sistema en Meta (secreto).</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>App Secret de la aplicación Meta para validar <c>X-Hub-Signature-256</c> (secreto).</summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>Token secreto de verificación para el handshake GET del webhook (secreto).</summary>
    public string WebhookVerifyToken { get; set; } = string.Empty;

    /// <summary>
    /// Versión provisional de Graph API de Meta (valor inicial <c>v21.0</c>).
    /// Confirmar su vigencia en la consola de Meta for Developers antes de habilitar <c>Provider=Meta</c>.
    /// </summary>
    public string GraphApiVersion { get; set; } = "v21.0";

    /// <summary>URL base de Graph API de Meta.</summary>
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com";

    /// <summary>Código de país por defecto para normalizar números locales a E.164 (Perú = 51).</summary>
    public string DefaultCountryCode { get; set; } = "51";

    /// <summary>Timeout HTTP en segundos para llamadas hacia Meta Cloud API.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Tamaño máximo permitido del payload recibido en el webhook POST (por defecto 256 KB).</summary>
    public int MaxWebhookPayloadBytes { get; set; } = 262_144;

    /// <summary>Cantidad máxima de intentos de envío en la cola Outbox ante rechazos seguros reintentables (429/503).</summary>
    public int MaxSendRetries { get; set; } = 3;

    /// <summary>Intervalo en segundos entre ciclos del worker Outbox cuando la integración está activa.</summary>
    public int OutboxPollingIntervalSeconds { get; set; } = 5;

    /// <summary>Cantidad máxima de mensajes pendientes reservados por vuelta en el Outbox.</summary>
    public int OutboxBatchSize { get; set; } = 20;

    public bool UsaProveedorMock =>
        string.Equals(Provider?.Trim(), ProviderMock, StringComparison.OrdinalIgnoreCase);

    public bool UsaProveedorMeta =>
        string.Equals(Provider?.Trim(), ProviderMeta, StringComparison.OrdinalIgnoreCase);

    public bool EsProveedorReconocido => UsaProveedorMock || UsaProveedorMeta;

    public bool TieneCredencialesMetaCompletas =>
        !string.IsNullOrWhiteSpace(PhoneNumberId)
        && !string.IsNullOrWhiteSpace(AccessToken)
        && !string.IsNullOrWhiteSpace(GraphApiVersion);

    public bool TieneSeguridadWebhookConfigurada =>
        !string.IsNullOrWhiteSpace(AppSecret)
        && !string.IsNullOrWhiteSpace(WebhookVerifyToken);

    /// <summary>
    /// Impide la activación accidental del proveedor real Meta si falta cualquier credencial
    /// o secreto de seguridad del webhook.
    /// </summary>
    public bool PuedeOperarProveedorMeta =>
        Enabled
        && UsaProveedorMeta
        && TieneCredencialesMetaCompletas
        && TieneSeguridadWebhookConfigurada;

    /// <summary>
    /// Valida que la configuración sea segura para despachar mensajes salientes.
    /// Retorna <c>null</c> si es válida o un mensaje de error seguro sin exponer secretos.
    /// </summary>
    public string? ValidarConfiguracionEnvio()
    {
        if (!Enabled)
        {
            return "La integración de WhatsApp está deshabilitada. Habilítela explícitamente antes de enviar mensajes.";
        }

        if (!EsProveedorReconocido)
        {
            return $"Proveedor de WhatsApp no reconocido ('{Provider}'). Valores válidos: Mock o Meta.";
        }

        if (UsaProveedorMeta && !TieneCredencialesMetaCompletas)
        {
            return "Configuración incompleta para proveedor Meta: se requieren PhoneNumberId, AccessToken y GraphApiVersion vigentes.";
        }

        if (UsaProveedorMeta && !TieneSeguridadWebhookConfigurada)
        {
            return "Configuración de seguridad incompleta para proveedor Meta: se requieren AppSecret y WebhookVerifyToken antes de activar el envío real.";
        }

        return null;
    }
}
