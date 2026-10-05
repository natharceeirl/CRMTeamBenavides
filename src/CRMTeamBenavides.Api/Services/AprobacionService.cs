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
        if (entUpper == "ORDENSERVICIO" || entUpper == "ORDENESSERVICIO")
        {
            var orden = await _context.OrdenesServicio.FirstOrDefaultAsync(o => o.Id == entidadId, ct);
            if (orden != null)
            {
                orden.EstadoAprobacionGerencia = nuevoEstado;
                orden.FechaAprobacionGerencia = DateTime.UtcNow;
                if (usuarioId.HasValue) orden.UsuarioAprobacionGerenciaId = usuarioId;
                if (!string.IsNullOrWhiteSpace(observaciones)) orden.ObservacionesAprobacionGerencia = observaciones.Trim();
                orden.FechaModificacion = DateTime.UtcNow;
            }
        }
        else if (entUpper == "VENTA" || entUpper == "VENTAS")
        {
            var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.Id == entidadId, ct);
            if (venta != null)
            {
                venta.EstadoAprobacionGerencia = nuevoEstado;
                venta.FechaAprobacionGerencia = DateTime.UtcNow;
                if (usuarioId.HasValue) venta.UsuarioAprobacionGerenciaId = usuarioId;
                if (!string.IsNullOrWhiteSpace(observaciones)) venta.ObservacionesAprobacionGerencia = observaciones.Trim();
                venta.FechaModificacion = DateTime.UtcNow;
            }
        }
        else if (entUpper == "PEDIDOLIMA" || entUpper == "PEDIDOSLIMA")
        {
            var pedido = await _context.PedidosLima.FirstOrDefaultAsync(p => p.Id == entidadId, ct);
            if (pedido != null)
            {
                pedido.EstadoAprobacionGerencia = nuevoEstado;
                pedido.FechaAprobacionGerencia = DateTime.UtcNow;
                if (usuarioId.HasValue) pedido.UsuarioAprobacionGerenciaId = usuarioId;
                if (!string.IsNullOrWhiteSpace(observaciones)) pedido.ObservacionesAprobacionGerencia = observaciones.Trim();
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
