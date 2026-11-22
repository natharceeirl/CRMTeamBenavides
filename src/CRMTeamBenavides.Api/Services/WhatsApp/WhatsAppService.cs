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
        bool incluirDocumentoCompleto = false,
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
        bool incluirDocumentoCompleto = false,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppConversacionDetalleResponse>> CambiarModoAtencionAsync(
        Guid conversacionId,
        WhatsAppCambiarModoRequest request,
        Guid usuarioAccionId,
        bool incluirDocumentoCompleto = false,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppConversacionDetalleResponse>> CambiarEstadoAsync(
        Guid conversacionId,
        WhatsAppCambiarEstadoRequest request,
        Guid usuarioAccionId,
        bool incluirDocumentoCompleto = false,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppConversacionDetalleResponse>> VincularClienteAsync(
        Guid conversacionId,
        WhatsAppVincularClienteRequest request,
        Guid usuarioAccionId,
        bool incluirDocumentoCompleto = false,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppConversacionDetalleResponse>> DesvincularClienteAsync(
        Guid conversacionId,
        Guid usuarioAccionId,
        bool incluirDocumentoCompleto = false,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppVinculacionClienteResponse>> ObtenerVinculacionClienteAsync(
        Guid conversacionId,
        bool incluirDocumentoCompleto = false,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppClientesCandidatosPaginadosResponse>> BuscarClientesParaVincularAsync(
        string? busqueda,
        int pagina = 1,
        int tamanoPagina = 15,
        CancellationToken ct = default);

    Task<ServiceResult<WhatsAppResultadoIdentificacionResponse>> ReintentarIdentificacionClienteAsync(
        Guid conversacionId,
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
    private readonly IWhatsAppClienteIdentificacionService _identificacionService;
    private readonly IAuditoriaService _auditoria;
    private readonly IOptionsMonitor<WhatsAppSettings> _settingsMonitor;

    public WhatsAppService(
        ApplicationDbContext context,
        IWhatsAppOutboxService outboxService,
        IWhatsAppClienteIdentificacionService identificacionService,
        IAuditoriaService auditoria,
        IOptionsMonitor<WhatsAppSettings> settingsMonitor)
    {
        _context = context;
        _outboxService = outboxService;
        _identificacionService = identificacionService;
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

            var clienteActivo = c.Cliente is { Activo: true } ? c.Cliente : null;
            return new WhatsAppConversacionResumenResponse(
                c.Id,
                c.TelefonoE164,
                c.WhatsAppId,
                c.NombrePerfilWhatsApp,
                clienteActivo?.Id,
                clienteActivo?.NombreCompleto,
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
        bool incluirDocumentoCompleto = false,
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

        return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(
            MapDetalle(conversacion, totalMensajes, incluirDocumentoCompleto));
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
        bool incluirDocumentoCompleto = false,
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

        return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(
            MapDetalle(conversacion, totalMensajes, incluirDocumentoCompleto));
    }

    public async Task<ServiceResult<WhatsAppConversacionDetalleResponse>> CambiarModoAtencionAsync(
        Guid conversacionId,
        WhatsAppCambiarModoRequest request,
        Guid usuarioAccionId,
        bool incluirDocumentoCompleto = false,
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

        return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(
            MapDetalle(conversacion, totalMensajes, incluirDocumentoCompleto));
    }

    public async Task<ServiceResult<WhatsAppConversacionDetalleResponse>> CambiarEstadoAsync(
        Guid conversacionId,
        WhatsAppCambiarEstadoRequest request,
        Guid usuarioAccionId,
        bool incluirDocumentoCompleto = false,
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

        return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(
            MapDetalle(conversacion, totalMensajes, incluirDocumentoCompleto));
    }

    public async Task<ServiceResult<WhatsAppConversacionDetalleResponse>> VincularClienteAsync(
        Guid conversacionId,
        WhatsAppVincularClienteRequest request,
        Guid usuarioAccionId,
        bool incluirDocumentoCompleto = false,
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
        var nuevoClienteId = cliente?.Id;

        // Evitar escrituras ni auditoría espuria cuando no existe cambio real (M-04).
        if (anteriorClienteId == nuevoClienteId
            && (nuevoClienteId is null || !conversacion.AsociacionClienteAmbigua))
        {
            var totalSinCambios = await _context.MensajesWhatsApp
                .AsNoTracking()
                .CountAsync(m => m.ConversacionId == conversacion.Id && m.Activo, ct);

            return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(
                MapDetalle(conversacion, totalSinCambios, incluirDocumentoCompleto));
        }

        conversacion.ClienteId = nuevoClienteId;
        conversacion.Cliente = cliente;

        if (cliente is not null)
        {
            conversacion.AsociacionClienteAmbigua = false;
        }
        else
        {
            // Al desvincular, recalcular si el teléfono sigue teniendo múltiples candidatos activos (M-04).
            var ident = await _identificacionService.IdentificarPorTelefonoAsync(conversacion.TelefonoE164, ct);
            conversacion.AsociacionClienteAmbigua = ident.EsAmbiguo;
        }

        conversacion.ModificadoPorId = usuarioAccionId;
        conversacion.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        var accion = cliente is null ? "WHATSAPP_DESVINCULAR_CLIENTE" : "WHATSAPP_VINCULAR_CLIENTE";
        await _auditoria.RegistrarEventoAsync(
            usuarioAccionId,
            accion,
            "ConversacionWhatsApp",
            conversacion.Id.ToString(),
            new
            {
                ClienteAnteriorId = anteriorClienteId,
                ClienteNuevoId = nuevoClienteId,
                AsociacionClienteAmbigua = conversacion.AsociacionClienteAmbigua
            },
            ct);

        var totalMensajes = await _context.MensajesWhatsApp
            .AsNoTracking()
            .CountAsync(m => m.ConversacionId == conversacion.Id && m.Activo, ct);

        return ServiceResult<WhatsAppConversacionDetalleResponse>.Success(
            MapDetalle(conversacion, totalMensajes, incluirDocumentoCompleto));
    }

    public Task<ServiceResult<WhatsAppConversacionDetalleResponse>> DesvincularClienteAsync(
        Guid conversacionId,
        Guid usuarioAccionId,
        bool incluirDocumentoCompleto = false,
        CancellationToken ct = default) =>
        VincularClienteAsync(
            conversacionId,
            new WhatsAppVincularClienteRequest(null),
            usuarioAccionId,
            incluirDocumentoCompleto,
            ct);

    public async Task<ServiceResult<WhatsAppVinculacionClienteResponse>> ObtenerVinculacionClienteAsync(
        Guid conversacionId,
        bool incluirDocumentoCompleto = false,
        CancellationToken ct = default)
    {
        var conversacion = await _context.ConversacionesWhatsApp
            .AsNoTracking()
            .Include(c => c.Cliente)
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return ServiceResult<WhatsAppVinculacionClienteResponse>.NotFound("Conversación de WhatsApp no encontrada.");
        }

        var codigoPais = _settingsMonitor.CurrentValue.DefaultCountryCode;
        var clienteVinculadoInactivo = conversacion.ClienteId.HasValue && conversacion.Cliente is not { Activo: true };
        var estaVinculada = conversacion.ClienteId.HasValue && conversacion.Cliente is { Activo: true };
        bool? telefonoCoincide = null;
        WhatsAppClienteVinculadoBasicoResponse? clienteDto = null;

        if (estaVinculada)
        {
            var cli = conversacion.Cliente!;
            var cantidadVehiculos = await _context.Vehiculos
                .AsNoTracking()
                .CountAsync(v => v.ClienteId == cli.Id && v.Activo, ct);

            var docCrudo = !string.IsNullOrWhiteSpace(cli.NumeroDocumento)
                ? cli.NumeroDocumento.Trim()
                : cli.DocumentoIdentidad?.Trim();

            var telefonosCliente = WhatsAppPhoneNormalizer.ExtraerTelefonosNormalizadosE164(cli.Telefono, codigoPais);
            telefonoCoincide = telefonosCliente.Contains(conversacion.TelefonoE164, StringComparer.Ordinal);

            clienteDto = new WhatsAppClienteVinculadoBasicoResponse(
                Id: cli.Id,
                NombreCompleto: cli.NombreCompleto,
                RazonSocial: cli.RazonSocial,
                TipoDocumento: cli.TipoDocumento,
                DocumentoVisible: incluirDocumentoCompleto
                    ? docCrudo
                    : WhatsAppPhoneNormalizer.EnmascararDocumento(docCrudo),
                Telefono: incluirDocumentoCompleto
                    ? cli.Telefono
                    : WhatsAppPhoneNormalizer.EnmascararTelefono(cli.Telefono),
                CantidadVehiculosActivos: cantidadVehiculos);
        }

        var origenVinculacion = "SinVincular";
        if (estaVinculada)
        {
            var convIdStr = conversacion.Id.ToString();
            var ultimoEventoVinculo = await _context.EventosAuditoria
                .AsNoTracking()
                .Where(e =>
                    e.Entidad == "ConversacionWhatsApp"
                    && e.EntidadId == convIdStr
                    && (e.Accion == "WHATSAPP_VINCULAR_CLIENTE"
                        || e.Accion == "WHATSAPP_DESVINCULAR_CLIENTE"
                        || e.Accion == "WHATSAPP_REIDENTIFICAR_CLIENTE"))
                .OrderByDescending(e => e.Fecha)
                .Select(e => e.Accion)
                .FirstOrDefaultAsync(ct);

            origenVinculacion = ultimoEventoVinculo == "WHATSAPP_VINCULAR_CLIENTE"
                ? "Manual"
                : "Automatico";
        }

        var requiereRevision = conversacion.AsociacionClienteAmbigua
            || clienteVinculadoInactivo
            || (estaVinculada && telefonoCoincide == false);

        return ServiceResult<WhatsAppVinculacionClienteResponse>.Success(new WhatsAppVinculacionClienteResponse(
            ConversacionId: conversacion.Id,
            TelefonoE164: conversacion.TelefonoE164,
            EstaVinculada: estaVinculada,
            AsociacionClienteAmbigua: conversacion.AsociacionClienteAmbigua,
            ClienteVinculadoInactivo: clienteVinculadoInactivo,
            TelefonoCoincideConCliente: telefonoCoincide,
            RequiereRevisionVinculacion: requiereRevision,
            OrigenVinculacion: origenVinculacion,
            Cliente: clienteDto));
    }

    public async Task<ServiceResult<WhatsAppClientesCandidatosPaginadosResponse>> BuscarClientesParaVincularAsync(
        string? busqueda,
        int pagina = 1,
        int tamanoPagina = 15,
        CancellationToken ct = default)
    {
        var termino = busqueda?.Trim();
        if (string.IsNullOrWhiteSpace(termino) || termino.Length < 3)
        {
            return ServiceResult<WhatsAppClientesCandidatosPaginadosResponse>.Invalid(
                "Debe ingresar un criterio de búsqueda de al menos 3 caracteres.");
        }

        var paginaSegura = Math.Max(1, pagina);
        var tamanoSeguro = Math.Clamp(tamanoPagina, 1, 25);
        var codigoPais = _settingsMonitor.CurrentValue.DefaultCountryCode;
        var termLower = termino.ToLowerInvariant();

        var normTelBusqueda = WhatsAppPhoneNormalizer.NormalizarE164(termino, codigoPais);
        var telObjetivoE164 = normTelBusqueda.EsValido ? normTelBusqueda.TelefonoE164 : null;
        var digitosPrefiltroTel = normTelBusqueda.EsValido && !string.IsNullOrEmpty(normTelBusqueda.WhatsAppId)
            ? (normTelBusqueda.WhatsAppId.StartsWith("51", StringComparison.Ordinal) && normTelBusqueda.WhatsAppId.Length > 2
                ? normTelBusqueda.WhatsAppId[2..]
                : normTelBusqueda.WhatsAppId)
            : null;

        // Filtrado en SQL antes de materializar para jamás cargar el padrón completo en memoria (H-03.C / M-05).
        var baseQuery = _context.Clientes
            .AsNoTracking()
            .Where(c => c.Activo);

        if (digitosPrefiltroTel is not null)
        {
            baseQuery = baseQuery.Where(c =>
                c.NombreCompleto.ToLower().Contains(termLower)
                || (c.RazonSocial != null && c.RazonSocial.ToLower().Contains(termLower))
                || (c.NumeroDocumento != null && c.NumeroDocumento.ToLower() == termLower)
                || (c.DocumentoIdentidad != null && c.DocumentoIdentidad.ToLower() == termLower)
                || (c.Telefono != null && c.Telefono
                    .Replace(" ", "")
                    .Replace("-", "")
                    .Replace("(", "")
                    .Replace(")", "")
                    .Replace(".", "")
                    .Replace("+", "")
                    .Replace("\t", "")
                    .Contains(digitosPrefiltroTel)));
        }
        else
        {
            baseQuery = baseQuery.Where(c =>
                c.NombreCompleto.ToLower().Contains(termLower)
                || (c.RazonSocial != null && c.RazonSocial.ToLower().Contains(termLower))
                || (c.NumeroDocumento != null && c.NumeroDocumento.ToLower() == termLower)
                || (c.DocumentoIdentidad != null && c.DocumentoIdentidad.ToLower() == termLower));
        }

        List<WhatsAppClienteCandidatoResponse> items;
        int totalRegistros;

        if (telObjetivoE164 is not null)
        {
            // Solo sobre el subconjunto ya prefiltrado en SQL verificamos igualdad E.164 exacta cuando coincidió por teléfono.
            var candidatosSql = await baseQuery
                .OrderBy(c => c.NombreCompleto)
                .Select(c => new
                {
                    c.Id,
                    c.NombreCompleto,
                    c.RazonSocial,
                    c.TipoDocumento,
                    c.NumeroDocumento,
                    c.DocumentoIdentidad,
                    c.Telefono,
                    CantidadVehiculos = c.Vehiculos.Count(v => v.Activo)
                })
                .Take(200)
                .ToListAsync(ct);

            var verificados = candidatosSql
                .Where(c =>
                    c.NombreCompleto.Contains(termino, StringComparison.OrdinalIgnoreCase)
                    || (c.RazonSocial is not null && c.RazonSocial.Contains(termino, StringComparison.OrdinalIgnoreCase))
                    || (c.NumeroDocumento is not null && c.NumeroDocumento.Equals(termino, StringComparison.OrdinalIgnoreCase))
                    || (c.DocumentoIdentidad is not null && c.DocumentoIdentidad.Equals(termino, StringComparison.OrdinalIgnoreCase))
                    || WhatsAppPhoneNormalizer.ExtraerTelefonosNormalizadosE164(c.Telefono, codigoPais)
                        .Contains(telObjetivoE164, StringComparer.Ordinal))
                .ToList();

            totalRegistros = verificados.Count;
            items = verificados
                .Skip((paginaSegura - 1) * tamanoSeguro)
                .Take(tamanoSeguro)
                .Select(c =>
                {
                    var docCrudo = !string.IsNullOrWhiteSpace(c.NumeroDocumento)
                        ? c.NumeroDocumento.Trim()
                        : c.DocumentoIdentidad?.Trim();

                    return new WhatsAppClienteCandidatoResponse(
                        Id: c.Id,
                        NombreCompleto: c.NombreCompleto,
                        RazonSocial: c.RazonSocial,
                        TipoDocumento: c.TipoDocumento,
                        DocumentoEnmascarado: WhatsAppPhoneNormalizer.EnmascararDocumento(docCrudo),
                        TelefonoEnmascarado: WhatsAppPhoneNormalizer.EnmascararTelefono(c.Telefono),
                        CantidadVehiculosActivos: c.CantidadVehiculos);
                })
                .ToList();
        }
        else
        {
            totalRegistros = await baseQuery.CountAsync(ct);
            var paginaSql = await baseQuery
                .OrderBy(c => c.NombreCompleto)
                .Skip((paginaSegura - 1) * tamanoSeguro)
                .Take(tamanoSeguro)
                .Select(c => new
                {
                    c.Id,
                    c.NombreCompleto,
                    c.RazonSocial,
                    c.TipoDocumento,
                    c.NumeroDocumento,
                    c.DocumentoIdentidad,
                    c.Telefono,
                    CantidadVehiculos = c.Vehiculos.Count(v => v.Activo)
                })
                .ToListAsync(ct);

            items = paginaSql.Select(c =>
            {
                var docCrudo = !string.IsNullOrWhiteSpace(c.NumeroDocumento)
                    ? c.NumeroDocumento.Trim()
                    : c.DocumentoIdentidad?.Trim();

                return new WhatsAppClienteCandidatoResponse(
                    Id: c.Id,
                    NombreCompleto: c.NombreCompleto,
                    RazonSocial: c.RazonSocial,
                    TipoDocumento: c.TipoDocumento,
                    DocumentoEnmascarado: WhatsAppPhoneNormalizer.EnmascararDocumento(docCrudo),
                    TelefonoEnmascarado: WhatsAppPhoneNormalizer.EnmascararTelefono(c.Telefono),
                    CantidadVehiculosActivos: c.CantidadVehiculos);
            }).ToList();
        }

        return ServiceResult<WhatsAppClientesCandidatosPaginadosResponse>.Success(
            new WhatsAppClientesCandidatosPaginadosResponse(items, totalRegistros, paginaSegura, tamanoSeguro));
    }

    public async Task<ServiceResult<WhatsAppResultadoIdentificacionResponse>> ReintentarIdentificacionClienteAsync(
        Guid conversacionId,
        Guid usuarioAccionId,
        CancellationToken ct = default)
    {
        var conversacion = await _context.ConversacionesWhatsApp
            .Include(c => c.Cliente)
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return ServiceResult<WhatsAppResultadoIdentificacionResponse>.NotFound("Conversación de WhatsApp no encontrada.");
        }

        // Nunca sobrescribir un vínculo activo (manual o previo); sí permitir reemplazar si el cliente anterior fue desactivado (M-03).
        if (conversacion.ClienteId.HasValue && conversacion.Cliente is { Activo: true })
        {
            return ServiceResult<WhatsAppResultadoIdentificacionResponse>.Conflict(
                "La conversación ya está vinculada a un cliente activo. Desvincule primero si desea re-ejecutar la identificación automática.");
        }

        var clienteAnteriorInactivoId = conversacion.ClienteId;
        var identificacion = await _identificacionService.IdentificarPorTelefonoAsync(conversacion.TelefonoE164, ct);

        conversacion.ClienteId = identificacion.EsVinculableAutomaticamente ? identificacion.ClienteId : null;
        conversacion.Cliente = null;
        conversacion.AsociacionClienteAmbigua = identificacion.EsAmbiguo;
        conversacion.ModificadoPorId = usuarioAccionId;
        conversacion.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _auditoria.RegistrarEventoAsync(
            usuarioAccionId,
            "WHATSAPP_REIDENTIFICAR_CLIENTE",
            "ConversacionWhatsApp",
            conversacion.Id.ToString(),
            new
            {
                ClienteAnteriorInactivoId = clienteAnteriorInactivoId,
                EstadoIdentificacion = identificacion.Estado.ToString(),
                TotalCandidatos = identificacion.TotalCandidatos,
                ClienteVinculadoId = conversacion.ClienteId
            },
            ct);

        return ServiceResult<WhatsAppResultadoIdentificacionResponse>.Success(
            new WhatsAppResultadoIdentificacionResponse(
                ConversacionId: conversacion.Id,
                EstadoIdentificacion: identificacion.Estado.ToString(),
                VinculadaAutomaticamente: identificacion.EsVinculableAutomaticamente,
                AsociacionAmbigua: identificacion.EsAmbiguo,
                ClienteId: conversacion.ClienteId,
                TotalCandidatos: identificacion.TotalCandidatos,
                Motivo: identificacion.Motivo));
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

    private static WhatsAppConversacionDetalleResponse MapDetalle(
        ConversacionWhatsApp c,
        int totalMensajes,
        bool incluirDocumentoCompleto)
    {
        var clienteActivo = c.Cliente is { Activo: true } ? c.Cliente : null;
        var docCrudo = clienteActivo is null
            ? null
            : (!string.IsNullOrWhiteSpace(clienteActivo.NumeroDocumento)
                ? clienteActivo.NumeroDocumento.Trim()
                : clienteActivo.DocumentoIdentidad?.Trim());

        var docVisible = incluirDocumentoCompleto
            ? docCrudo
            : WhatsAppPhoneNormalizer.EnmascararDocumento(docCrudo);

        return new WhatsAppConversacionDetalleResponse(
            c.Id,
            c.TelefonoE164,
            c.WhatsAppId,
            c.NombrePerfilWhatsApp,
            clienteActivo?.Id,
            clienteActivo?.NombreCompleto,
            docVisible,
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
    }

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
