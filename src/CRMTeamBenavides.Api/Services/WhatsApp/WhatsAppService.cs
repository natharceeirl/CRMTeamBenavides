using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

public interface IWhatsAppService
{
    WhatsAppEstadoIntegracionResponse ObtenerEstadoIntegracion();

    Task<WhatsAppConversacionesPaginadasResponse> ListarConversacionesAsync(
        int pagina,
        int tamanoPagina,
        EstadoConversacionWhatsApp? estado,
        ModoAtencionWhatsApp? modoAtencion,
        bool? soloSinAsignar,
        string? busqueda,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppConversacionDetalleResponse>> ObtenerConversacionAsync(
        Guid conversacionId,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppMensajesPaginadosResponse>> ListarMensajesAsync(
        Guid conversacionId,
        int pagina,
        int tamanoPagina,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppConversacionDetalleResponse>> AsignarAgenteAsync(
        Guid conversacionId,
        WhatsAppAsignarAgenteRequest request,
        Guid usuarioAccionId,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppConversacionDetalleResponse>> CambiarModoAtencionAsync(
        Guid conversacionId,
        WhatsAppCambiarModoRequest request,
        Guid usuarioAccionId,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppConversacionDetalleResponse>> CambiarEstadoAsync(
        Guid conversacionId,
        WhatsAppCambiarEstadoRequest request,
        Guid usuarioAccionId,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppConversacionDetalleResponse>> VincularClienteAsync(
        Guid conversacionId,
        WhatsAppVincularClienteRequest request,
        Guid usuarioAccionId,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppMensajeResponse>> EnviarMensajeManualAsync(
        Guid conversacionId,
        WhatsAppEnviarMensajeRequest request,
        Guid usuarioAccionId,
        CancellationToken ct = default);
}

public sealed class WhatsAppService : IWhatsAppService
{
    private static readonly KeyedAsyncLock<string> LocksIdempotencia = new(StringComparer.Ordinal);

    private readonly ApplicationDbContext _context;
    private readonly IWhatsAppOutboxService _outboxService;
    private readonly IAuditoriaService _auditoria;
    private readonly IOptionsMonitor<WhatsAppSettings> _settingsMonitor;

    public WhatsAppService(
        ApplicationDbContext context,
        IWhatsAppOutboxService outboxService,
        IAuditoriaService auditoria,
        IOptionsMonitor<WhatsAppSettings> settingsMonitor)
    {
        _context = context;
        _outboxService = outboxService;
        _auditoria = auditoria;
        _settingsMonitor = settingsMonitor;
    }

    public WhatsAppEstadoIntegracionResponse ObtenerEstadoIntegracion()
    {
        var s = _settingsMonitor.CurrentValue;
        return new WhatsAppEstadoIntegracionResponse(
            Enabled: s.Enabled,
            Provider: string.IsNullOrWhiteSpace(s.Provider) ? WhatsAppSettings.ProviderMock : s.Provider.Trim(),
            GraphApiVersion: s.GraphApiVersion,
            TieneCredencialesMetaConfiguradas: s.TieneCredencialesMetaCompletas,
            TieneSeguridadWebhookConfigurada: s.TieneSeguridadWebhookConfigurada);
    }

    public async Task<WhatsAppConversacionesPaginadasResponse> ListarConversacionesAsync(
        int pagina,
        int tamanoPagina,
        EstadoConversacionWhatsApp? estado,
        ModoAtencionWhatsApp? modoAtencion,
        bool? soloSinAsignar,
        string? busqueda,
        CancellationToken ct = default)
    {
        var paginaSegura = Math.Max(1, pagina);
        var tamanoSeguro = Math.Clamp(tamanoPagina, 1, 100);

        var query = _context.ConversacionesWhatsApp
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Include(c => c.AgenteAsignado)
            .Where(c => c.Activo);

        if (estado.HasValue)
        {
            query = query.Where(c => c.Estado == estado.Value);
        }

        if (modoAtencion.HasValue)
        {
            query = query.Where(c => c.ModoAtencion == modoAtencion.Value);
        }

        if (soloSinAsignar == true)
        {
            query = query.Where(c => c.AgenteAsignadoId == null);
        }

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.Trim().ToLower();
            query = query.Where(c =>
                c.TelefonoE164.ToLower().Contains(term)
                || (c.NombrePerfilWhatsApp != null && c.NombrePerfilWhatsApp.ToLower().Contains(term))
                || (c.Cliente != null && c.Cliente.NombreCompleto.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);

        var itemsDb = await query
            .OrderByDescending(c => c.FechaUltimoMensaje ?? c.FechaCreacion)
            .Skip((paginaSegura - 1) * tamanoSeguro)
            .Take(tamanoSeguro)
            .ToListAsync(ct);

        var ids = itemsDb.Select(c => c.Id).ToList();
        var ultimosMensajes = ids.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _context.MensajesWhatsApp
                .AsNoTracking()
                .Where(m => ids.Contains(m.ConversacionId) && m.Activo)
                .GroupBy(m => m.ConversacionId)
                .Select(g => new
                {
                    ConversacionId = g.Key,
                    Ultimo = g.OrderByDescending(m => m.FechaEventoUtc)
                              .ThenByDescending(m => m.FechaRecepcionUtc)
                              .Select(m => m.ContenidoTexto ?? $"[{m.Tipo}]")
                              .FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.ConversacionId, x => x.Ultimo, ct);

        var items = itemsDb.Select(c =>
        {
            ultimosMensajes.TryGetValue(c.Id, out var preview);
            if (!string.IsNullOrEmpty(preview) && preview.Length > 120)
            {
                preview = preview[..120] + "...";
            }

            return new WhatsAppConversacionResumenResponse(
                c.Id,
                c.TelefonoE164,
                c.WhatsAppId,
                c.NombrePerfilWhatsApp,
                c.ClienteId,
                c.Cliente?.NombreCompleto,
                c.AsociacionClienteAmbigua,
                c.Estado,
                c.ModoAtencion,
                c.AgenteAsignadoId,
                c.AgenteAsignado?.NombreCompleto,
                c.FechaCreacion,
                c.FechaUltimoMensaje,
                c.FechaUltimoMensajeEntrante,
                c.FechaModificacion,
                preview);
        }).ToList();

        return new WhatsAppConversacionesPaginadasResponse(items, total, paginaSegura, tamanoSeguro);
    }

    public async Task<ServiceResult<WhatsAppConversacionDetalleResponse>> ObtenerConversacionAsync(
        Guid conversacionId,
        CancellationToken ct = default)
    {
        var conversacion = await _context.ConversacionesWhatsApp
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Include(c => c.AgenteAsignado)
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return ServiceResult<WhatsAppConversacionDetalleResponse>.NotFound("Conversación de WhatsApp no encontrada.");
        }

        var totalMensajes = await _context.MensajesWhatsApp
            .AsNoTracking()
            .CountAsync(m => m.ConversacionId == conversacion.Id && m.Activo, ct);

        return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(MapDetalle(conversacion, totalMensajes));
    }

    public async Task<ServiceResult<WhatsAppMensajesPaginadosResponse>> ListarMensajesAsync(
        Guid conversacionId,
        int pagina,
        int tamanoPagina,
        CancellationToken ct = default)
    {
        var existe = await _context.ConversacionesWhatsApp
            .AsNoTracking()
            .AnyAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (!existe)
        {
            return ServiceResult<WhatsAppMensajesPaginadosResponse>.NotFound("Conversación de WhatsApp no encontrada.");
        }

        var paginaSegura = Math.Max(1, pagina);
        var tamanoSeguro = Math.Clamp(tamanoPagina, 1, 200);

        var query = _context.MensajesWhatsApp
            .AsNoTracking()
            .Include(m => m.EnviadoPorUsuario)
            .Where(m => m.ConversacionId == conversacionId && m.Activo);

        var total = await query.CountAsync(ct);

        var mensajes = await query
            .OrderByDescending(m => m.FechaEventoUtc)
            .ThenByDescending(m => m.FechaRecepcionUtc)
            .Skip((paginaSegura - 1) * tamanoSeguro)
            .Take(tamanoSeguro)
            .ToListAsync(ct);

        var items = mensajes.Select(MapMensaje).ToList();
        return ServiceResult<WhatsAppMensajesPaginadosResponse>.Success(
            new WhatsAppMensajesPaginadosResponse(items, total, paginaSegura, tamanoSeguro));
    }

    public async Task<ServiceResult<WhatsAppConversacionDetalleResponse>> AsignarAgenteAsync(
        Guid conversacionId,
        WhatsAppAsignarAgenteRequest request,
        Guid usuarioAccionId,
        CancellationToken ct = default)
    {
        var conversacion = await _context.ConversacionesWhatsApp
            .Include(c => c.Cliente)
            .Include(c => c.AgenteAsignado)
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return ServiceResult<WhatsAppConversacionDetalleResponse>.NotFound("Conversación de WhatsApp no encontrada.");
        }

        Usuario? agente = null;
        if (request.AgenteId.HasValue)
        {
            agente = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == request.AgenteId.Value && u.Activo, ct);

            if (agente is null)
            {
                return ServiceResult<WhatsAppConversacionDetalleResponse>.Invalid("El trabajador especificado no existe o está inactivo.");
            }
        }

        var anteriorAgenteId = conversacion.AgenteAsignadoId;
        conversacion.AgenteAsignadoId = agente?.Id;
        conversacion.AgenteAsignado = agente;
        if (agente is not null && conversacion.Estado == EstadoConversacionWhatsApp.Abierta)
        {
            conversacion.Estado = EstadoConversacionWhatsApp.EnAtencion;
        }
        conversacion.ModificadoPorId = usuarioAccionId;
        conversacion.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _auditoria.RegistrarEventoAsync(
            usuarioAccionId,
            "WHATSAPP_ASIGNAR_AGENTE",
            "ConversacionWhatsApp",
            conversacion.Id.ToString(),
            new { AgenteAnteriorId = anteriorAgenteId, AgenteNuevoId = agente?.Id },
            ct);

        var totalMensajes = await _context.MensajesWhatsApp
            .AsNoTracking()
            .CountAsync(m => m.ConversacionId == conversacion.Id && m.Activo, ct);

        return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(MapDetalle(conversacion, totalMensajes));
    }

    public async Task<ServiceResult<WhatsAppConversacionDetalleResponse>> CambiarModoAtencionAsync(
        Guid conversacionId,
        WhatsAppCambiarModoRequest request,
        Guid usuarioAccionId,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.ModoAtencion))
        {
            return ServiceResult<WhatsAppConversacionDetalleResponse>.Invalid("Modo de atención inválido.");
        }

        var conversacion = await _context.ConversacionesWhatsApp
            .Include(c => c.Cliente)
            .Include(c => c.AgenteAsignado)
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return ServiceResult<WhatsAppConversacionDetalleResponse>.NotFound("Conversación de WhatsApp no encontrada.");
        }

        var modoAnterior = conversacion.ModoAtencion;
        var ahora = DateTime.UtcNow;
        conversacion.ModoAtencion = request.ModoAtencion;
        if (request.ModoAtencion == ModoAtencionWhatsApp.Humano)
        {
            conversacion.FechaCambioModoHumano = ahora;
            if (conversacion.Estado == EstadoConversacionWhatsApp.Abierta)
            {
                conversacion.Estado = EstadoConversacionWhatsApp.EnAtencion;
            }
        }
        conversacion.ModificadoPorId = usuarioAccionId;
        conversacion.FechaModificacion = ahora;

        await _context.SaveChangesAsync(ct);

        await _auditoria.RegistrarEventoAsync(
            usuarioAccionId,
            "WHATSAPP_CAMBIAR_MODO",
            "ConversacionWhatsApp",
            conversacion.Id.ToString(),
            new { ModoAnterior = modoAnterior.ToString(), ModoNuevo = request.ModoAtencion.ToString() },
            ct);

        var totalMensajes = await _context.MensajesWhatsApp
            .AsNoTracking()
            .CountAsync(m => m.ConversacionId == conversacion.Id && m.Activo, ct);

        return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(MapDetalle(conversacion, totalMensajes));
    }

    public async Task<ServiceResult<WhatsAppConversacionDetalleResponse>> CambiarEstadoAsync(
        Guid conversacionId,
        WhatsAppCambiarEstadoRequest request,
        Guid usuarioAccionId,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.Estado))
        {
            return ServiceResult<WhatsAppConversacionDetalleResponse>.Invalid("Estado de conversación inválido.");
        }

        var conversacion = await _context.ConversacionesWhatsApp
            .Include(c => c.Cliente)
            .Include(c => c.AgenteAsignado)
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return ServiceResult<WhatsAppConversacionDetalleResponse>.NotFound("Conversación de WhatsApp no encontrada.");
        }

        var estadoAnterior = conversacion.Estado;
        conversacion.Estado = request.Estado;
        conversacion.ModificadoPorId = usuarioAccionId;
        conversacion.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _auditoria.RegistrarEventoAsync(
            usuarioAccionId,
            "WHATSAPP_CAMBIAR_ESTADO",
            "ConversacionWhatsApp",
            conversacion.Id.ToString(),
            new { EstadoAnterior = estadoAnterior.ToString(), EstadoNuevo = request.Estado.ToString() },
            ct);

        var totalMensajes = await _context.MensajesWhatsApp
            .AsNoTracking()
            .CountAsync(m => m.ConversacionId == conversacion.Id && m.Activo, ct);

        return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(MapDetalle(conversacion, totalMensajes));
    }

