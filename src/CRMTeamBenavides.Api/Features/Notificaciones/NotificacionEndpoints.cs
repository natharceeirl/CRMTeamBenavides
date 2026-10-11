using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.Notificaciones;
using Microsoft.AspNetCore.Mvc;

namespace CRMTeamBenavides.Api.Features.Notificaciones;

/// <summary>La bandeja y los teléfonos de quien llama: cualquier usuario autenticado, solo lo suyo.</summary>
public static class NotificacionEndpoints
{
    public static IEndpointRouteBuilder MapNotificacionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notificaciones").WithTags("Notificaciones").RequireAuthorization();

        group.MapPost("/dispositivos", async (
            [FromBody] RegistrarDispositivoRequest request,
            INotificacionService servicio,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (UserIsolationHelper.ObtenerUsuarioId(user) is not { } usuarioId) return Results.Unauthorized();
            var result = await servicio.RegistrarDispositivoAsync(usuarioId, request, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.Conflict => Results.Conflict(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error })
            };
        })
        .WithName("RegistrarDispositivoNotificacion");

        group.MapPost("/dispositivos/baja", async (
            [FromBody] DarDeBajaDispositivoRequest request,
            INotificacionService servicio,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (UserIsolationHelper.ObtenerUsuarioId(user) is not { } usuarioId) return Results.Unauthorized();
            await servicio.DarDeBajaDispositivoAsync(usuarioId, request.Token, ct);
            return Results.NoContent();
        })
        .WithName("DarDeBajaDispositivoNotificacion");

        group.MapGet("/", async (
            [FromQuery] bool? soloNoLeidas,
            [FromQuery] int? limite,
            INotificacionService servicio,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (UserIsolationHelper.ObtenerUsuarioId(user) is not { } usuarioId) return Results.Unauthorized();
            return Results.Ok(await servicio.ListarAsync(usuarioId, soloNoLeidas ?? false, limite ?? 50, ct));
        })
        .WithName("ListarNotificaciones");

        group.MapGet("/conteo", async (INotificacionService servicio, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (UserIsolationHelper.ObtenerUsuarioId(user) is not { } usuarioId) return Results.Unauthorized();
            return Results.Ok(new ConteoNotificacionesResponse(await servicio.ContarNoLeidasAsync(usuarioId, ct)));
        })
        .WithName("ContarNotificacionesNoLeidas");

        group.MapPut("/{id:guid}/leida", async (Guid id, INotificacionService servicio, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (UserIsolationHelper.ObtenerUsuarioId(user) is not { } usuarioId) return Results.Unauthorized();
            return await servicio.MarcarLeidaAsync(usuarioId, id, ct)
                ? Results.NoContent()
                : Results.NotFound(new { error = "Notificación no encontrada." });
        })
        .WithName("MarcarNotificacionLeida");

        group.MapPut("/leidas", async (INotificacionService servicio, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (UserIsolationHelper.ObtenerUsuarioId(user) is not { } usuarioId) return Results.Unauthorized();
            await servicio.MarcarTodasLeidasAsync(usuarioId, ct);
            return Results.NoContent();
        })
        .WithName("MarcarTodasNotificacionesLeidas");

        return app;
    }
}
