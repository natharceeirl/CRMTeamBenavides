using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Aprobaciones;

public static class AprobacionEndpoints
{
    public static void MapAprobacionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/aprobaciones").RequireAuthorization();

        // Consultar solicitudes pendientes (exclusivo para Gerencia/Admin)
        group.MapGet("/pendientes", async (IAprobacionService service, CancellationToken ct) =>
        {
            var pendientes = await service.ObtenerPendientesAsync(ct);
            return Results.Ok(pendientes);
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesAprobarGerencia)
        .WithName("GetAprobacionesPendientes");

        // Historial completo con filtros
        group.MapGet("/", async (
            string? entidad,
            string? tipo,
            EstadoAprobacionGerencia? estado,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            IAprobacionService service,
            CancellationToken ct) =>
        {
            var historial = await service.ObtenerHistorialAsync(entidad, tipo, estado, fechaDesde, fechaHasta, ct);
            return Results.Ok(historial);
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesAprobarGerencia)
        .WithName("GetAprobacionesHistorial");

        // Obtener solicitud por ID
        group.MapGet("/{id:guid}", async (Guid id, IAprobacionService service, CancellationToken ct) =>
        {
            var result = await service.ObtenerPorIdAsync(id, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesAprobarGerencia)
        .WithName("GetAprobacionPorId");

        // Solicitar aprobación de Gerencia (personal de mostrador/taller)
        group.MapPost("/solicitar", async (
            RegistrarSolicitudAprobacionRequest request,
            ClaimsPrincipal user,
            IAprobacionService service,
            CancellationToken ct) =>
        {
            var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
            var result = await service.CrearSolicitudAsync(request, usuarioId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/aprobaciones/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesEditar)
        .WithName("CrearSolicitudAprobacion");

        // Resolver (Aprobar o Rechazar)
        group.MapPost("/{id:guid}/resolver", async (
            Guid id,
            ResolverSolicitudAprobacionRequest request,
            ClaimsPrincipal user,
            IAprobacionService service,
            CancellationToken ct) =>
        {
            var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
            var result = await service.ResolverSolicitudAsync(id, request, usuarioId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesAprobarGerencia)
        .WithName("ResolverSolicitudAprobacion");

        // Alias explícito: Aprobar
        group.MapPost("/{id:guid}/aprobar", async (
            Guid id,
            string? observaciones,
            ClaimsPrincipal user,
            IAprobacionService service,
            CancellationToken ct) =>
        {
            var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
            var result = await service.ResolverSolicitudAsync(
                id,
                new ResolverSolicitudAprobacionRequest(EstadoAprobacionGerencia.Aprobado, observaciones),
                usuarioId,
                ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesAprobarGerencia)
        .WithName("AprobarSolicitud");

        // Alias explícito: Rechazar
        group.MapPost("/{id:guid}/rechazar", async (
            Guid id,
            string? observaciones,
            ClaimsPrincipal user,
            IAprobacionService service,
            CancellationToken ct) =>
        {
            var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
            var result = await service.ResolverSolicitudAsync(
                id,
                new ResolverSolicitudAprobacionRequest(EstadoAprobacionGerencia.Rechazado, observaciones),
                usuarioId,
                ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesAprobarGerencia)
        .WithName("RechazarSolicitud");
    }
}
