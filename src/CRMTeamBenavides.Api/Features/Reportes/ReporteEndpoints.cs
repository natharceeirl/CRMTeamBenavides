using CRMTeamBenavides.Api.Configuration.Autorizacion;
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
        .RequireAuthorization(PermisosDefinidos.ReportesVerOperativos)
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
        .RequireAuthorization(PermisosDefinidos.ReportesVerOperativos)
        .WithName("GetReporteVentas");

        // --- Stock Bajo ---
        group.MapGet("/stock-bajo", async (IReporteService service) =>
        {
            var datos = await service.GetStockBajoAsync();
            return Results.Ok(datos);
        })
        .RequireAuthorization(PermisosDefinidos.ReportesVerOperativos)
        .WithName("GetReporteStockBajo");

        // --- Rentabilidad / Costos Históricos (D9) ---
        group.MapGet("/rentabilidad", async (
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            Guid? productoId,
            IReporteService service) =>
        {
            var datos = await service.GetRentabilidadAsync(fechaDesde, fechaHasta, productoId);
            return Results.Ok(datos);
        })
        .RequireAuthorization(PermisosDefinidos.ReportesVerFinancieros)
        .WithName("GetReporteRentabilidad");

        // --- Exportar Ventas a Excel (Item 11) ---
        async Task<IResult> ExportarVentasHandler(
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EstadoVenta? estado,
            Guid? clienteId,
            System.Security.Claims.ClaimsPrincipal user,
            IReporteService service)
        {
            bool incluirFinanciero = user.IsInRole(RolesDefinidos.GerenciaAdmin) || user.HasClaim("permission", PermisosDefinidos.ReportesVerFinancieros);
            var bytes = await service.ExportarVentasExcelAsync(fechaDesde, fechaHasta, estado, clienteId, incluirFinanciero);
            var fileName = $"ventas_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        group.MapGet("/ventas/excel", (
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EstadoVenta? estado,
            Guid? clienteId,
            System.Security.Claims.ClaimsPrincipal user,
            IReporteService service) => ExportarVentasHandler(fechaDesde, fechaHasta, estado, clienteId, user, service))
        .RequireAuthorization(PermisosDefinidos.ReportesVerOperativos)
        .WithName("ExportarReporteVentasExcel");

        group.MapPost("/exportar/ventas", (
            ExportarVentasExcelRequest? request,
            System.Security.Claims.ClaimsPrincipal user,
            IReporteService service) => ExportarVentasHandler(request?.FechaDesde, request?.FechaHasta, request?.Estado, request?.ClienteId, user, service))
        .RequireAuthorization(PermisosDefinidos.ReportesVerOperativos)
        .WithName("PostExportarReporteVentasExcel");

        // --- Exportar Órdenes de Servicio a Excel (Item 11) ---
        async Task<IResult> ExportarOrdenesHandler(
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EstadoOrdenServicio? estado,
            Guid? tecnicoId,
            System.Security.Claims.ClaimsPrincipal user,
            IReporteService service)
        {
            bool incluirFinanciero = user.IsInRole(RolesDefinidos.GerenciaAdmin) || user.HasClaim("permission", PermisosDefinidos.ReportesVerFinancieros);
            var bytes = await service.ExportarOrdenesServicioExcelAsync(fechaDesde, fechaHasta, estado, tecnicoId, incluirFinanciero);
            var fileName = $"ordenes_servicio_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        group.MapGet("/ordenes-servicio/excel", (
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EstadoOrdenServicio? estado,
            Guid? tecnicoId,
            System.Security.Claims.ClaimsPrincipal user,
            IReporteService service) => ExportarOrdenesHandler(fechaDesde, fechaHasta, estado, tecnicoId, user, service))
        .RequireAuthorization(PermisosDefinidos.ReportesVerOperativos)
        .WithName("ExportarReporteOrdenesServicioExcel");

        group.MapPost("/exportar/ordenes-servicio", (
            ExportarOrdenesServicioExcelRequest? request,
            System.Security.Claims.ClaimsPrincipal user,
            IReporteService service) => ExportarOrdenesHandler(request?.FechaDesde, request?.FechaHasta, request?.Estado, request?.TecnicoId, user, service))
        .RequireAuthorization(PermisosDefinidos.ReportesVerOperativos)
        .WithName("PostExportarReporteOrdenesServicioExcel");
    }
}

public record ExportarVentasExcelRequest(
    DateTime? FechaDesde,
    DateTime? FechaHasta,
    EstadoVenta? Estado,
    Guid? ClienteId);

public record ExportarOrdenesServicioExcelRequest(
    DateTime? FechaDesde,
    DateTime? FechaHasta,
    EstadoOrdenServicio? Estado,
    Guid? TecnicoId);
