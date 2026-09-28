using System.IdentityModel.Tokens.Jwt;
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
            var soloClienteId = await ResolverClienteRestringidoIdAsync(user, dbContext);
            var vehiculos = await service.GetAllAsync(clienteId, soloClienteId);
            return Results.Ok(vehiculos);
        })
        .RequireAuthorization(PermisosDefinidos.UnidadesVer)
        .WithName("GetVehiculos");

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IVehiculoService service, ApplicationDbContext dbContext) =>
        {
            var soloClienteId = await ResolverClienteRestringidoIdAsync(user, dbContext);
            var result = await service.GetByIdAsync(id, soloClienteId);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .RequireAuthorization(PermisosDefinidos.UnidadesVer)
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

    private static async Task<Guid?> ResolverClienteRestringidoIdAsync(ClaimsPrincipal user, ApplicationDbContext dbContext)
    {
        var esCliente = user.IsInRole(RolesDefinidos.Cliente)
            && !user.IsInRole(RolesDefinidos.GerenciaAdmin)
            && !user.IsInRole(RolesDefinidos.Recepcion);

        if (!esCliente)
        {
            return null;
        }

        var subClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(subClaim, out var usuarioId))
        {
            return await dbContext.Clientes
                .Where(c => c.UsuarioId == usuarioId && c.Activo)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync();
        }

        return null;
    }
}
