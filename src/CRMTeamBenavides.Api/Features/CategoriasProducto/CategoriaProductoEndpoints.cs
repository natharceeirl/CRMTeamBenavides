using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.CategoriasProducto;

public static class CategoriaProductoEndpoints
{
    public static void MapCategoriaProductoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categorias-producto").RequireAuthorization();

        group.MapGet("/", async (ICategoriaProductoService service) =>
        {
            var categorias = await service.GetAllAsync();
            return Results.Ok(categorias);
        })
        .WithName("GetCategoriasProducto");

        group.MapGet("/{id:guid}", async (Guid id, ICategoriaProductoService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("GetCategoriaProductoById");

        group.MapPost("/", async (CreateCategoriaProductoRequest request, ICategoriaProductoService service) =>
        {
            var result = await service.CreateAsync(request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/categorias-producto/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("CreateCategoriaProducto");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCategoriaProductoRequest request, ICategoriaProductoService service) =>
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
        .WithName("UpdateCategoriaProducto");

        group.MapDelete("/{id:guid}", async (Guid id, ICategoriaProductoService service) =>
        {
            var result = await service.DeleteAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("DeleteCategoriaProducto");
    }
}
