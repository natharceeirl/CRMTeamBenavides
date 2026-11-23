using System.Text.Json;
using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

public sealed record IntentoOutboxRegistro(
    int Intento,
    DateTime FechaUtc,
    string Proveedor,
    string Clasificacion,
    int? CodigoHttp,
    string? ExternalMessageId,
    string? Detalle);

public interface IWhatsAppOutboxService
{
    /// <summary>
    /// Reclama y despacha de forma exclusiva un registro específico en estado <c>Pendiente</c>.
    /// Garantiza que reintentos concurrentes nunca envíen dos veces el mismo mensaje.
    /// </summary>
    Task<OutboxMensajeWhatsApp?> ProcesarRegistroAsync(Guid outboxId, CancellationToken ct = default);

    /// <summary>
    /// Recupera reservas expiradas en estado <c>Procesando</c> (posible caída de proceso)
    /// marcándolas de forma conservadora como <see cref="EstadoOutboxWhatsApp.Indeterminado"/>
    /// para evitar reenvíos duplicados en Meta.
    /// </summary>
    Task<int> RecuperarReservasExpiradasAsync(CancellationToken ct = default);

    /// <summary>
    /// Ejecuta recuperación de reservas vencidas y luego reclama y procesa un lote de mensajes
    /// exclusivamente en estado <c>Pendiente</c>.
    /// </summary>
    Task<int> ProcesarLotePendienteAsync(CancellationToken ct = default);
}

public sealed class WhatsAppOutboxService : IWhatsAppOutboxService
{
    private static readonly TimeSpan DuracionReserva = TimeSpan.FromMinutes(2);
    private static readonly KeyedAsyncLock<Guid> LocksOutbox = new();

    private readonly ApplicationDbContext _context;
    private readonly IWhatsAppProvider _provider;
    private readonly IOptionsMonitor<WhatsAppSettings> _settingsMonitor;
    private readonly IOptionsMonitor<WhatsAppAiSettings>? _aiSettingsMonitor;
    private readonly ILogger<WhatsAppOutboxService> _logger;

    public WhatsAppOutboxService(
        ApplicationDbContext context,
        IWhatsAppProvider provider,
        IOptionsMonitor<WhatsAppSettings> settingsMonitor,
        ILogger<WhatsAppOutboxService> logger,
        IOptionsMonitor<WhatsAppAiSettings>? aiSettingsMonitor = null)
    {
        _context = context;
        _provider = provider;
        _settingsMonitor = settingsMonitor;
        _logger = logger;
        _aiSettingsMonitor = aiSettingsMonitor;
    }

    public async Task<OutboxMensajeWhatsApp?> ProcesarRegistroAsync(Guid outboxId, CancellationToken ct = default)
    {
        var settings = _settingsMonitor.CurrentValue;
        if (settings.ValidarConfiguracionEnvio() is not null)
        {
            return await _context.OutboxMensajesWhatsApp
                .Include(o => o.MensajeWhatsApp)
                .FirstOrDefaultAsync(o => o.Id == outboxId && o.Activo, ct);
        }

        using var _ = await LocksOutbox.AcquireAsync(outboxId, ct);

        var ahora = DateTime.UtcNow;
        var registro = await ReclamarUnoPendienteAsync(outboxId, ahora, ct);
        if (registro is null)
        {
            return await _context.OutboxMensajesWhatsApp
                .Include(o => o.MensajeWhatsApp)
                .FirstOrDefaultAsync(o => o.Id == outboxId && o.Activo, ct);
        }

        await DespacharReclamadoAsync(registro, settings, ct);
        return registro;
    }

    public async Task<int> RecuperarReservasExpiradasAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        List<OutboxMensajeWhatsApp> expirados;

