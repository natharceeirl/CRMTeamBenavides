using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Features.Auditoria;

public record EventoAuditoriaResponse(
    Guid Id,
    Guid? UsuarioId,
    string? UsuarioNombre,
    DateTime Fecha,
    string Accion,
    string Entidad,
    string EntidadId,
    string? Detalle
);

public static class AuditoriaEndpoints
{
    public static void MapAuditoriaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auditoria").RequireAuthorization(PermisosDefinidos.AuditoriaVer);

        group.MapGet("/", async (
            string? entidad,
            string? accion,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            int? limite,
            ApplicationDbContext context,
            CancellationToken ct) =>
        {
            var query = context.EventosAuditoria.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(entidad))
                query = query.Where(e => e.Entidad == entidad.Trim());

            if (!string.IsNullOrWhiteSpace(accion))
                query = query.Where(e => e.Accion == accion.Trim());

            if (fechaDesde.HasValue)
                query = query.Where(e => e.Fecha >= fechaDesde.Value);

            if (fechaHasta.HasValue)
                query = query.Where(e => e.Fecha <= fechaHasta.Value);

            var take = Math.Clamp(limite ?? 50, 1, 200);

            var eventos = await query
                .OrderByDescending(e => e.Fecha)
                .Take(take)
                .ToListAsync(ct);

            var usuarioIds = eventos.Where(e => e.UsuarioId.HasValue).Select(e => e.UsuarioId!.Value).Distinct().ToList();
            var usuarios = await context.Usuarios
                .AsNoTracking()
                .Where(u => usuarioIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.NombreCompleto, ct);

            var response = eventos.Select(e => new EventoAuditoriaResponse(
                e.Id,
                e.UsuarioId,
                e.UsuarioId.HasValue && usuarios.TryGetValue(e.UsuarioId.Value, out var nombre) ? nombre : null,
                e.Fecha,
                e.Accion,
                e.Entidad,
                e.EntidadId,
                e.Detalle
            )).ToList();

            return Results.Ok(response);
        })
        .WithName("GetEventosAuditoria");
    }
}
