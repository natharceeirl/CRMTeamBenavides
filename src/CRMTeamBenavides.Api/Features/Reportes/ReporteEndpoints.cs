using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Reportes;

public static class ReporteEndpoints
{
    public static void MapReporteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reportes").RequireAuthorization();

        // --- Órdenes de Servicio ---
        group.MapGet("/ordenes-servicio", async (
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EstadoOrdenServicio? estado,
            Guid? tecnicoId,
            IReporteService service) =>
        {
            var datos = await service.GetOrdenesServicioAsync(fechaDesde, fechaHasta, estado, tecnicoId);
            return Results.Ok(datos);
        })
        .WithName("GetReporteOrdenesServicio");

        // --- Ventas ---
        group.MapGet("/ventas", async (
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EstadoVenta? estado,
            Guid? clienteId,
            IReporteService service) =>
        {
            var datos = await service.GetVentasAsync(fechaDesde, fechaHasta, estado, clienteId);
            return Results.Ok(datos);
        })
        .WithName("GetReporteVentas");

        // --- Stock Bajo ---
        group.MapGet("/stock-bajo", async (IReporteService service) =>
        {
            var datos = await service.GetStockBajoAsync();
            return Results.Ok(datos);
        })
        .WithName("GetReporteStockBajo");
    }
}
