using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Vehiculos;

public static class VehiculoEndpoints
{
    public static void MapVehiculoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vehiculos").RequireAuthorization();

        group.MapGet("/", async (Guid? clienteId, IVehiculoService service) =>
        {
            var vehiculos = await service.GetAllAsync(clienteId);
            return Results.Ok(vehiculos);
        })
        .WithName("GetVehiculos");

        group.MapGet("/{id:guid}", async (Guid id, IVehiculoService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .WithName("GetVehiculoById");

        group.MapPost("/", async (CreateVehiculoRequest request, IVehiculoService service) =>
        {
            var result = await service.CreateAsync(request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/vehiculos/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("CreateVehiculo");

        group.MapPut("/{id:guid}", async (Guid id, UpdateVehiculoRequest request, IVehiculoService service) =>
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
        .WithName("UpdateVehiculo");

        group.MapDelete("/{id:guid}", async (Guid id, IVehiculoService service) =>
        {
            var result = await service.DeleteAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("DeleteVehiculo");
    }
}
