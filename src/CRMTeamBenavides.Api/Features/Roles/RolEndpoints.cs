using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Roles;

public static class RolEndpoints
{
    public static void MapRolEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/roles").RequireAuthorization();

        group.MapGet("/", async (IRolService service) =>
            Results.Ok(await service.GetAllAsync()))
        .WithName("GetRoles");

        group.MapGet("/{id:guid}", async (Guid id, IRolService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .WithName("GetRolById");

        group.MapPost("/", async (CreateRolRequest request, IRolService service) =>
        {
            var result = await service.CreateAsync(request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/roles/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("CreateRol");

        group.MapPut("/{id:guid}", async (Guid id, UpdateRolRequest request, IRolService service) =>
        {
            var result = await service.UpdateAsync(id, request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("UpdateRol");

        group.MapDelete("/{id:guid}", async (Guid id, IRolService service) =>
        {
            var result = await service.DeleteAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("DeleteRol");

        group.MapGet("/{id:guid}/permisos", async (Guid id, IRolService service) =>
        {
            var result = await service.GetPermisosAsync(id);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .WithName("GetPermisosDeRol");

        group.MapPost("/{id:guid}/permisos", async (Guid id, AsignarPermisoRequest request, IRolService service) =>
        {
            var result = await service.AsignarPermisoAsync(id, request.PermisoId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("AsignarPermisoARol");

        group.MapDelete("/{id:guid}/permisos/{permisoId:guid}", async (Guid id, Guid permisoId, IRolService service) =>
        {
            var result = await service.QuitarPermisoAsync(id, permisoId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("QuitarPermisoDeRol");
    }
}
