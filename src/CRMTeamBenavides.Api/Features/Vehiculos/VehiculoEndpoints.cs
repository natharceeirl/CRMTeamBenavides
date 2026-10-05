using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;

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

        group.MapGet("/por-placa/{placa}/historial-servicio", async (
            string placa,
            ClaimsPrincipal user,
            IPortalService portalService,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var cleanPlaca = placa.Trim().ToUpperInvariant();
            var vehiculo = await dbContext.Vehiculos
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Placa != null && v.Placa.ToUpper() == cleanPlaca && v.Activo);

            if (vehiculo is null)
            {
                return Results.NotFound();
            }

            var result = await portalService.GetHistorialServicioUnidadAsync(vehiculo.Id, isolation.SoloClienteId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.UnidadesVer)
        .WithName("GetHistorialServicioPorPlaca");

        group.MapGet("/por-placa/{placa}", async (
            string placa,
            ClaimsPrincipal user,
            IVehiculoService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var cleanPlaca = placa.Trim().ToUpperInvariant();
            var vehiculo = await dbContext.Vehiculos
                .Include(v => v.Cliente)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Placa != null && v.Placa.ToUpper() == cleanPlaca && v.Activo);

            if (vehiculo is null)
            {
                return Results.NotFound();
            }

            if (isolation.SoloClienteId.HasValue && vehiculo.ClienteId != isolation.SoloClienteId.Value)
            {
                return Results.NotFound();
            }

            var result = await service.GetByIdAsync(vehiculo.Id, isolation.SoloClienteId);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .RequireAuthorization(PermisosDefinidos.UnidadesVer)
        .WithName("GetVehiculoPorPlaca");

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

        group.MapPost("/alta-rapida", async (AltaRapidaVehiculoRequest request, IVehiculoService service) =>
        {
            var createRequest = new CreateVehiculoRequest(
                request.ClienteId,
                request.Placa,
                request.Marca,
                request.Modelo,
                null,
                request.Kilometraje,
                request.Color,
                null);
            var result = await service.CreateAsync(createRequest);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/vehiculos/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.UnidadesCrear)
        .WithName("AltaRapidaVehiculo");

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
