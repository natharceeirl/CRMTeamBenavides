using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.OrdenesServicio;

public static class OrdenServicioEndpoints
{
    public static void MapOrdenServicioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ordenes-servicio").RequireAuthorization();

        group.MapGet("/", async (Guid? vehiculoId, EstadoOrdenServicio? estado, IOrdenServicioService service) =>
        {
            var ordenes = await service.GetAllAsync(vehiculoId, estado);
            return Results.Ok(ordenes);
        })
        .WithName("GetOrdenesServicio");

        group.MapGet("/{id:guid}", async (Guid id, IOrdenServicioService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("GetOrdenServicioById");

        group.MapPost("/", async (AperturaOrdenServicioRequest request, IOrdenServicioService service) =>
        {
            var result = await service.CreateAperturaAsync(request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ordenes-servicio/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("CreateAperturaOrdenServicio");

        group.MapPut("/{id:guid}/diagnostico", async (Guid id, RegistrarDiagnosticoRequest request, IOrdenServicioService service) =>
        {
            var result = await service.RegistrarDiagnosticoAsync(id, request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("RegistrarDiagnosticoOrdenServicio");
    }
}
