using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Features.Clientes;

public static class ClienteEndpoints
{
    public static void MapClienteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clientes").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IClienteService service, ApplicationDbContext dbContext) =>
        {
            var soloClienteId = await ResolverClienteRestringidoIdAsync(user, dbContext);
            var clientes = await service.GetAllAsync(soloClienteId);
            return Results.Ok(clientes);
        })
        .RequireAuthorization(PermisosDefinidos.ClientesVer)
        .WithName("GetClientes");

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IClienteService service, ApplicationDbContext dbContext) =>
        {
            var soloClienteId = await ResolverClienteRestringidoIdAsync(user, dbContext);
            var result = await service.GetByIdAsync(id, soloClienteId);
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound();
        })
        .RequireAuthorization(PermisosDefinidos.ClientesVer)
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
        .RequireAuthorization(PermisosDefinidos.ClientesCrear)
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
        .RequireAuthorization(PermisosDefinidos.ClientesEditar)
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
        .RequireAuthorization(PermisosDefinidos.ClientesEliminar)
        .WithName("DeleteCliente");
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