        if (_context.Database.IsNpgsql())
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            expirados = await _context.OutboxMensajesWhatsApp
                .FromSqlInterpolated($"""
                    SELECT * FROM "OutboxMensajesWhatsApp"
                    WHERE "Activo" = true
                      AND "Estado" = {(int)EstadoOutboxWhatsApp.Procesando}
                      AND "ReservadoHastaUtc" IS NOT NULL
                      AND "ReservadoHastaUtc" <= {ahora}
                    ORDER BY "ReservadoHastaUtc"
                    LIMIT 50
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            if (expirados.Count == 0)
            {
                await tx.RollbackAsync(ct);
                return 0;
            }

            await MarcarExpiradosComoIndeterminadosAsync(expirados, ahora, ct);
            await tx.CommitAsync(ct);
            return expirados.Count;
        }

        expirados = await _context.OutboxMensajesWhatsApp
            .Where(o =>
                o.Activo
                && o.Estado == EstadoOutboxWhatsApp.Procesando
                && o.ReservadoHastaUtc != null
                && o.ReservadoHastaUtc <= ahora)
            .OrderBy(o => o.ReservadoHastaUtc)
            .Take(50)
            .ToListAsync(ct);

        if (expirados.Count == 0)
        {
            return 0;
        }

        await MarcarExpiradosComoIndeterminadosAsync(expirados, ahora, ct);
        return expirados.Count;
    }

    public async Task<int> ProcesarLotePendienteAsync(CancellationToken ct = default)
    {
        var settings = _settingsMonitor.CurrentValue;
        if (settings.ValidarConfiguracionEnvio() is not null)
        {
            return 0;
        }

        // Primero recupera reservas expiradas pasándolas a Indeterminado (nunca reenvía ciegamente).
        await RecuperarReservasExpiradasAsync(ct);

        var tamanoLote = Math.Clamp(settings.OutboxBatchSize, 1, 100);
        var ahora = DateTime.UtcNow;

        var lote = await ReclamarLotePendienteAsync(tamanoLote, ahora, ct);
        if (lote.Count == 0)
        {
            return 0;
        }

        foreach (var item in lote)
        {
            using var _ = await LocksOutbox.AcquireAsync(item.Id, ct);
            await DespacharReclamadoAsync(item, settings, ct);
        }

        return lote.Count;
    }

    private async Task MarcarExpiradosComoIndeterminadosAsync(
        List<OutboxMensajeWhatsApp> expirados,
        DateTime ahora,
        CancellationToken ct)
    {
        const string motivo = "Reserva expirada tras posible caída del proceso durante el despacho; marcado como Indeterminado para evitar envío duplicado en Meta.";

        var mensajeIds = expirados.Select(e => e.MensajeWhatsAppId).Distinct().ToList();
        var mensajes = await _context.MensajesWhatsApp
            .Where(m => mensajeIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        foreach (var item in expirados)
        {
            item.Estado = EstadoOutboxWhatsApp.Indeterminado;
            item.ReservadoHastaUtc = null;
            item.ProximoIntentoUtc = null;
            item.FechaProcesadoUtc = ahora;
            item.UltimoError = motivo;
            item.FechaModificacion = ahora;
            item.HistorialIntentosJson = AgregarIntentoAlHistorial(
                item.HistorialIntentosJson,
                new IntentoOutboxRegistro(
                    Intento: item.Intentos,
                    FechaUtc: ahora,
                    Proveedor: _provider.NombreProveedor,
                    Clasificacion: TipoResultadoEnvioWhatsApp.ResultadoAmbiguo.ToString(),
                    CodigoHttp: null,
                    ExternalMessageId: null,
                    Detalle: motivo));

            if (mensajes.TryGetValue(item.MensajeWhatsAppId, out var mensaje)
                && string.IsNullOrWhiteSpace(mensaje.ExternalMessageId))
            {
                mensaje.Estado = EstadoMensajeWhatsApp.Indeterminado;
                mensaje.ErrorDetalle = motivo;
                mensaje.FechaUltimoEstadoUtc = ahora;
                mensaje.FechaModificacion = ahora;
            }

            _logger.LogWarning(
                "WhatsApp Outbox reserva expirada marcada como Indeterminado (sin reenvío automático). OutboxId={OutboxId}, Intentos={Intentos}",
                item.Id,
                item.Intentos);
        }

        await _context.SaveChangesAsync(ct);
    }

    private async Task<OutboxMensajeWhatsApp?> ReclamarUnoPendienteAsync(Guid outboxId, DateTime ahora, CancellationToken ct)
    {
        if (_context.Database.IsNpgsql())
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            // Sin .Include() sobre FromSqlInterpolated + FOR UPDATE SKIP LOCKED para evitar subconsultas/JOINs inválidos en PostgreSQL.
            var reclamados = await _context.OutboxMensajesWhatsApp
                .FromSqlInterpolated($"""
                    SELECT * FROM "OutboxMensajesWhatsApp"
                    WHERE "Id" = {outboxId}
                      AND "Activo" = true
                      AND "Estado" = {(int)EstadoOutboxWhatsApp.Pendiente}
                      AND ("ProximoIntentoUtc" IS NULL OR "ProximoIntentoUtc" <= {ahora})
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            var item = reclamados.FirstOrDefault();
            if (item is null)
            {
                await tx.RollbackAsync(CancellationToken.None);
                return null;
            }

            // Garantiza lectura fresca tras FOR UPDATE SKIP LOCKED si el registro ya estaba rastreado en el DbContext.
            await _context.Entry(item).ReloadAsync(ct);
            if (item.Estado != EstadoOutboxWhatsApp.Pendiente)
            {
                await tx.RollbackAsync(CancellationToken.None);
                return null;
            }

            item.Estado = EstadoOutboxWhatsApp.Procesando;
            item.ReservadoHastaUtc = ahora.Add(DuracionReserva);
            item.FechaModificacion = ahora;
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return item;
        }

