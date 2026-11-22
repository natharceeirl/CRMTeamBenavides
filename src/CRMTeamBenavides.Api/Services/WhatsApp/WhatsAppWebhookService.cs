using System.Globalization;
using System.Text.Json;
using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

public sealed record WhatsAppWebhookProcesoResultado(
    bool EsValido,
    string? Error,
    int MensajesProcesados,
    int MensajesDuplicados,
    int EstadosActualizados,
    int EstadosIgnorados,
    int EventosDesconocidos);

public interface IWhatsAppWebhookService
{
    Task<WhatsAppWebhookProcesoResultado> ProcesarNotificacionAsync(
        ReadOnlyMemory<byte> rawBody,
        CancellationToken ct = default);
}

public sealed class WhatsAppWebhookService : IWhatsAppWebhookService
{
    private static readonly KeyedAsyncLock<string> LocksWamid = new(StringComparer.Ordinal);
    private static readonly KeyedAsyncLock<string> LocksTelefono = new(StringComparer.Ordinal);

    private readonly ApplicationDbContext _context;
    private readonly IWhatsAppClienteIdentificacionService _identificacionService;
    private readonly IWhatsAppAutoResponder _autoResponder;
    private readonly IOptionsMonitor<WhatsAppSettings> _settingsMonitor;
    private readonly ILogger<WhatsAppWebhookService> _logger;

    public WhatsAppWebhookService(
        ApplicationDbContext context,
        IWhatsAppClienteIdentificacionService identificacionService,
        IWhatsAppAutoResponder autoResponder,
        IOptionsMonitor<WhatsAppSettings> settingsMonitor,
        ILogger<WhatsAppWebhookService> logger)
    {
        _context = context;
        _identificacionService = identificacionService;
        _autoResponder = autoResponder;
        _settingsMonitor = settingsMonitor;
        _logger = logger;
    }

    public async Task<WhatsAppWebhookProcesoResultado> ProcesarNotificacionAsync(
        ReadOnlyMemory<byte> rawBody,
        CancellationToken ct = default)
    {
        if (rawBody.IsEmpty)
        {
            return new WhatsAppWebhookProcesoResultado(false, "El cuerpo del webhook está vacío.", 0, 0, 0, 0, 0);
        }

        var payloadHash = WhatsAppWebhookSecurity.CalcularSha256Hex(rawBody.Span);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            return new WhatsAppWebhookProcesoResultado(false, "El cuerpo del webhook no es un JSON válido.", 0, 0, 0, 0, 0);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new WhatsAppWebhookProcesoResultado(false, "La raíz del payload del webhook debe ser un objeto JSON.", 0, 0, 0, 0, 0);
            }

            if (!root.TryGetProperty("object", out var objProp) || objProp.ValueKind != JsonValueKind.String)
            {
                return new WhatsAppWebhookProcesoResultado(false, "El payload del webhook no contiene la propiedad obligatoria 'object'.", 0, 0, 0, 0, 0);
            }

            var objeto = objProp.GetString() ?? string.Empty;
            if (!string.Equals(objeto, "whatsapp_business_account", StringComparison.OrdinalIgnoreCase))
            {
                await RegistrarEventoWebhookAsync(
                    payloadHash,
                    "unknown_object",
                    null,
                    0,
                    0,
                    true,
                    $"Objeto webhook no manejado: '{Truncar(objeto, 60)}'.",
                    ct);

                return new WhatsAppWebhookProcesoResultado(true, null, 0, 0, 0, 0, 1);
            }

            if (!root.TryGetProperty("entry", out var entryProp))
            {
                await RegistrarEventoWebhookAsync(payloadHash, "empty", null, 0, 0, true, "Notificación sin propiedad 'entry'.", ct);
                return new WhatsAppWebhookProcesoResultado(true, null, 0, 0, 0, 0, 1);
            }

            if (entryProp.ValueKind != JsonValueKind.Array)
            {
                return new WhatsAppWebhookProcesoResultado(false, "La propiedad 'entry' del webhook debe ser un arreglo JSON.", 0, 0, 0, 0, 0);
            }

            var settings = _settingsMonitor.CurrentValue;
            int mensajesProcesados = 0;
            int mensajesDuplicados = 0;
            int estadosActualizados = 0;
            int estadosIgnorados = 0;
            int eventosDesconocidos = 0;
            string? primerExternalId = null;

            foreach (var entry in entryProp.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || !entry.TryGetProperty("changes", out var changesProp)
                    || changesProp.ValueKind != JsonValueKind.Array)
                {
                    eventosDesconocidos++;
                    continue;
                }

