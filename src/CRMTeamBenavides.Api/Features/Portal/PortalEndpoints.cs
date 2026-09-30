using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;

namespace CRMTeamBenavides.Api.Features.Portal;

public static class PortalEndpoints
{
    public static void MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal").RequireAuthorization();

        // -------------------------------------------------------------------
        // Resumen Ejecutivo del Portal
        // -------------------------------------------------------------------
        group.MapGet("/resumen", async (
            ClaimsPrincipal user,
            IPortalService portalService,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso || !isolation.ClienteId.HasValue)
            {
                return Results.Json(new { error = "Usuario sin cuenta de cliente activa asociada." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await portalService.GetResumenAsync(isolation.ClienteId.Value);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.PortalAcceso)
        .WithName("GetPortalResumen");

        // -------------------------------------------------------------------
        // Comprobantes Consolidados del Cliente
        // -------------------------------------------------------------------
        group.MapGet("/comprobantes", async (
            ClaimsPrincipal user,
            IPortalService portalService,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso || !isolation.ClienteId.HasValue)
            {
                return Results.Json(new { error = "Usuario sin cuenta de cliente activa asociada." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await portalService.GetComprobantesAsync(isolation.ClienteId.Value);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.PortalAcceso)
        .WithName("GetPortalComprobantes");
    }
}