    public async Task<ServiceResult<WhatsAppConversacionDetalleResponse>> VincularClienteAsync(
        Guid conversacionId,
        WhatsAppVincularClienteRequest request,
        Guid usuarioAccionId,
        CancellationToken ct = default)
    {
        var conversacion = await _context.ConversacionesWhatsApp
            .Include(c => c.Cliente)
            .Include(c => c.AgenteAsignado)
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return ServiceResult<WhatsAppConversacionDetalleResponse>.NotFound("Conversación de WhatsApp no encontrada.");
        }

        Cliente? cliente = null;
        if (request.ClienteId.HasValue)
        {
            cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == request.ClienteId.Value && c.Activo, ct);

            if (cliente is null)
            {
                return ServiceResult<WhatsAppConversacionDetalleResponse>.Invalid("El cliente especificado no existe o está inactivo.");
            }
        }

        var anteriorClienteId = conversacion.ClienteId;
        conversacion.ClienteId = cliente?.Id;
        conversacion.Cliente = cliente;
        if (cliente is not null)
        {
            conversacion.AsociacionClienteAmbigua = false;
        }
        conversacion.ModificadoPorId = usuarioAccionId;
        conversacion.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _auditoria.RegistrarEventoAsync(
            usuarioAccionId,
            "WHATSAPP_VINCULAR_CLIENTE",
            "ConversacionWhatsApp",
            conversacion.Id.ToString(),
            new { ClienteAnteriorId = anteriorClienteId, ClienteNuevoId = cliente?.Id },
            ct);

        var totalMensajes = await _context.MensajesWhatsApp
            .AsNoTracking()
            .CountAsync(m => m.ConversacionId == conversacion.Id && m.Activo, ct);

        return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(MapDetalle(conversacion, totalMensajes));
    }

    public async Task<ServiceResult<WhatsAppMensajeResponse>> EnviarMensajeManualAsync(
        Guid conversacionId,
        WhatsAppEnviarMensajeRequest request,
        Guid usuarioAccionId,
        CancellationToken ct = default)
    {
        var settings = _settingsMonitor.CurrentValue;
        var errorConfig = settings.ValidarConfiguracionEnvio();
        if (errorConfig is not null)
        {
            return ServiceResult<WhatsAppMensajeResponse>.Invalid(errorConfig);
        }

        if (string.IsNullOrWhiteSpace(request.Texto))
        {
            return ServiceResult<WhatsAppMensajeResponse>.Invalid("El texto del mensaje es obligatorio.");
        }

        var textoLimpio = request.Texto.Trim();
        if (textoLimpio.Length > 4096)
        {
            return ServiceResult<WhatsAppMensajeResponse>.Invalid("El mensaje excede el máximo de 4096 caracteres permitido por WhatsApp.");
        }

        var conversacion = await _context.ConversacionesWhatsApp
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return ServiceResult<WhatsAppMensajeResponse>.NotFound("Conversación de WhatsApp no encontrada.");
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"manual:{conversacionId:N}:{Guid.NewGuid():N}"
            : request.IdempotencyKey.Trim();

        if (idempotencyKey.Length > 128)
        {
            return ServiceResult<WhatsAppMensajeResponse>.Invalid("La clave de idempotencia no puede exceder 128 caracteres.");
        }

        using var lockIdem = await LocksIdempotencia.AcquireAsync(idempotencyKey, ct);
        {
            // Prevención de envío duplicado local por IdempotencyKey (no implica idempotencia en servidores de Meta)
            var existente = await _context.MensajesWhatsApp
                .AsNoTracking()
                .Include(m => m.EnviadoPorUsuario)
                .FirstOrDefaultAsync(m => m.IdempotencyKey == idempotencyKey, ct);

            if (existente is not null)
            {
                return ServiceResult<WhatsAppMensajeResponse>.Success(MapMensaje(existente));
            }

            var ahora = DateTime.UtcNow;
            var mensaje = new MensajeWhatsApp
            {
                ConversacionId = conversacion.Id,
                IdempotencyKey = idempotencyKey,
                Direccion = DireccionMensajeWhatsApp.Saliente,
                Tipo = TipoMensajeWhatsApp.Texto,
                ContenidoTexto = textoLimpio,
                Estado = EstadoMensajeWhatsApp.Pendiente,
                FechaEventoUtc = ahora,
                FechaRecepcionUtc = ahora,
                EnviadoPorUsuarioId = usuarioAccionId,
                CreadoPorId = usuarioAccionId,
                FechaCreacion = ahora,
                Activo = true
            };

            var outbox = new OutboxMensajeWhatsApp
            {
                MensajeWhatsApp = mensaje,
                ConversacionId = conversacion.Id,
                DestinatarioE164 = conversacion.TelefonoE164,
                ContenidoTexto = textoLimpio,
                IdempotencyKey = idempotencyKey,
                Estado = EstadoOutboxWhatsApp.Pendiente,
                Intentos = 0,
                MaxIntentos = Math.Max(1, settings.MaxSendRetries),
                ProximoIntentoUtc = ahora,
                CreadoPorId = usuarioAccionId,
                FechaCreacion = ahora,
                Activo = true
            };

            _context.MensajesWhatsApp.Add(mensaje);
            _context.OutboxMensajesWhatsApp.Add(outbox);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Colisión concurrente por IdempotencyKey en BD: retornar el mensaje ya registrado.
                _context.Entry(outbox).State = EntityState.Detached;
                _context.Entry(mensaje).State = EntityState.Detached;

                var concurrente = await _context.MensajesWhatsApp
                    .AsNoTracking()
                    .Include(m => m.EnviadoPorUsuario)
                    .FirstOrDefaultAsync(m => m.IdempotencyKey == idempotencyKey, ct);

                if (concurrente is not null)
                {
                    return ServiceResult<WhatsAppMensajeResponse>.Success(MapMensaje(concurrente));
                }

                throw;
            }

            await _outboxService.ProcesarRegistroAsync(outbox.Id, ct);

            var actualizado = await _context.MensajesWhatsApp
                .AsNoTracking()
                .Include(m => m.EnviadoPorUsuario)
                .FirstAsync(m => m.Id == mensaje.Id, ct);

            // Auditoría sin registrar el texto completo del mensaje por protección de datos.
            await _auditoria.RegistrarEventoAsync(
                usuarioAccionId,
                "WHATSAPP_ENVIAR_MENSAJE",
                "MensajeWhatsApp",
                actualizado.Id.ToString(),
                new
                {
                    ConversacionId = conversacion.Id,
                    Estado = actualizado.Estado.ToString(),
                    ExternalMessageId = actualizado.ExternalMessageId,
                    LongitudTexto = textoLimpio.Length,
                    Proveedor = settings.Provider
                },
                ct);

            return ServiceResult<WhatsAppMensajeResponse>.Success(MapMensaje(actualizado));
        }
    }

    private static WhatsAppConversacionDetalleResponse MapDetalle(ConversacionWhatsApp c, int totalMensajes) => new(
        c.Id,
        c.TelefonoE164,
        c.WhatsAppId,
        c.NombrePerfilWhatsApp,
        c.ClienteId,
        c.Cliente?.NombreCompleto,
        c.Cliente?.NumeroDocumento,
        c.AsociacionClienteAmbigua,
        c.Estado,
        c.ModoAtencion,
        c.AgenteAsignadoId,
        c.AgenteAsignado?.NombreCompleto,
        c.FechaCreacion,
        c.FechaUltimoMensaje,
        c.FechaUltimoMensajeEntrante,
        c.FechaCambioModoHumano,
        c.FechaModificacion,
        totalMensajes);

    private static WhatsAppMensajeResponse MapMensaje(MensajeWhatsApp m) => new(
        m.Id,
        m.ConversacionId,
        m.ExternalMessageId,
        m.IdempotencyKey,
        m.Direccion,
        m.Tipo,
        m.ContenidoTexto,
        m.Estado,
        m.FechaEventoUtc,
        m.FechaRecepcionUtc,
        m.FechaUltimoEstadoUtc,
        m.EnviadoPorUsuarioId,
        m.EnviadoPorUsuario?.NombreCompleto,
        m.MediaId,
        m.MediaMimeType,
        m.MediaNombreArchivo,
        m.MetadataTecnicaJson,
        m.ErrorDetalle);
}
