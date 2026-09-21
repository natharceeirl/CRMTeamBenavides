using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.OrdenesServicio;

public static class OrdenServicioEndpoints
{
    public static void MapOrdenServicioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ordenes-servicio").RequireAuthorization();

        group.MapGet("/", async (Guid? vehiculoId, EstadoOrdenServicio? estado, Guid? clienteId, IOrdenServicioService service) =>
        {
            var ordenes = await service.GetAllAsync(vehiculoId, estado, clienteId);
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

        group.MapPost("/{id:guid}/detalles", async (Guid id, AgregarDetalleServicioRequest request, IOrdenServicioService service) =>
        {
            var result = await service.AgregarDetalleAsync(id, request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ordenes-servicio/{id}/detalles/{result.Data!.Id}", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("AgregarDetalleOrdenServicio");

        group.MapDelete("/{id:guid}/detalles/{detalleId:guid}", async (Guid id, Guid detalleId, IOrdenServicioService service) =>
        {
            var result = await service.EliminarDetalleAsync(id, detalleId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(new { message = "Detalle eliminado correctamente." }),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("EliminarDetalleOrdenServicio");

        group.MapPut("/{id:guid}/estado", async (Guid id, CambiarEstadoOrdenServicioRequest request, IOrdenServicioService service) =>
        {
            var result = await service.CambiarEstadoAsync(id, request);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("CambiarEstadoOrdenServicio");
    }
}
