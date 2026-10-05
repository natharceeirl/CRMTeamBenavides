using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Servicios;

public static class ServicioEndpoints
{
    public static void MapServicioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/servicios").RequireAuthorization();

        group.MapGet("/", async (bool? soloActivos, IServicioService service, CancellationToken ct) =>
        {
            var servicios = await service.GetAllAsync(soloActivos ?? true, ct);
            return Results.Ok(servicios);
        })
        .RequireAuthorization(PermisosDefinidos.ServiciosVer)
        .WithName("GetServicios");

        group.MapGet("/{id:guid}", async (Guid id, IServicioService service, CancellationToken ct) =>
        {
            var servicio = await service.GetByIdAsync(id, ct);
            return servicio is not null ? Results.Ok(servicio) : Results.NotFound();
        })
        .RequireAuthorization(PermisosDefinidos.ServiciosVer)
        .WithName("GetServicioById");

        group.MapPost("/", async (CrearServicioRequest request, IServicioService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/servicios/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.ServiciosCrear)
        .WithName("CreateServicio");

        group.MapPost("/alta-rapida", async (CrearServicioRequest request, IServicioService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/servicios/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.ServiciosCrear)
        .WithName("AltaRapidaServicio");

        group.MapPut("/{id:guid}", async (Guid id, ActualizarServicioRequest request, IServicioService service, CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.ServiciosEditar)
        .WithName("UpdateServicio");

        group.MapDelete("/{id:guid}", async (Guid id, IServicioService service, CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(id, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.ServiciosEliminar)
        .WithName("DeleteServicio");
    }
}
