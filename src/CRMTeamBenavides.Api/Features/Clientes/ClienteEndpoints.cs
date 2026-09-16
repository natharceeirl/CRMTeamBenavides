using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Clientes;

public static class ClienteEndpoints
{
    public static void MapClienteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clientes").RequireAuthorization();

        group.MapGet("/", async (IClienteService service) =>
        {
            var clientes = await service.GetAllAsync();
            return Results.Ok(clientes);
        })
        .WithName("GetClientes");

        group.MapGet("/{id:guid}", async (Guid id, IClienteService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .WithName("GetClienteById");

        group.MapPost("/", async (CreateClienteRequest request, IClienteService service) =>
        {
            var result = await service.CreateAsync(request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/clientes/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("CreateCliente");

        group.MapPut("/{id:guid}", async (Guid id, UpdateClienteRequest request, IClienteService service) =>
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
        .WithName("UpdateCliente");

        group.MapDelete("/{id:guid}", async (Guid id, IClienteService service) =>
        {
            var result = await service.DeleteAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("DeleteCliente");
    }
}
