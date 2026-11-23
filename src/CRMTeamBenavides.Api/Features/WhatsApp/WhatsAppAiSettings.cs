namespace CRMTeamBenavides.Api.Features.WhatsApp;

/// <summary>
/// Configuración desacoplada de la capa de inteligencia conversacional de WhatsApp (Fase 3).
/// Permanece deshabilitada por defecto (<c>Enabled = false</c>) y en modo <c>Mock</c>
/// para impedir cualquier respuesta automática o llamada externa no autorizada.
/// Los secretos (<c>ApiKey</c>) deben inyectarse solo mediante variables de entorno o User Secrets.
/// </summary>
public sealed class WhatsAppAiSettings
{
    public const string SectionName = "WhatsAppAi";

    public const string ProviderMock = "Mock";
    public const string ProviderExternal = "External";

    /// <summary>Habilita el procesamiento conversacional automático. Por defecto <c>false</c>.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Proveedor de IA configurado (<c>Mock</c> por defecto; proveedores externos permanecen deshabilitados).</summary>
    public string Provider { get; set; } = ProviderMock;

    /// <summary>Nombre lógico del modelo configurado.</summary>
    public string Model { get; set; } = "mock-deterministic-v1";

    /// <summary>Clave API del proveedor externo (secreto; jamás escribir en código, logs ni respuestas).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>URL base opcional para un proveedor externo futuro.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Longitud máxima aceptada del mensaje de entrada del usuario.</summary>
    public int MaxInputChars { get; set; } = 1000;

    /// <summary>Longitud máxima de la respuesta generada antes de despacharla.</summary>
    public int MaxOutputChars { get; set; } = 1200;

    /// <summary>Límite máximo de tokens de salida solicitados al proveedor.</summary>
    public int MaxOutputTokens { get; set; } = 350;

    /// <summary>Cantidad máxima de mensajes recientes del hilo incluidos como contexto.</summary>
    public int MaxHistoryMessages { get; set; } = 6;

    /// <summary>Tiempo máximo de espera en segundos para la generación de respuesta.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Permite consultar FAQs públicas autorizadas del taller.</summary>
    public bool AllowFaqTools { get; set; } = true;

    /// <summary>Permite consultar el catálogo público (productos/servicios sin costos ni stock exacto).</summary>
    public bool AllowPublicCatalogTools { get; set; } = true;

    /// <summary>Límite anti-bucle de respuestas automáticas por minuto en una misma conversación.</summary>
    public int MaxRepliesPerMinutePerConversation { get; set; } = 6;

    /// <summary>
    /// Antigüedad máxima (en minutos) de una respuesta automática pendiente en Outbox antes de descartarla.
    /// Impide despachar respuestas bot obsoletas acumuladas tras cortes o reactivaciones posteriores (F3-H01).
    /// No aplica a mensajes escritos por agentes humanos.
    /// </summary>
    public int MaxBotOutboxAgeMinutes { get; set; } = 15;

    public bool UsaProveedorMock =>
        string.Equals(Provider?.Trim(), ProviderMock, StringComparison.OrdinalIgnoreCase);

    public bool TieneCredencialesExternasConfiguradas =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiBaseUrl);

    public int ObtenerMaxInputCharsSeguro() => Math.Clamp(MaxInputChars, 50, 4096);
    public int ObtenerMaxOutputCharsSeguro() => Math.Clamp(MaxOutputChars, 100, 4096);
    public int ObtenerMaxOutputTokensSeguro() => Math.Clamp(MaxOutputTokens, 20, 1024);
    public int ObtenerMaxHistoryMessagesSeguro() => Math.Clamp(MaxHistoryMessages, 0, 20);
    public int ObtenerTimeoutSegundosSeguro() => Math.Clamp(TimeoutSeconds, 1, 60);
    public int ObtenerMaxRespuestasPorMinutoSeguro() => Math.Clamp(MaxRepliesPerMinutePerConversation, 1, 20);
    public int ObtenerMaxBotOutboxAgeMinutosSeguro() => Math.Clamp(MaxBotOutboxAgeMinutes, 1, 120);
}