        var candidato = await _context.OutboxMensajesWhatsApp
            .FirstOrDefaultAsync(o =>
                o.Id == outboxId
                && o.Activo
                && o.Estado == EstadoOutboxWhatsApp.Pendiente
                && (o.ProximoIntentoUtc == null || o.ProximoIntentoUtc <= ahora), ct);

        if (candidato is null)
        {
            return null;
        }

        candidato.Estado = EstadoOutboxWhatsApp.Procesando;
        candidato.ReservadoHastaUtc = ahora.Add(DuracionReserva);
        candidato.FechaModificacion = ahora;
        await _context.SaveChangesAsync(ct);
        return candidato;
    }

    private async Task<List<OutboxMensajeWhatsApp>> ReclamarLotePendienteAsync(int tamanoLote, DateTime ahora, CancellationToken ct)
    {
        if (_context.Database.IsNpgsql())
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            var lote = await _context.OutboxMensajesWhatsApp
                .FromSqlInterpolated($"""
                    SELECT * FROM "OutboxMensajesWhatsApp"
                    WHERE "Activo" = true
                      AND "Estado" = {(int)EstadoOutboxWhatsApp.Pendiente}
                      AND ("ProximoIntentoUtc" IS NULL OR "ProximoIntentoUtc" <= {ahora})
                    ORDER BY "FechaCreacion"
                    LIMIT {tamanoLote}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            var confirmados = new List<OutboxMensajeWhatsApp>(lote.Count);
            foreach (var item in lote)
            {
                await _context.Entry(item).ReloadAsync(ct);
                if (item.Estado != EstadoOutboxWhatsApp.Pendiente)
                {
                    continue;
                }

                item.Estado = EstadoOutboxWhatsApp.Procesando;
                item.ReservadoHastaUtc = ahora.Add(DuracionReserva);
                item.FechaModificacion = ahora;
                confirmados.Add(item);
            }

            if (confirmados.Count > 0)
            {
                await _context.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
            return confirmados;
        }

        var candidatos = await _context.OutboxMensajesWhatsApp
            .Where(o =>
                o.Activo
                && o.Estado == EstadoOutboxWhatsApp.Pendiente
                && (o.ProximoIntentoUtc == null || o.ProximoIntentoUtc <= ahora))
            .OrderBy(o => o.FechaCreacion)
            .Take(tamanoLote)
            .ToListAsync(ct);

        foreach (var item in candidatos)
        {
            item.Estado = EstadoOutboxWhatsApp.Procesando;
            item.ReservadoHastaUtc = ahora.Add(DuracionReserva);
            item.FechaModificacion = ahora;
        }

        if (candidatos.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }

        return candidatos;
    }

    private async Task DespacharReclamadoAsync(
        OutboxMensajeWhatsApp item,
        WhatsAppSettings settings,
        CancellationToken ct)
    {
        var mensaje = await _context.MensajesWhatsApp
            .FirstOrDefaultAsync(m => m.Id == item.MensajeWhatsAppId, ct);

        // Guardia adicional: si ya cuenta con ExternalMessageId confirmado, jamás reenviar.
        if (mensaje is not null && !string.IsNullOrWhiteSpace(mensaje.ExternalMessageId))
        {
            item.Estado = EstadoOutboxWhatsApp.Enviado;
            item.ReservadoHastaUtc = null;
            item.FechaProcesadoUtc ??= DateTime.UtcNow;
            item.FechaModificacion = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return;
        }

        // Guardias exclusivas para respuestas automáticas del bot (ai:reply:*):
        // jamás bloquean mensajes legítimos enviados por agentes humanos (EnviadoPorUsuarioId != null).
        if (EsMensajeAutomaticoBot(item, mensaje))
        {
            var motivoCancelacion = await EvaluarCancelacionMensajeBotAsync(item, mensaje, ct);
            if (motivoCancelacion is not null)
            {
                var ahoraCancel = DateTime.UtcNow;
                item.Estado = EstadoOutboxWhatsApp.Fallido;
                item.ReservadoHastaUtc = null;
                item.ProximoIntentoUtc = null;
                item.FechaProcesadoUtc = ahoraCancel;
                item.UltimoError = motivoCancelacion;
                item.FechaModificacion = ahoraCancel;
                item.HistorialIntentosJson = AgregarIntentoAlHistorial(
                    item.HistorialIntentosJson,
                    new IntentoOutboxRegistro(
                        Intento: item.Intentos,
                        FechaUtc: ahoraCancel,
                        Proveedor: _provider.NombreProveedor,
                        Clasificacion: "CanceladoPorPoliticaBot",
                        CodigoHttp: null,
                        ExternalMessageId: null,
                        Detalle: motivoCancelacion));

                if (mensaje is not null)
                {
                    mensaje.Estado = EstadoMensajeWhatsApp.Fallido;
                    mensaje.ErrorDetalle = motivoCancelacion;
                    mensaje.FechaUltimoEstadoUtc = ahoraCancel;
                    mensaje.FechaModificacion = ahoraCancel;
                }

                await _context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "WhatsApp Outbox canceló despacho automático del bot sin llamar al proveedor. OutboxId={OutboxId}, ConversacionId={ConversacionId}",
                    item.Id,
                    item.ConversacionId);
                return;
            }
        }

        item.Intentos += 1;
        // La llamada HTTP al proveedor se ejecuta fuera de cualquier transacción de BD abierta.
        var resultado = await _provider.EnviarTextoAsync(
            new WhatsAppSendTextRequest(item.DestinatarioE164, item.ContenidoTexto, item.IdempotencyKey),
            ct);

        var ahora = DateTime.UtcNow;
        item.UltimoCodigoHttp = resultado.CodigoHttp;
        item.ReservadoHastaUtc = null;
        item.FechaModificacion = ahora;

        var errorSeguro = resultado.Exitoso
            ? null
            : WhatsAppWebhookSecurity.SanitizarMensajeError(
                resultado.Error ?? "Error desconocido del proveedor de WhatsApp.",
                settings.AccessToken,
                settings.AppSecret,
                settings.WebhookVerifyToken);

        item.HistorialIntentosJson = AgregarIntentoAlHistorial(
            item.HistorialIntentosJson,
            new IntentoOutboxRegistro(
                Intento: item.Intentos,
                FechaUtc: ahora,
                Proveedor: resultado.Proveedor,
                Clasificacion: resultado.Clasificacion.ToString(),
                CodigoHttp: resultado.CodigoHttp,
                ExternalMessageId: resultado.ExternalMessageId,
                Detalle: errorSeguro));

        if (resultado.Exitoso)
        {
            item.Estado = EstadoOutboxWhatsApp.Enviado;
            item.FechaProcesadoUtc = ahora;
            item.ProximoIntentoUtc = null;
            item.UltimoError = null;

            if (mensaje is not null)
            {
                mensaje.ExternalMessageId = resultado.ExternalMessageId;
                mensaje.Estado = EstadoMensajeWhatsApp.Enviado;
                mensaje.FechaEventoUtc = ahora;
                mensaje.FechaUltimoEstadoUtc = ahora;
                mensaje.ErrorDetalle = null;
                mensaje.MetadataTecnicaJson = JsonSerializer.Serialize(new
                {
                    proveedor = resultado.Proveedor,
                    codigoHttp = resultado.CodigoHttp
                });
                mensaje.FechaModificacion = ahora;
            }

            var conversacion = await _context.ConversacionesWhatsApp
                .FirstOrDefaultAsync(c => c.Id == item.ConversacionId, ct);
            if (conversacion is not null)
            {
                conversacion.FechaUltimoMensaje = ahora;
                conversacion.FechaModificacion = ahora;
            }

            await _context.SaveChangesAsync(ct);
            return;
        }

        item.UltimoError = errorSeguro;
        var maxIntentos = item.MaxIntentos > 0 ? item.MaxIntentos : Math.Max(1, settings.MaxSendRetries);

        if (resultado.EsAmbiguo)
        {
            // Política conservadora: ante resultado ambiguo (timeout, corte de red, 500/502/504, 2xx sin wamid)
            // NO reintentar automáticamente para evitar duplicados silenciosos en Meta.
            item.Estado = EstadoOutboxWhatsApp.Indeterminado;
            item.FechaProcesadoUtc = ahora;
            item.ProximoIntentoUtc = null;

            if (mensaje is not null)
            {
                mensaje.Estado = EstadoMensajeWhatsApp.Indeterminado;
                mensaje.ErrorDetalle = errorSeguro;
                mensaje.FechaUltimoEstadoUtc = ahora;
                mensaje.FechaModificacion = ahora;
            }

            _logger.LogWarning(
                "WhatsApp Outbox marcado como Indeterminado (sin reintento automático para evitar duplicado). OutboxId={OutboxId}, Intento={Intento}, Error={Error}",
                item.Id,
                item.Intentos,
                errorSeguro);
        }
        else if (resultado.EsReintentableSeguro && item.Intentos < maxIntentos)
        {
            // Solo rechazos explícitos previos al despacho (HTTP 429 / 503) son reintentables con backoff.
            var esperaSegundos = resultado.RetryAfterSegundos
                ?? (int)(30 * Math.Pow(2, item.Intentos - 1));

            item.Estado = EstadoOutboxWhatsApp.Pendiente;
            item.ProximoIntentoUtc = ahora.AddSeconds(Math.Clamp(esperaSegundos, 5, 3600));

            if (mensaje is not null)
            {
                mensaje.Estado = EstadoMensajeWhatsApp.Pendiente;
                mensaje.ErrorDetalle = errorSeguro;
                mensaje.FechaModificacion = ahora;
            }

            _logger.LogWarning(
                "WhatsApp Outbox reprogramado tras rechazo seguro reintentable. OutboxId={OutboxId}, Intento={Intento}/{Max}, ProximoIntentoUtc={Proximo}",
                item.Id,
                item.Intentos,
                maxIntentos,
                item.ProximoIntentoUtc);
        }
        else
        {
            item.Estado = EstadoOutboxWhatsApp.Fallido;
            item.FechaProcesadoUtc = ahora;
            item.ProximoIntentoUtc = null;

            if (mensaje is not null)
            {
                mensaje.Estado = EstadoMensajeWhatsApp.Fallido;
                mensaje.ErrorDetalle = errorSeguro;
                mensaje.FechaUltimoEstadoUtc = ahora;
                mensaje.FechaModificacion = ahora;
            }

            _logger.LogWarning(
                "WhatsApp Outbox marcado como fallido definitivo. OutboxId={OutboxId}, Intento={Intento}/{Max}, Error={Error}",
                item.Id,
                item.Intentos,
                maxIntentos,
                errorSeguro);
        }

        await _context.SaveChangesAsync(ct);
    }

    internal static string AgregarIntentoAlHistorial(string? historialActual, IntentoOutboxRegistro nuevo)
    {
        var lista = new List<IntentoOutboxRegistro>();
        if (!string.IsNullOrWhiteSpace(historialActual))
        {
            try
            {
                var existente = JsonSerializer.Deserialize<List<IntentoOutboxRegistro>>(historialActual);
                if (existente is not null)
                {
                    lista.AddRange(existente);
                }
            }
            catch (JsonException)
            {
                // Si hubiera un valor previo no parseable, iniciar bitácora limpia.
            }
        }

        lista.Add(nuevo);

        var serializado = JsonSerializer.Serialize(lista);
        while (serializado.Length > 4000 && lista.Count > 1)
        {
            lista.RemoveAt(0);
            serializado = JsonSerializer.Serialize(lista);
        }

        return serializado.Length <= 4000 ? serializado : serializado[..4000];
    }

    private static bool EsMensajeAutomaticoBot(OutboxMensajeWhatsApp item, MensajeWhatsApp? mensaje) =>
        item.IdempotencyKey.StartsWith("ai:reply:", StringComparison.Ordinal)
        && (mensaje is null || mensaje.EnviadoPorUsuarioId is null);

    /// <summary>
    /// Verifica antes de llamar al proveedor externo si una respuesta automática del bot
    /// debe cancelarse (F3-H01: expiración o IA deshabilitada; F3-H02: toma humana, agente asignado o cierre).
    /// </summary>
    private async Task<string?> EvaluarCancelacionMensajeBotAsync(
        OutboxMensajeWhatsApp item,
        MensajeWhatsApp? mensaje,
        CancellationToken ct)
    {
        var aiSettings = _aiSettingsMonitor?.CurrentValue;

        // F3-H01: si la IA está deshabilitada o el registro bot superó su antigüedad máxima, no despachar respuestas antiguas.
        if (aiSettings is not null && !aiSettings.Enabled)
        {
            return "Envío automático omitido: la IA conversacional está deshabilitada.";
        }

        var maxAgeMinutos = aiSettings?.ObtenerMaxBotOutboxAgeMinutosSeguro() ?? 15;
        if (DateTime.UtcNow - item.FechaCreacion > TimeSpan.FromMinutes(maxAgeMinutos))
        {
            return $"Envío automático omitido: la respuesta del bot superó la vigencia máxima de {maxAgeMinutos} minutos.";
        }

        var conversacion = await _context.ConversacionesWhatsApp
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == item.ConversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return "Envío automático omitido: la conversación ya no existe o está inactiva.";
        }

        if (conversacion.Estado == EstadoConversacionWhatsApp.Cerrada)
        {
            return "Envío automático omitido: la conversación se encuentra cerrada.";
        }

        // Un mensaje bot con escalado=true es el aviso generado atómicamente al derivar a humano:
        // solo se permite enviarlo mientras ningún agente humano haya tomado o intervenido posteriormente.
        var esAvisoDeEscalamiento = EsAvisoDeEscalamientoBot(mensaje?.MetadataTecnicaJson);
        if (esAvisoDeEscalamiento)
        {
            var intervencionHumanaPosterior = conversacion.ModificadoPorId.HasValue
                && (!conversacion.FechaModificacion.HasValue
                    || mensaje is null
                    || conversacion.FechaModificacion.Value >= mensaje.FechaCreacion);

            if (conversacion.AgenteAsignadoId.HasValue || intervencionHumanaPosterior)
            {
                return "Envío automático omitido: un agente humano ya intervino en la conversación.";
            }

            return null;
        }

        // Respuestas normales del bot: jamás despachar si la conversación pasó a Humano, EnAtencion o fue asignada.
        if (conversacion.ModoAtencion != ModoAtencionWhatsApp.Bot
            || conversacion.AgenteAsignadoId.HasValue
            || conversacion.Estado == EstadoConversacionWhatsApp.EnAtencion)
        {
            return "Envío automático omitido: la conversación pasó a atención humana antes del despacho.";
        }

        return null;
    }

    private static bool EsAvisoDeEscalamientoBot(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty("escalado", out var prop)
                && prop.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
