using System.Text.Json.Serialization;

namespace CRMTeamBenavides.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoConversacionWhatsApp
{
    Abierta = 0,
    EnAtencion = 1,
    Cerrada = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModoAtencionWhatsApp
{
    Bot = 0,
    Humano = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DireccionMensajeWhatsApp
{
    Entrante = 0,
    Saliente = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoMensajeWhatsApp
{
    Texto = 0,
    Imagen = 1,
    Documento = 2,
    Audio = 3,
    Video = 4,
    Ubicacion = 5,
    Sticker = 6,
    Interactivo = 7,
    Reaccion = 8,
    Desconocido = 99
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoMensajeWhatsApp
{
    Pendiente = 0,
    Enviado = 1,
    Entregado = 2,
    Leido = 3,
    Fallido = 4,
    Recibido = 5,
    /// <summary>
    /// Resultado ambiguo (timeout, corte de red, HTTP 504, 2xx sin wamid o caída durante despacho).
    /// No se reintenta automáticamente para evitar envíos duplicados en Meta; requiere revisión humana.
    /// </summary>
    Indeterminado = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoOutboxWhatsApp
{
    Pendiente = 0,
    Procesando = 1,
    Enviado = 2,
    Fallido = 3,
    /// <summary>
    /// Resultado incierto tras contactar (o intentar contactar) al proveedor externo.
    /// Detiene reintentos automáticos para impedir duplicados silenciosos.
    /// </summary>
    Indeterminado = 4
}

/// <summary>
/// Hilo conversacional persistente del canal WhatsApp Business Cloud API.
/// Se mantiene separado de <see cref="ConsultaChatbot"/> (canal web/móvil).
/// </summary>
public class ConversacionWhatsApp : BaseEntity
{
    /// <summary>Teléfono normalizado en formato internacional E.164 (ej. +51987654321).</summary>
    public string TelefonoE164 { get; set; } = string.Empty;

    /// <summary>Identificador de contacto reportado por WhatsApp (wa_id, sin signo '+').</summary>
    public string? WhatsAppId { get; set; }

    /// <summary>Nombre de perfil público reportado por WhatsApp cuando esté disponible.</summary>
    public string? NombrePerfilWhatsApp { get; set; }

    /// <summary>Cliente del CRM vinculado explícita o unívocamente. Nulo si no existe o es ambiguo.</summary>
    public Guid? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    /// <summary>
    /// Verdadero cuando más de un cliente activo del CRM comparte el mismo teléfono normalizado.
    /// Impide vinculación automática ambigua y alerta al personal para vinculación explícita.
    /// </summary>
    public bool AsociacionClienteAmbigua { get; set; } = false;

    public EstadoConversacionWhatsApp Estado { get; set; } = EstadoConversacionWhatsApp.Abierta;
    public ModoAtencionWhatsApp ModoAtencion { get; set; } = ModoAtencionWhatsApp.Bot;

    public Guid? AgenteAsignadoId { get; set; }
    public Usuario? AgenteAsignado { get; set; }

    public DateTime? FechaUltimoMensaje { get; set; }

    /// <summary>Marca del último mensaje entrante del cliente (ventana de 24 horas de Meta).</summary>
    public DateTime? FechaUltimoMensajeEntrante { get; set; }

    public DateTime? FechaCambioModoHumano { get; set; }

    public ICollection<MensajeWhatsApp> Mensajes { get; set; } = new List<MensajeWhatsApp>();
}

/// <summary>
/// Mensaje individual (entrante o saliente) dentro de una <see cref="ConversacionWhatsApp"/>.
/// </summary>
public class MensajeWhatsApp : BaseEntity
{
    public Guid ConversacionId { get; set; }
    public ConversacionWhatsApp Conversacion { get; set; } = null!;

    /// <summary>Identificador único de Meta (wamid). Único en la base cuando está presente.</summary>
    public string? ExternalMessageId { get; set; }

    /// <summary>Clave idempotente para prevenir envíos salientes duplicados.</summary>
    public string? IdempotencyKey { get; set; }

    public DireccionMensajeWhatsApp Direccion { get; set; }
    public TipoMensajeWhatsApp Tipo { get; set; } = TipoMensajeWhatsApp.Texto;

    /// <summary>Texto del mensaje o caption multimedia, cuando corresponda.</summary>
    public string? ContenidoTexto { get; set; }

    public EstadoMensajeWhatsApp Estado { get; set; } = EstadoMensajeWhatsApp.Pendiente;

    /// <summary>Fecha del evento reportada por Meta o del despacho saliente (UTC).</summary>
    public DateTime FechaEventoUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha exacta en que el CRM registró el mensaje (UTC).</summary>
    public DateTime FechaRecepcionUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha del último cambio de estado aplicado (protección contra eventos fuera de orden).</summary>
    public DateTime? FechaUltimoEstadoUtc { get; set; }

    /// <summary>Usuario del CRM que envió el mensaje manual, cuando corresponda.</summary>
    public Guid? EnviadoPorUsuarioId { get; set; }
    public Usuario? EnviadoPorUsuario { get; set; }

    /// <summary>Metadatos mínimos de adjuntos multimedia para futura descarga controlada.</summary>
    public string? MediaId { get; set; }
    public string? MediaMimeType { get; set; }
    public string? MediaSha256 { get; set; }
    public string? MediaNombreArchivo { get; set; }

    /// <summary>Metadata técnica mínima (ubicación, tipo original desconocido, proveedor, etc.) sin secretos.</summary>
    public string? MetadataTecnicaJson { get; set; }

    /// <summary>Resumen sanitizado del último error de envío o entrega.</summary>
    public string? ErrorDetalle { get; set; }
}

/// <summary>
/// Cola transaccional Outbox para despacho resiliente y libre de duplicados hacia WhatsApp.
/// </summary>
public class OutboxMensajeWhatsApp : BaseEntity
{
    public Guid MensajeWhatsAppId { get; set; }
    public MensajeWhatsApp MensajeWhatsApp { get; set; } = null!;

    public Guid ConversacionId { get; set; }

    public string DestinatarioE164 { get; set; } = string.Empty;
    public string ContenidoTexto { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;

    public EstadoOutboxWhatsApp Estado { get; set; } = EstadoOutboxWhatsApp.Pendiente;
    public int Intentos { get; set; } = 0;
    public int MaxIntentos { get; set; } = 3;

    public DateTime? ProximoIntentoUtc { get; set; }
    public DateTime? ReservadoHastaUtc { get; set; }
    public DateTime? FechaProcesadoUtc { get; set; }

    public string? UltimoError { get; set; }
    public int? UltimoCodigoHttp { get; set; }

    /// <summary>Bitácora JSON sanitizada con el resultado individual de cada intento de despacho.</summary>
    public string? HistorialIntentosJson { get; set; }
}

/// <summary>
/// Registro ligero de trazabilidad de notificaciones recibidas en el webhook de WhatsApp.
/// Almacena metadatos y hash del payload sin duplicar texto sensible ni exponer secretos.
/// </summary>
public class EventoWebhookWhatsApp : BaseEntity
{
    public string PayloadHashSha256 { get; set; } = string.Empty;
    public string TipoEvento { get; set; } = "unknown";
    public string? ExternalMessageId { get; set; }
    public int CantidadMensajes { get; set; }
    public int CantidadEstados { get; set; }
    public bool ProcesadoConExito { get; set; } = true;
    public string? Resumen { get; set; }
    public DateTime FechaRecepcionUtc { get; set; } = DateTime.UtcNow;
}
