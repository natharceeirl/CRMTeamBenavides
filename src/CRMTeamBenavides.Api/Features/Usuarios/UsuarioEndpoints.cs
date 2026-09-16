using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Usuarios;

public static class UsuarioEndpoints
{
    public static void MapUsuarioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/usuarios").RequireAuthorization();

        group.MapGet("/", async (IUsuarioService service) =>
            Results.Ok(await service.GetAllAsync()))
        .WithName("GetUsuarios");

        group.MapGet("/{id:guid}", async (Guid id, IUsuarioService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .WithName("GetUsuarioById");

        group.MapPost("/", async (CreateUsuarioRequest request, IUsuarioService service) =>
        {
            var result = await service.CreateAsync(request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/usuarios/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("CreateUsuario");

        group.MapPut("/{id:guid}", async (Guid id, UpdateUsuarioRequest request, IUsuarioService service) =>
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
        .WithName("UpdateUsuario");

        group.MapDelete("/{id:guid}", async (Guid id, IUsuarioService service) =>
        {
            var result = await service.DeleteAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("DeleteUsuario");

        group.MapGet("/{id:guid}/roles", async (Guid id, IUsuarioService service) =>
        {
            var result = await service.GetRolesAsync(id);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .WithName("GetRolesDeUsuario");

        group.MapPost("/{id:guid}/roles", async (Guid id, AsignarRolRequest request, IUsuarioService service) =>
        {
            var result = await service.AsignarRolAsync(id, request.RolId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("AsignarRolAUsuario");

        group.MapDelete("/{id:guid}/roles/{rolId:guid}", async (Guid id, Guid rolId, IUsuarioService service) =>
        {
            var result = await service.QuitarRolAsync(id, rolId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("QuitarRolDeUsuario");
    }
}
