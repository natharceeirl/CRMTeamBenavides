using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;

namespace CRMTeamBenavides.Api.Features.Vehiculos;

public static class VehiculoEndpoints
{
    public static void MapVehiculoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vehiculos").RequireAuthorization();

        group.MapGet("/", async (Guid? clienteId, ClaimsPrincipal user, IVehiculoService service, ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Ok(new List<VehiculoResponse>());
            }

            var vehiculos = await service.GetAllAsync(clienteId, isolation.SoloClienteId);
            return Results.Ok(vehiculos);
        })
        .RequireAuthorization(PermisosDefinidos.UnidadesVer)
        .WithName("GetVehiculos");

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IVehiculoService service, ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.GetByIdAsync(id, isolation.SoloClienteId);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .RequireAuthorization(PermisosDefinidos.UnidadesVer)
        .WithName("GetVehiculoById");

        group.MapGet("/{id:guid}/historial-servicio", async (
            Guid id,
            ClaimsPrincipal user,
            IPortalService portalService,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await portalService.GetHistorialServicioUnidadAsync(id, isolation.SoloClienteId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.UnidadesVer)
        .WithName("GetHistorialServicioUnidad");

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
        .RequireAuthorization(PermisosDefinidos.UnidadesCrear)
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
        .RequireAuthorization(PermisosDefinidos.UnidadesEditar)
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
        .RequireAuthorization(PermisosDefinidos.UnidadesEliminar)
        .WithName("DeleteVehiculo");
    }
}
