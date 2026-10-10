using CRMTeamBenavides.Api.Features.Aprobaciones;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class AprobacionService : IAprobacionService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public AprobacionService(ApplicationDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<SolicitudAprobacionResponse>> ObtenerPendientesAsync(CancellationToken ct = default)
    {
        return await ObtenerHistorialAsync(estado: EstadoAprobacionGerencia.Pendiente, ct: ct);
    }

    public async Task<List<SolicitudAprobacionResponse>> ObtenerHistorialAsync(
        string? entidad = null,
        string? tipo = null,
        EstadoAprobacionGerencia? estado = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        CancellationToken ct = default)
    {
        var query = _context.SolicitudesAprobacion
            .AsNoTracking()
            .Include(s => s.UsuarioSolicitante)
            .Include(s => s.UsuarioAprobador)
            .Where(s => s.Activo)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(entidad))
        {
            var ent = entidad.Trim();
            query = query.Where(s => s.Entidad.ToLower() == ent.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(tipo))
        {
            var tip = tipo.Trim();
            query = query.Where(s => s.Tipo.ToLower() == tip.ToLower());
        }

        if (estado.HasValue)
        {
            query = query.Where(s => s.Estado == estado.Value);
        }

        if (fechaDesde.HasValue)
        {
            query = query.Where(s => s.FechaSolicitud >= fechaDesde.Value);
        }

        if (fechaHasta.HasValue)
        {
            query = query.Where(s => s.FechaSolicitud <= fechaHasta.Value);
        }

        var lista = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync(ct);

        return lista.Select(MapToResponse).ToList();
    }

    public async Task<ServiceResult<SolicitudAprobacionResponse>> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var solicitud = await _context.SolicitudesAprobacion
            .AsNoTracking()
            .Include(s => s.UsuarioSolicitante)
            .Include(s => s.UsuarioAprobador)
            .FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);

        if (solicitud is null)
        {
            return ServiceResult<SolicitudAprobacionResponse>.NotFound();
        }

        return ServiceResult<SolicitudAprobacionResponse>.Success(MapToResponse(solicitud));
    }

    public async Task<ServiceResult<SolicitudAprobacionResponse>> CrearSolicitudAsync(
        RegistrarSolicitudAprobacionRequest request,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Entidad) || string.IsNullOrWhiteSpace(request.EntidadId))
        {
            return ServiceResult<SolicitudAprobacionResponse>.Invalid("Entidad y EntidadId son obligatorios.");
        }

        if (string.IsNullOrWhiteSpace(request.DetalleCambio))
        {
            return ServiceResult<SolicitudAprobacionResponse>.Invalid("El detalle del cambio es obligatorio.");
        }

        string? solicitanteNombre = null;
        if (usuarioId.HasValue)
        {
            solicitanteNombre = await _context.Usuarios
                .Where(u => u.Id == usuarioId.Value)
                .Select(u => u.NombreCompleto)
                .FirstOrDefaultAsync(ct);
        }

        var solicitud = new SolicitudAprobacion
        {
            Id = Guid.NewGuid(),
            Tipo = string.IsNullOrWhiteSpace(request.Tipo) ? "CambioPrecio" : request.Tipo.Trim(),
            Entidad = request.Entidad.Trim(),
            EntidadId = request.EntidadId.Trim(),
            UsuarioSolicitanteId = usuarioId,
            UsuarioSolicitanteNombre = solicitanteNombre,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoAprobacionGerencia.Pendiente,
            DetalleCambio = request.DetalleCambio.Trim(),
            ValorAnterior = request.ValorAnterior,
            ValorSolicitado = request.ValorSolicitado,
            Motivo = request.Motivo?.Trim(),
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        var clave = ObtenerClaveObjetivo(solicitud);
        await RetirarSolicitudesPendientesPorObjetivoAsync(
            _context,
            solicitud.Entidad,
            solicitud.EntidadId,
            clave,
            "Superada automáticamente por nueva solicitud para el mismo objetivo.",
            ct);

        _context.SolicitudesAprobacion.Add(solicitud);

        // Actualizar la entidad vinculada a Pendiente
        await ActualizarEstadoEntidadVinculadaAsync(solicitud.Entidad, solicitud.EntidadId, EstadoAprobacionGerencia.Pendiente, request.Motivo, null, ct);

        await _context.SaveChangesAsync(ct);

        await _auditoriaService.RegistrarEventoAsync(
            usuarioId,
            "SolicitudAprobacionCreada",
            solicitud.Entidad,
            solicitud.EntidadId,
            new
            {
                SolicitudId = solicitud.Id,
                solicitud.Tipo,
                solicitud.DetalleCambio,
                solicitud.ValorAnterior,
                solicitud.ValorSolicitado,
                solicitud.Motivo
            });

        return ServiceResult<SolicitudAprobacionResponse>.Success(MapToResponse(solicitud));
    }

    public async Task<ServiceResult<SolicitudAprobacionResponse>> ResolverSolicitudAsync(
        Guid id,
        ResolverSolicitudAprobacionRequest request,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        if (request.Estado != EstadoAprobacionGerencia.Aprobado && request.Estado != EstadoAprobacionGerencia.Rechazado)
        {
            return ServiceResult<SolicitudAprobacionResponse>.Invalid("El estado de resolución debe ser Aprobado o Rechazado.");
        }

        var solicitud = await _context.SolicitudesAprobacion
            .Include(s => s.UsuarioSolicitante)
            .FirstOrDefaultAsync(s => s.Id == id && s.Activo, ct);

        if (solicitud is null)
        {
            return ServiceResult<SolicitudAprobacionResponse>.NotFound();
        }

        if (solicitud.Estado != EstadoAprobacionGerencia.Pendiente)
        {
            return ServiceResult<SolicitudAprobacionResponse>.Invalid($"La solicitud ya fue resuelta previamente con estado '{solicitud.Estado}'.");
        }

        string? aprobadorNombre = null;
        if (usuarioId.HasValue)
        {
            aprobadorNombre = await _context.Usuarios
                .Where(u => u.Id == usuarioId.Value)
                .Select(u => u.NombreCompleto)
                .FirstOrDefaultAsync(ct);
        }

        solicitud.Estado = request.Estado;
        solicitud.UsuarioAprobadorId = usuarioId;
        solicitud.UsuarioAprobadorNombre = aprobadorNombre;
        solicitud.FechaRespuesta = DateTime.UtcNow;
        solicitud.ObservacionesRespuesta = request.Observaciones?.Trim();
        solicitud.FechaModificacion = DateTime.UtcNow;

        // Retirar cualquier otra solicitud obsoleta que haya quedado pendiente para este mismo objetivo
        var claveObjetivo = ObtenerClaveObjetivo(solicitud);
        var otrasPendientes = await _context.SolicitudesAprobacion
            .Where(s => s.Activo && s.Id != solicitud.Id && s.Entidad == solicitud.Entidad && s.EntidadId == solicitud.EntidadId && s.Estado == EstadoAprobacionGerencia.Pendiente)
            .ToListAsync(ct);

        foreach (var otra in otrasPendientes)
        {
            if (ObtenerClaveObjetivo(otra) == claveObjetivo)
            {
                otra.Activo = false;
                otra.ObservacionesRespuesta = $"Retirada automáticamente tras resolución de solicitud {solicitud.Id}.";
                otra.FechaRespuesta = DateTime.UtcNow;
                otra.FechaModificacion = DateTime.UtcNow;
            }
        }

        // Actualizar la entidad origen (OrdenServicio, Venta o PedidoLima)
        await ActualizarEstadoEntidadVinculadaAsync(solicitud.Entidad, solicitud.EntidadId, request.Estado, request.Observaciones, usuarioId, ct);

        await _context.SaveChangesAsync(ct);

        await _auditoriaService.RegistrarEventoAsync(
            usuarioId,
            request.Estado == EstadoAprobacionGerencia.Aprobado ? "AprobacionConcedida" : "AprobacionRechazada",
            solicitud.Entidad,
            solicitud.EntidadId,
            new
            {
                SolicitudId = solicitud.Id,
                Estado = request.Estado.ToString(),
                solicitud.Tipo,
                solicitud.DetalleCambio,
                solicitud.ValorAnterior,
                solicitud.ValorSolicitado,
                Observaciones = request.Observaciones?.Trim()
            });

        return ServiceResult<SolicitudAprobacionResponse>.Success(MapToResponse(solicitud));
    }

    public static Guid? ObtenerDetalleId(SolicitudAprobacion s)
    {
        var detalle = s.DetalleCambio ?? string.Empty;
        var idx = detalle.IndexOf("[Detalle:", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = idx + 9;
            var end = detalle.IndexOf(']', start);
            if (end > start)
            {
                var strGuid = detalle.Substring(start, end - start).Trim();
                if (Guid.TryParse(strGuid, out var guid))
                {
                    return guid;
                }
            }
        }
        return null;
    }

    public static Guid? ObtenerSuperaId(SolicitudAprobacion s)
    {
        var detalle = s.DetalleCambio ?? string.Empty;
        var idx = detalle.IndexOf("[Supera:", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = idx + 8;
            var end = detalle.IndexOf(']', start);
            if (end > start)
            {
                var strGuid = detalle.Substring(start, end - start).Trim();
                if (Guid.TryParse(strGuid, out var guid))
                {
                    return guid;
                }
            }
        }
        return null;
    }

    public static string? ObtenerClaveObjetivoExplicita(SolicitudAprobacion s)
    {
        var detalle = s.DetalleCambio ?? string.Empty;
        var idx = detalle.IndexOf("[Objetivo:", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = idx + 10;
            var end = detalle.IndexOf(']', start);
            if (end > start)
            {
                var clave = detalle.Substring(start, end - start).Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(clave))
                {
                    return clave;
                }
            }
        }
        return null;
    }

    public static string? ObtenerNombreItem(SolicitudAprobacion s)
    {
        var detalle = s.DetalleCambio ?? string.Empty;
        var idxStart = detalle.IndexOf('\'');
        if (idxStart >= 0)
        {
            var idxEnd = detalle.IndexOf('\'', idxStart + 1);
            if (idxEnd > idxStart)
            {
                var nombreItem = detalle.Substring(idxStart + 1, idxEnd - idxStart - 1).Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(nombreItem))
                {
                    return nombreItem;
                }
            }
        }
        return null;
    }

    public static string ObtenerClaveObjetivo(SolicitudAprobacion s)
    {
        var objExplicito = ObtenerClaveObjetivoExplicita(s);
        if (!string.IsNullOrEmpty(objExplicito))
        {
            return objExplicito;
        }

        var detalleId = ObtenerDetalleId(s);
        if (detalleId.HasValue)
        {
            return $"detalle_{detalleId.Value}".ToLowerInvariant();
        }

        var nombreItem = ObtenerNombreItem(s);
        if (!string.IsNullOrEmpty(nombreItem))
        {
            var tipoItem = string.IsNullOrWhiteSpace(s.Tipo) ? "ITEM" : s.Tipo.Trim();
            return $"{tipoItem}_{nombreItem}".ToLowerInvariant();
        }

        var entidad = (s.Entidad ?? string.Empty).Trim().ToUpperInvariant();
        if (entidad == "VENTA" || entidad == "VENTAS")
        {
            return "entidad_venta";
        }

        var detalle = (s.DetalleCambio ?? string.Empty).Trim().ToLowerInvariant();
        if (detalle.StartsWith("solicitud de aprobación para os") || detalle.StartsWith("solicitud de aprobacion para os"))
        {
            return "entidad_ordenservicio";
        }

        if (detalle.StartsWith("modificación de precio en pedido lima") || detalle.StartsWith("modificacion de precio en pedido lima"))
        {
            return "entidad_pedidolima";
        }

        var superaId = ObtenerSuperaId(s);
        if (superaId.HasValue)
        {
            return $"solicitud_{superaId.Value}".ToLowerInvariant();
        }

        return $"solicitud_{s.Id}".ToLowerInvariant();
    }

    public static async Task RetirarSolicitudesPendientesPorObjetivoAsync(
        ApplicationDbContext context,
        string entidad,
        string entidadId,
        string claveObjetivo,
        string motivoRetiro,
        CancellationToken ct = default)
    {
        var normalizadoEnt = entidad.Trim().ToUpperInvariant();
        var normalizadoEntNombre = (normalizadoEnt == "ORDENSERVICIO" || normalizadoEnt == "ORDENESSERVICIO") ? "ORDENSERVICIO"
            : (normalizadoEnt == "VENTA" || normalizadoEnt == "VENTAS") ? "VENTA"
            : (normalizadoEnt == "PEDIDOLIMA" || normalizadoEnt == "PEDIDOSLIMA") ? "PEDIDOLIMA"
            : normalizadoEnt;

        var solicitudes = await context.SolicitudesAprobacion
            .Where(s => s.Activo && s.EntidadId == entidadId && s.Estado == EstadoAprobacionGerencia.Pendiente)
            .ToListAsync(ct);

        foreach (var sol in solicitudes)
        {
            var solEnt = sol.Entidad.Trim().ToUpperInvariant();
            var solNorm = (solEnt == "ORDENSERVICIO" || solEnt == "ORDENESSERVICIO") ? "ORDENSERVICIO"
                : (solEnt == "VENTA" || solEnt == "VENTAS") ? "VENTA"
                : (solEnt == "PEDIDOLIMA" || solEnt == "PEDIDOSLIMA") ? "PEDIDOLIMA"
                : solEnt;

            if (solNorm == normalizadoEntNombre && ObtenerClaveObjetivo(sol) == claveObjetivo)
            {
                sol.Activo = false;
                sol.ObservacionesRespuesta = motivoRetiro;
                sol.FechaRespuesta = DateTime.UtcNow;
                sol.FechaModificacion = DateTime.UtcNow;
            }
        }
    }

    public static EstadoAprobacionGerencia CalcularEstadoAgregado(
        IEnumerable<SolicitudAprobacion> solicitudes,
        EstadoAprobacionGerencia? estadoFallback = null)
    {
        var solicitudesVigentes = solicitudes
            .GroupBy(ObtenerClaveObjetivo)
            .Select(g => g
                .OrderByDescending(s => s.FechaSolicitud)
                .ThenByDescending(s => s.FechaCreacion)
                .ThenByDescending(s => s.Id)
                .First())
            .ToList();

        var estados = solicitudesVigentes.Select(s => s.Estado).ToList();
        if (estados.Count == 0)
        {
            return estadoFallback ?? EstadoAprobacionGerencia.NoAplica;
        }

        if (estados.Any(e => e == EstadoAprobacionGerencia.Rechazado))
        {
            return EstadoAprobacionGerencia.Rechazado;
        }

        if (estados.Any(e => e == EstadoAprobacionGerencia.Pendiente))
        {
            return EstadoAprobacionGerencia.Pendiente;
        }

        if (estados.Any(e => e == EstadoAprobacionGerencia.Aprobado))
        {
            return EstadoAprobacionGerencia.Aprobado;
        }

        return EstadoAprobacionGerencia.NoAplica;
    }

    private async Task ActualizarEstadoEntidadVinculadaAsync(
        string entidad,
        string entidadIdStr,
        EstadoAprobacionGerencia nuevoEstado,
        string? observaciones,
        Guid? usuarioId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(entidadIdStr, out var entidadId)) return;

        var entUpper = entidad.Trim().ToUpperInvariant();
        var normalizadoEnt = (entUpper == "ORDENSERVICIO" || entUpper == "ORDENESSERVICIO") ? "ORDENSERVICIO"
            : (entUpper == "VENTA" || entUpper == "VENTAS") ? "VENTA"
            : (entUpper == "PEDIDOLIMA" || entUpper == "PEDIDOSLIMA") ? "PEDIDOLIMA"
            : entUpper;

        // Consultar todas las solicitudes activas de la BD para esta entidad
        var solicitudesBd = await _context.SolicitudesAprobacion
            .Where(s => s.Activo && s.EntidadId == entidadIdStr)
            .ToListAsync(ct);

        // Considerar también las solicitudes locales rastreadas en DbContext (en memoria / Added / Modified)
        var solicitudesLocales = _context.SolicitudesAprobacion.Local
            .Where(s => s.Activo && s.EntidadId == entidadIdStr)
            .ToList();

        var todasSolicitudes = solicitudesBd
            .UnionBy(solicitudesLocales, s => s.Id)
            .Where(s =>
            {
                var sUpper = s.Entidad.Trim().ToUpperInvariant();
                var sNorm = (sUpper == "ORDENSERVICIO" || sUpper == "ORDENESSERVICIO") ? "ORDENSERVICIO"
                    : (sUpper == "VENTA" || sUpper == "VENTAS") ? "VENTA"
                    : (sUpper == "PEDIDOLIMA" || sUpper == "PEDIDOSLIMA") ? "PEDIDOLIMA"
                    : sUpper;
                return sNorm == normalizadoEnt;
            })
            .ToList();

        if (normalizadoEnt == "ORDENSERVICIO")
        {
            var orden = await _context.OrdenesServicio
                .Include(o => o.Detalles)
                .FirstOrDefaultAsync(o => o.Id == entidadId, ct);

            if (orden != null)
            {
                var detallesActivos = orden.Detalles.Where(d => d.Activo).ToList();
                var idsActivos = new HashSet<Guid>(detallesActivos.Select(d => d.Id));
                var prodIds = detallesActivos.Where(d => d.ProductoId.HasValue).Select(d => d.ProductoId!.Value).Distinct().ToList();
                var servIds = detallesActivos.Where(d => d.ServicioId.HasValue).Select(d => d.ServicioId!.Value).Distinct().ToList();

                var nombresActivos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (prodIds.Count > 0)
                {
                    var pNombres = await _context.Productos.Where(p => prodIds.Contains(p.Id)).Select(p => p.Nombre).ToListAsync(ct);
                    foreach (var n in pNombres) nombresActivos.Add(n.Trim().ToLowerInvariant());
                }
                if (servIds.Count > 0)
                {
                    var sNombres = await _context.Servicios.Where(s => servIds.Contains(s.Id)).Select(s => s.Nombre).ToListAsync(ct);
                    foreach (var n in sNombres) nombresActivos.Add(n.Trim().ToLowerInvariant());
                }
                foreach (var d in detallesActivos.Where(d => d.TipoItem == TipoItemServicio.ManoDeObra || d.TipoItem == TipoItemServicio.Terceros))
                {
                    if (!string.IsNullOrWhiteSpace(d.Descripcion))
                    {
                        nombresActivos.Add(d.Descripcion.Trim().ToLowerInvariant());
                    }
                }

                // Filtrar solicitudes para que ítems eliminados de la OS no influyan
                var solicitudesRelevantes = todasSolicitudes.Where(s =>
                {
                    var dId = ObtenerDetalleId(s);
                    if (dId.HasValue)
                    {
                        return idsActivos.Contains(dId.Value);
                    }
                    var item = ObtenerNombreItem(s);
                    if (item != null)
                    {
                        return nombresActivos.Contains(item);
                    }
                    // Solicitud genérica de la orden: siempre relevante mientras la orden exista
                    return true;
                }).ToList();

                var estadoAgregado = CalcularEstadoAgregado(solicitudesRelevantes, nuevoEstado);
                orden.EstadoAprobacionGerencia = estadoAgregado;
                if (estadoAgregado == EstadoAprobacionGerencia.Aprobado || estadoAgregado == EstadoAprobacionGerencia.Rechazado)
                {
                    orden.FechaAprobacionGerencia = DateTime.UtcNow;
                    if (usuarioId.HasValue) orden.UsuarioAprobacionGerenciaId = usuarioId;
                    if (!string.IsNullOrWhiteSpace(observaciones)) orden.ObservacionesAprobacionGerencia = observaciones.Trim();
                }
                orden.FechaModificacion = DateTime.UtcNow;
            }
        }
        else if (normalizadoEnt == "VENTA")
        {
            var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.Id == entidadId, ct);
            if (venta != null)
            {
                var estadoAgregado = CalcularEstadoAgregado(todasSolicitudes, nuevoEstado);
                venta.EstadoAprobacionGerencia = estadoAgregado;
                if (estadoAgregado == EstadoAprobacionGerencia.Aprobado || estadoAgregado == EstadoAprobacionGerencia.Rechazado)
                {
                    venta.FechaAprobacionGerencia = DateTime.UtcNow;
                    if (usuarioId.HasValue) venta.UsuarioAprobacionGerenciaId = usuarioId;
                    if (!string.IsNullOrWhiteSpace(observaciones)) venta.ObservacionesAprobacionGerencia = observaciones.Trim();
                }
                venta.FechaModificacion = DateTime.UtcNow;
            }
        }
        else if (normalizadoEnt == "PEDIDOLIMA")
        {
            var pedido = await _context.PedidosLima
                .Include(p => p.Detalles)
                .FirstOrDefaultAsync(p => p.Id == entidadId, ct);

            if (pedido != null)
            {
                var detallesActivos = pedido.Detalles.Where(d => d.Activo).ToList();
                var idsActivos = new HashSet<Guid>(detallesActivos.Select(d => d.Id));
                var prodIds = detallesActivos.Select(d => d.ProductoId).Distinct().ToList();
                var nombresActivos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (prodIds.Count > 0)
                {
                    var pNombres = await _context.Productos.Where(p => prodIds.Contains(p.Id)).Select(p => p.Nombre).ToListAsync(ct);
                    foreach (var n in pNombres) nombresActivos.Add(n.Trim().ToLowerInvariant());
                }

                var solicitudesRelevantes = todasSolicitudes.Where(s =>
                {
                    var dId = ObtenerDetalleId(s);
                    if (dId.HasValue)
                    {
                        return idsActivos.Contains(dId.Value);
                    }
                    var item = ObtenerNombreItem(s);
                    if (item != null)
                    {
                        return nombresActivos.Contains(item);
                    }
                    return true;
                }).ToList();

                var estadoAgregado = CalcularEstadoAgregado(solicitudesRelevantes, nuevoEstado);
                pedido.EstadoAprobacionGerencia = estadoAgregado;
                if (estadoAgregado == EstadoAprobacionGerencia.Aprobado || estadoAgregado == EstadoAprobacionGerencia.Rechazado)
                {
                    pedido.FechaAprobacionGerencia = DateTime.UtcNow;
                    if (usuarioId.HasValue) pedido.UsuarioAprobacionGerenciaId = usuarioId;
                    if (!string.IsNullOrWhiteSpace(observaciones)) pedido.ObservacionesAprobacionGerencia = observaciones.Trim();
                }
                pedido.FechaModificacion = DateTime.UtcNow;
            }
        }
    }

    private static SolicitudAprobacionResponse MapToResponse(SolicitudAprobacion s)
    {
        return new SolicitudAprobacionResponse(
            s.Id,
            s.Tipo,
            s.Entidad,
            s.EntidadId,
            s.UsuarioSolicitanteId,
            s.UsuarioSolicitanteNombre ?? s.UsuarioSolicitante?.NombreCompleto,
            s.FechaSolicitud,
            (int)s.Estado,
            s.Estado.ToString(),
            s.DetalleCambio,
            s.ValorAnterior,
            s.ValorSolicitado,
            s.Motivo,
            s.UsuarioAprobadorId,
            s.UsuarioAprobadorNombre ?? s.UsuarioAprobador?.NombreCompleto,
            s.FechaRespuesta,
            s.ObservacionesRespuesta
        );
    }
}
