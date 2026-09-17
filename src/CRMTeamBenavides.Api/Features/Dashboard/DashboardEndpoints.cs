using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").RequireAuthorization();

        group.MapGet("/resumen", async (
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            IDashboardService service) =>
        {
            var resumen = await service.GetResumenAsync(fechaDesde, fechaHasta);
            return Results.Ok(resumen);
        })
        .WithName("GetDashboardResumen");
    }
}