                foreach (var change in changesProp.EnumerateArray())
                {
                    if (change.ValueKind != JsonValueKind.Object)
                    {
                        eventosDesconocidos++;
                        continue;
                    }

                    var field = ObtenerString(change, "field");
                    if (!string.Equals(field, "messages", StringComparison.OrdinalIgnoreCase))
                    {
                        eventosDesconocidos++;
                        continue;
                    }

                    if (!change.TryGetProperty("value", out var valueProp) || valueProp.ValueKind != JsonValueKind.Object)
                    {
                        eventosDesconocidos++;
                        continue;
                    }

                    var nombresPorWaId = ExtraerMapaContactos(valueProp);
                    bool tuvoMensajesOEstados = false;

                    if (valueProp.TryGetProperty("messages", out var messagesProp) && messagesProp.ValueKind == JsonValueKind.Array)
                    {
                        tuvoMensajesOEstados = true;
                        foreach (var msgElem in messagesProp.EnumerateArray())
                        {
                            if (msgElem.ValueKind != JsonValueKind.Object)
                            {
                                eventosDesconocidos++;
                                continue;
                            }

                            var wamid = ObtenerString(msgElem, "id");
                            primerExternalId ??= wamid;

                            var (procesado, duplicado) = await ProcesarMensajeEntranteAsync(
                                msgElem,
                                nombresPorWaId,
                                settings.DefaultCountryCode,
                                ct);

                            if (procesado) mensajesProcesados++;
                            if (duplicado) mensajesDuplicados++;
                        }
                    }

                    if (valueProp.TryGetProperty("statuses", out var statusesProp) && statusesProp.ValueKind == JsonValueKind.Array)
                    {
                        tuvoMensajesOEstados = true;
                        foreach (var statusElem in statusesProp.EnumerateArray())
                        {
                            if (statusElem.ValueKind != JsonValueKind.Object)
                            {
                                eventosDesconocidos++;
                                continue;
                            }

                            var statusWamid = ObtenerString(statusElem, "id");
                            primerExternalId ??= statusWamid;

                            var actualizado = await ProcesarNotificacionEstadoAsync(statusElem, ct);
                            if (actualizado) estadosActualizados++;
                            else estadosIgnorados++;
                        }
                    }

                    if (!tuvoMensajesOEstados)
                    {
                        eventosDesconocidos++;
                    }
                }
            }

            var tipoEvento = (mensajesProcesados + mensajesDuplicados > 0, estadosActualizados + estadosIgnorados > 0) switch
            {
                (true, true) => "mixed",
                (true, false) => "messages",
                (false, true) => "statuses",
                _ => "unknown"
            };

            var resumen = $"mensajes={mensajesProcesados}, duplicados={mensajesDuplicados}, estados={estadosActualizados}, estadosIgnorados={estadosIgnorados}, desconocidos={eventosDesconocidos}";

            await RegistrarEventoWebhookAsync(
                payloadHash,
                tipoEvento,
                Truncar(primerExternalId, 128),
                mensajesProcesados,
                estadosActualizados,
                true,
                resumen,
                ct);

            _logger.LogInformation(
                "WhatsApp webhook procesado. Tipo={Tipo}, {Resumen}",
                tipoEvento,
                resumen);

            return new WhatsAppWebhookProcesoResultado(
                EsValido: true,
                Error: null,
                MensajesProcesados: mensajesProcesados,
                MensajesDuplicados: mensajesDuplicados,
                EstadosActualizados: estadosActualizados,
                EstadosIgnorados: estadosIgnorados,
                EventosDesconocidos: eventosDesconocidos);
        }
    }

    private async Task<(bool Procesado, bool Duplicado)> ProcesarMensajeEntranteAsync(
        JsonElement msgElem,
        Dictionary<string, string> nombresPorWaId,
        string defaultCountryCode,
        CancellationToken ct)
    {
        var wamid = ObtenerString(msgElem, "id")?.Trim();
        var fromRaw = ObtenerString(msgElem, "from");

        var norm = WhatsAppPhoneNormalizer.NormalizarRemitenteMeta(fromRaw, defaultCountryCode);
        if (!norm.EsValido || string.IsNullOrEmpty(norm.TelefonoE164))
        {
            _logger.LogWarning("WhatsApp webhook ignoró mensaje entrante con número de origen inválido.");
            return (false, false);
        }

        IDisposable? lockWamid = null;
        if (!string.IsNullOrWhiteSpace(wamid))
        {
            lockWamid = await LocksWamid.AcquireAsync(wamid, ct);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(wamid))
            {
                var yaExiste = await _context.MensajesWhatsApp
                    .AsNoTracking()
                    .AnyAsync(m => m.ExternalMessageId == wamid, ct);

                if (yaExiste)
                {
                    return (false, true);
                }
            }

            var waId = norm.WhatsAppId!;
            nombresPorWaId.TryGetValue(waId, out var nombrePerfil);
            var fechaEventoUtc = ParsearUnixTimestampUtc(ObtenerString(msgElem, "timestamp")) ?? DateTime.UtcNow;
            var fechaRecepcionUtc = DateTime.UtcNow;

            var conversacion = await ObtenerOCrearConversacionAsync(
                norm.TelefonoE164,
                waId,
                nombrePerfil,
                fechaEventoUtc,
                ct);

            var parsed = ParsearContenidoMensaje(msgElem);

            var mensaje = new MensajeWhatsApp
            {
                ConversacionId = conversacion.Id,
                ExternalMessageId = Truncar(wamid, 128),
                Direccion = DireccionMensajeWhatsApp.Entrante,
                Tipo = parsed.Tipo,
                ContenidoTexto = Truncar(parsed.ContenidoTexto, 4096),
                Estado = EstadoMensajeWhatsApp.Recibido,
                FechaEventoUtc = fechaEventoUtc,
                FechaRecepcionUtc = fechaRecepcionUtc,
                FechaUltimoEstadoUtc = fechaRecepcionUtc,
                MediaId = Truncar(parsed.MediaId, 128),
                MediaMimeType = Truncar(parsed.MediaMimeType, 100),
                MediaSha256 = Truncar(parsed.MediaSha256, 128),
                MediaNombreArchivo = Truncar(parsed.MediaNombreArchivo, 255),
                MetadataTecnicaJson = Truncar(parsed.MetadataTecnicaJson, 2000),
                FechaCreacion = fechaRecepcionUtc,
                Activo = true
            };

            _context.MensajesWhatsApp.Add(mensaje);

            // Protección contra eventos fuera de orden: no retroceder la fecha del último mensaje.
            if (!conversacion.FechaUltimoMensaje.HasValue || fechaEventoUtc > conversacion.FechaUltimoMensaje.Value)
            {
                conversacion.FechaUltimoMensaje = fechaEventoUtc;
            }

            if (!conversacion.FechaUltimoMensajeEntrante.HasValue || fechaEventoUtc > conversacion.FechaUltimoMensajeEntrante.Value)
            {
                conversacion.FechaUltimoMensajeEntrante = fechaEventoUtc;
            }

            if (conversacion.Estado == EstadoConversacionWhatsApp.Cerrada)
            {
                conversacion.Estado = EstadoConversacionWhatsApp.Abierta;
            }

            conversacion.FechaModificacion = fechaRecepcionUtc;

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(wamid))
            {
                // Defensa en profundidad frente a concurrencia entre múltiples instancias de API.
                _context.Entry(mensaje).State = EntityState.Detached;
                var existeTrasColision = await _context.MensajesWhatsApp
                    .AsNoTracking()
                    .AnyAsync(m => m.ExternalMessageId == wamid, ct);

                if (existeTrasColision)
                {
                    return (false, true);
                }

                throw;
            }

            // Solo invocar procesamiento automático si la conversación está en modo Bot.
            // En modo Humano jamás se dispara automatización para no interferir con el asesor.
            if (conversacion.ModoAtencion == ModoAtencionWhatsApp.Bot)
            {
                await _autoResponder.ProcesarMensajeEntranteAsync(conversacion, mensaje, ct);
            }

            return (true, false);
        }
        finally
        {
            lockWamid?.Dispose();
        }
    }

    private async Task<ConversacionWhatsApp> ObtenerOCrearConversacionAsync(
        string telefonoE164,
        string waId,
        string? nombrePerfil,
        DateTime fechaEventoUtc,
        CancellationToken ct)
    {
        using var lockTel = await LocksTelefono.AcquireAsync(telefonoE164, ct);
        {
            var existente = await _context.ConversacionesWhatsApp
                .FirstOrDefaultAsync(c => c.TelefonoE164 == telefonoE164 && c.Activo, ct);

            if (existente is not null)
            {
                if (string.IsNullOrWhiteSpace(existente.WhatsAppId))
                {
                    existente.WhatsAppId = waId;
                }

                if (!string.IsNullOrWhiteSpace(nombrePerfil) && string.IsNullOrWhiteSpace(existente.NombrePerfilWhatsApp))
                {
                    existente.NombrePerfilWhatsApp = Truncar(nombrePerfil, 150);
                }

                return existente;
            }

            var identificacion = await _identificacionService.IdentificarPorTelefonoAsync(telefonoE164, ct);

            var ahora = DateTime.UtcNow;
            var nueva = new ConversacionWhatsApp
            {
                TelefonoE164 = telefonoE164,
                WhatsAppId = Truncar(waId, 32),
                NombrePerfilWhatsApp = Truncar(nombrePerfil, 150),
                ClienteId = identificacion.EsVinculableAutomaticamente ? identificacion.ClienteId : null,
                AsociacionClienteAmbigua = identificacion.EsAmbiguo,
                Estado = EstadoConversacionWhatsApp.Abierta,
                ModoAtencion = ModoAtencionWhatsApp.Bot,
                FechaUltimoMensaje = fechaEventoUtc,
                FechaUltimoMensajeEntrante = fechaEventoUtc,
                FechaCreacion = ahora,
                Activo = true
            };

            _context.ConversacionesWhatsApp.Add(nueva);
            try
            {
                await _context.SaveChangesAsync(ct);
                return nueva;
            }
            catch (DbUpdateException)
            {
                _context.Entry(nueva).State = EntityState.Detached;
                var concurrente = await _context.ConversacionesWhatsApp
                    .FirstOrDefaultAsync(c => c.TelefonoE164 == telefonoE164 && c.Activo, ct);

                if (concurrente is not null)
                {
                    return concurrente;
                }

                throw;
            }
        }
    }

    private async Task<bool> ProcesarNotificacionEstadoAsync(JsonElement statusElem, CancellationToken ct)
    {
        var wamid = ObtenerString(statusElem, "id")?.Trim();
        var statusStr = ObtenerString(statusElem, "status")?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(wamid) || string.IsNullOrWhiteSpace(statusStr))
        {
            return false;
        }

        var nuevoEstado = statusStr switch
        {
            "sent" => (EstadoMensajeWhatsApp?)EstadoMensajeWhatsApp.Enviado,
            "delivered" => EstadoMensajeWhatsApp.Entregado,
            "read" => EstadoMensajeWhatsApp.Leido,
            "failed" => EstadoMensajeWhatsApp.Fallido,
            _ => null
        };

        if (!nuevoEstado.HasValue)
        {
            return false;
        }

        using var lockWamid = await LocksWamid.AcquireAsync(wamid, ct);
        {
            var mensaje = await _context.MensajesWhatsApp
                .FirstOrDefaultAsync(m => m.ExternalMessageId == wamid && m.Activo, ct);

            if (mensaje is null)
            {
                // Evento de estado para un mensaje aún no persistido o externo: ignorar sin fallar.
                return false;
            }

            var fechaEstadoUtc = ParsearUnixTimestampUtc(ObtenerString(statusElem, "timestamp")) ?? DateTime.UtcNow;

            if (!DebeAplicarTransicionEstado(mensaje.Estado, nuevoEstado.Value, mensaje.FechaUltimoEstadoUtc, fechaEstadoUtc))
            {
                return false;
            }

            mensaje.Estado = nuevoEstado.Value;
            mensaje.FechaUltimoEstadoUtc = fechaEstadoUtc;
            mensaje.FechaModificacion = DateTime.UtcNow;

            if (nuevoEstado.Value == EstadoMensajeWhatsApp.Fallido)
            {
                var detalleError = ExtraerDetalleErrorEstado(statusElem);
                if (!string.IsNullOrWhiteSpace(detalleError))
                {
                    mensaje.ErrorDetalle = Truncar(detalleError, 500);
                }
            }

            await _context.SaveChangesAsync(ct);
            return true;
        }
    }

    /// <summary>
    /// Protege contra notificaciones de estado recibidas fuera de orden (ej. <c>delivered</c> tras <c>read</c>).
    /// </summary>
    internal static bool DebeAplicarTransicionEstado(
        EstadoMensajeWhatsApp actual,
        EstadoMensajeWhatsApp entrante,
        DateTime? fechaUltimoEstadoUtc,
        DateTime fechaEntranteUtc)
    {
        if (actual == entrante)
        {
            return false;
        }

        static int RangoProgresion(EstadoMensajeWhatsApp e) => e switch
        {
            EstadoMensajeWhatsApp.Pendiente => 0,
            EstadoMensajeWhatsApp.Enviado => 1,
            EstadoMensajeWhatsApp.Entregado => 2,
            EstadoMensajeWhatsApp.Leido => 3,
            _ => -1
        };

        if (entrante == EstadoMensajeWhatsApp.Fallido)
        {
            // No degradar a Fallido un mensaje ya Entregado o Leído.
            if (actual is EstadoMensajeWhatsApp.Entregado or EstadoMensajeWhatsApp.Leido)
            {
                return false;
            }

            return !fechaUltimoEstadoUtc.HasValue || fechaEntranteUtc >= fechaUltimoEstadoUtc.Value;
        }

        var rangoActual = RangoProgresion(actual);
        var rangoEntrante = RangoProgresion(entrante);

        if (rangoActual >= 0 && rangoEntrante >= 0)
        {
            return rangoEntrante > rangoActual;
        }

        return (actual is EstadoMensajeWhatsApp.Fallido or EstadoMensajeWhatsApp.Indeterminado) && rangoEntrante >= 2;
    }

    private sealed record MensajeParseado(
        TipoMensajeWhatsApp Tipo,
        string? ContenidoTexto,
        string? MediaId,
        string? MediaMimeType,
        string? MediaSha256,
        string? MediaNombreArchivo,
        string? MetadataTecnicaJson);

    private static MensajeParseado ParsearContenidoMensaje(JsonElement msgElem)
    {
        var tipoStr = ObtenerString(msgElem, "type")?.Trim().ToLowerInvariant() ?? string.Empty;

        switch (tipoStr)
        {
            case "text":
            {
                var body = msgElem.TryGetProperty("text", out var textObj) && textObj.ValueKind == JsonValueKind.Object
                    ? ObtenerString(textObj, "body")
                    : null;
                return new MensajeParseado(TipoMensajeWhatsApp.Texto, body, null, null, null, null, null);
            }

            case "image":
                return ParsearMultimedia(msgElem, "image", TipoMensajeWhatsApp.Imagen);

            case "document":
                return ParsearMultimedia(msgElem, "document", TipoMensajeWhatsApp.Documento);

            case "audio":
                return ParsearMultimedia(msgElem, "audio", TipoMensajeWhatsApp.Audio);

            case "video":
                return ParsearMultimedia(msgElem, "video", TipoMensajeWhatsApp.Video);

            case "sticker":
                return ParsearMultimedia(msgElem, "sticker", TipoMensajeWhatsApp.Sticker);

            case "location":
            {
                if (msgElem.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object)
                {
                    var nombre = ObtenerString(loc, "name");
                    var direccion = ObtenerString(loc, "address");
                    double? lat = loc.TryGetProperty("latitude", out var latP) && latP.TryGetDouble(out var lv) ? lv : null;
                    double? lng = loc.TryGetProperty("longitude", out var lngP) && lngP.TryGetDouble(out var lgv) ? lgv : null;

                    var meta = JsonSerializer.Serialize(new { latitude = lat, longitude = lng, name = nombre, address = direccion });
                    var resumenTexto = !string.IsNullOrWhiteSpace(nombre) || !string.IsNullOrWhiteSpace(direccion)
                        ? string.Join(" - ", new[] { nombre, direccion }.Where(s => !string.IsNullOrWhiteSpace(s)))
                        : null;

                    return new MensajeParseado(TipoMensajeWhatsApp.Ubicacion, resumenTexto, null, null, null, null, meta);
                }

                return new MensajeParseado(TipoMensajeWhatsApp.Ubicacion, null, null, null, null, null, null);
            }

            case "interactive":
            {
                string? titulo = null;
                string? replyId = null;
                string? subtipo = null;
                if (msgElem.TryGetProperty("interactive", out var inter) && inter.ValueKind == JsonValueKind.Object)
                {
                    subtipo = ObtenerString(inter, "type");
                    if (inter.TryGetProperty("button_reply", out var btn) && btn.ValueKind == JsonValueKind.Object)
                    {
                        replyId = ObtenerString(btn, "id");
                        titulo = ObtenerString(btn, "title");
                    }
                    else if (inter.TryGetProperty("list_reply", out var lst) && lst.ValueKind == JsonValueKind.Object)
                    {
                        replyId = ObtenerString(lst, "id");
                        titulo = ObtenerString(lst, "title");
                    }
                }

                var meta = JsonSerializer.Serialize(new { interactiveType = subtipo, replyId });
                return new MensajeParseado(TipoMensajeWhatsApp.Interactivo, titulo, null, null, null, null, meta);
            }

            case "reaction":
            {
                string? emoji = null;
                string? messageId = null;
                if (msgElem.TryGetProperty("reaction", out var reac) && reac.ValueKind == JsonValueKind.Object)
                {
                    emoji = ObtenerString(reac, "emoji");
                    messageId = ObtenerString(reac, "message_id");
                }

                var meta = JsonSerializer.Serialize(new { emoji, messageId });
                return new MensajeParseado(TipoMensajeWhatsApp.Reaccion, emoji, null, null, null, null, meta);
            }

            default:
            {
                var meta = JsonSerializer.Serialize(new { tipoOriginal = string.IsNullOrWhiteSpace(tipoStr) ? "unspecified" : tipoStr });
                return new MensajeParseado(TipoMensajeWhatsApp.Desconocido, null, null, null, null, null, meta);
            }
        }
    }

    private static MensajeParseado ParsearMultimedia(JsonElement msgElem, string propiedad, TipoMensajeWhatsApp tipo)
    {
        if (!msgElem.TryGetProperty(propiedad, out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return new MensajeParseado(tipo, null, null, null, null, null, null);
        }

        var caption = ObtenerString(obj, "caption");
        var mediaId = ObtenerString(obj, "id");
        var mime = ObtenerString(obj, "mime_type");
        var sha256 = ObtenerString(obj, "sha256");
        var filename = ObtenerString(obj, "filename");

        var meta = JsonSerializer.Serialize(new
        {
            tipoOriginal = propiedad,
            tieneCaption = !string.IsNullOrWhiteSpace(caption),
            mediaId,
            mimeType = mime
        });

        return new MensajeParseado(tipo, caption, mediaId, mime, sha256, filename, meta);
    }

    private static Dictionary<string, string> ExtraerMapaContactos(JsonElement valueProp)
    {
        var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!valueProp.TryGetProperty("contacts", out var contacts) || contacts.ValueKind != JsonValueKind.Array)
        {
            return mapa;
        }

        foreach (var contact in contacts.EnumerateArray())
        {
            if (contact.ValueKind != JsonValueKind.Object) continue;
            var waId = ObtenerString(contact, "wa_id")?.Trim();
            if (string.IsNullOrWhiteSpace(waId)) continue;

            if (contact.TryGetProperty("profile", out var prof) && prof.ValueKind == JsonValueKind.Object)
            {
                var name = ObtenerString(prof, "name")?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    mapa[waId] = name;
                }
            }
        }

        return mapa;
    }

    private static string? ExtraerDetalleErrorEstado(JsonElement statusElem)
    {
        if (!statusElem.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0)
        {
            return null;
        }

        var primero = errors[0];
        if (primero.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var code = primero.TryGetProperty("code", out var c) ? c.ToString() : "?";
        var title = ObtenerString(primero, "title") ?? "Error de entrega";
        var detail = ObtenerString(primero, "message");
        return string.IsNullOrWhiteSpace(detail)
            ? $"Meta error {code}: {title}"
            : $"Meta error {code}: {title} - {detail}";
    }

    private async Task RegistrarEventoWebhookAsync(
        string payloadHash,
        string tipoEvento,
        string? externalMessageId,
        int cantidadMensajes,
        int cantidadEstados,
        bool exito,
        string? resumen,
        CancellationToken ct)
    {
        var evento = new EventoWebhookWhatsApp
        {
            PayloadHashSha256 = payloadHash,
            TipoEvento = Truncar(tipoEvento, 64) ?? "unknown",
            ExternalMessageId = Truncar(externalMessageId, 128),
            CantidadMensajes = cantidadMensajes,
            CantidadEstados = cantidadEstados,
            ProcesadoConExito = exito,
            Resumen = Truncar(resumen, 500),
            FechaRecepcionUtc = DateTime.UtcNow,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        _context.EventosWebhookWhatsApp.Add(evento);
        await _context.SaveChangesAsync(ct);
    }

    private static string? ObtenerString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static DateTime? ParsearUnixTimestampUtc(string? unixStr)
    {
        if (string.IsNullOrWhiteSpace(unixStr)) return null;
        if (long.TryParse(unixStr.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
        return null;
    }

    private static string? Truncar(string? valor, int maxLongitud)
    {
        if (string.IsNullOrEmpty(valor)) return valor;
        return valor.Length <= maxLongitud ? valor : valor[..maxLongitud];
    }
}
