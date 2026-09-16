using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Permisos;

public static class PermisoEndpoints
{
    public static void MapPermisoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/permisos").RequireAuthorization();

        group.MapGet("/", async (IPermisoService service) =>
            Results.Ok(await service.GetAllAsync()))
        .WithName("GetPermisos");

        group.MapGet("/{id:guid}", async (Guid id, IPermisoService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .WithName("GetPermisoById");
    }
}
