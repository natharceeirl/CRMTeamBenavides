using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Motos;

/// <summary>Las motos del inventario, cada una con su serie.</summary>
public static class MotoEndpoints
{
    public static void MapMotoEndpoints(this IEndpointRouteBuilder app)
    {
        // Para la pestaña Vehículos y para elegir la moto exacta al vender.
        app.MapGet("/api/motos", async (
            Guid? productoId,
            EstadoUnidadInventario? estado,
            string? busqueda,
            MotosService service,
            CancellationToken ct) =>
            Results.Ok(await service.ListarAsync(productoId, estado, busqueda, ct)))
        .RequireAuthorization(PermisosDefinidos.InventarioVer)
        .WithName("GetMotos");

        // Las motos que ya estaban en la tienda entran al stock sin compra.
        app.MapPost("/api/productos/{id:guid}/motos", async (
            Guid id,
            IngresarMotoRequest request,
            MotosService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var result = await service.IngresarAsync(id, request, UserIsolationHelper.ObtenerUsuarioId(user), ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/motos/{result.Data!.Id}", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                _ => Results.BadRequest(new { mensaje = result.Error }),
            };
        })
        .RequireAuthorization(PermisosDefinidos.InventarioAjustar)
        .WithName("IngresarMoto");
    }
}
