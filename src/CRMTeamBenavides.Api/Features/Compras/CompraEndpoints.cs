using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CRMTeamBenavides.Api.Features.Compras;

public static class CompraEndpoints
{
    private static readonly string[] EstadosPagoValidos =
    {
        EstadosPagoCompra.Pendiente,
        EstadosPagoCompra.Parcial,
        EstadosPagoCompra.Pagada,
        EstadosPagoCompra.PorPagar,
        EstadosPagoCompra.Vencida
    };

    public static IEndpointRouteBuilder MapCompraEndpoints(this IEndpointRouteBuilder app)
    {
        MapCompras(app.MapGroup("/api/compras").WithTags("Compras"));
        MapProveedores(app.MapGroup("/api/proveedores").WithTags("Proveedores"));
        return app;
    }

    private static void MapCompras(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [FromQuery] Guid? proveedorId,
            [FromQuery] EstadoCompra? estado,
            [FromQuery] string? estadoPago,
            [FromQuery] DateOnly? fechaDesde,
            [FromQuery] DateOnly? fechaHasta,
            [FromQuery] string? busqueda,
            ICompraService compraService,
            CancellationToken ct) =>
        {
            if (!EstadoPagoValido(estadoPago))
                return Results.BadRequest(new { mensaje = "El estado de pago del filtro no es válido." });

            var compras = await compraService.ListarAsync(
                new FiltrosCompras(proveedorId, estado, estadoPago, fechaDesde, fechaHasta, busqueda), ct);
            return Results.Ok(compras);
        })
        .RequireAuthorization(PermisosDefinidos.ComprasVer)
        .WithName("ListarCompras");

        group.MapGet("/exportar-excel", async (
            [FromQuery] Guid? proveedorId,
            [FromQuery] EstadoCompra? estado,
            [FromQuery] string? estadoPago,
            [FromQuery] DateOnly? fechaDesde,
            [FromQuery] DateOnly? fechaHasta,
            [FromQuery] string? busqueda,
            ICompraService compraService,
            CancellationToken ct) =>
        {
            if (!EstadoPagoValido(estadoPago))
                return Results.BadRequest(new { mensaje = "El estado de pago del filtro no es válido." });

            var bytes = await compraService.ExportarExcelAsync(
                new FiltrosCompras(proveedorId, estado, estadoPago, fechaDesde, fechaHasta, busqueda), ct);
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"compras_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        })
        .RequireAuthorization(PermisosDefinidos.ComprasVer)
        .WithName("ExportarComprasExcel");

        group.MapGet("/{id:guid}", async (Guid id, ICompraService compraService, CancellationToken ct) =>
            Responder(await compraService.ObtenerAsync(id, ct)))
        .RequireAuthorization(PermisosDefinidos.ComprasVer)
        .WithName("ObtenerCompra");

        group.MapPost("/", async (
            [FromBody] CrearCompraRequest request,
            ICompraService compraService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var result = await compraService.RegistrarAsync(request, UserIsolationHelper.ObtenerUsuarioId(user), ct);
            return result.IsSuccess
                ? Results.Created($"/api/compras/{result.Data!.Id}", result.Data)
                : Responder(result);
        })
        .RequireAuthorization(PermisosDefinidos.ComprasRegistrar)
        .WithName("RegistrarCompra");

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] ActualizarCompraRequest request,
            ICompraService compraService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
            Responder(await compraService.ActualizarAsync(id, request, UserIsolationHelper.ObtenerUsuarioId(user), ct)))
        .RequireAuthorization(PermisosDefinidos.ComprasRegistrar)
        .WithName("ActualizarCompra");

        group.MapPost("/{id:guid}/pagos", async (
            Guid id,
            [FromBody] RegistrarPagoCompraRequest request,
            ICompraService compraService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
            Responder(await compraService.RegistrarPagoAsync(id, request, UserIsolationHelper.ObtenerUsuarioId(user), ct)))
        .RequireAuthorization(PermisosDefinidos.ComprasRegistrar)
        .WithName("RegistrarPagoCompra");

        group.MapPut("/{id:guid}/pagos/{pagoId:guid}/anular", async (
            Guid id,
            Guid pagoId,
            [FromBody] AnularRequest request,
            ICompraService compraService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
            Responder(await compraService.AnularPagoAsync(id, pagoId, request, UserIsolationHelper.ObtenerUsuarioId(user), ct)))
        .RequireAuthorization(PermisosDefinidos.ComprasAnular)
        .WithName("AnularPagoCompra");

        group.MapPut("/{id:guid}/anular", async (
            Guid id,
            [FromBody] AnularRequest request,
            ICompraService compraService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
            Responder(await compraService.AnularAsync(id, request, UserIsolationHelper.ObtenerUsuarioId(user), ct)))
        .RequireAuthorization(PermisosDefinidos.ComprasAnular)
        .WithName("AnularCompra");
    }

    private static void MapProveedores(RouteGroupBuilder group)
    {
        group.MapGet("/", async ([FromQuery] string? busqueda, IProveedorService proveedorService, CancellationToken ct) =>
            Results.Ok(await proveedorService.ListarAsync(busqueda, ct)))
        .RequireAuthorization(PermisosDefinidos.ComprasVer)
        .WithName("ListarProveedores");

        group.MapPost("/", async (
            [FromBody] GuardarProveedorRequest request,
            IProveedorService proveedorService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var result = await proveedorService.CrearAsync(request, UserIsolationHelper.ObtenerUsuarioId(user), ct);
            return result.IsSuccess
                ? Results.Created($"/api/proveedores/{result.Data!.Id}", result.Data)
                : Responder(result);
        })
        .RequireAuthorization(PermisosDefinidos.ComprasRegistrar)
        .WithName("CrearProveedor");

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] GuardarProveedorRequest request,
            IProveedorService proveedorService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
            Responder(await proveedorService.ActualizarAsync(id, request, UserIsolationHelper.ObtenerUsuarioId(user), ct)))
        .RequireAuthorization(PermisosDefinidos.ComprasRegistrar)
        .WithName("ActualizarProveedor");

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IProveedorService proveedorService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var result = await proveedorService.EliminarAsync(id, UserIsolationHelper.ObtenerUsuarioId(user), ct);
            return result.IsSuccess ? Results.NoContent() : Responder(result);
        })
        .RequireAuthorization(PermisosDefinidos.ComprasRegistrar)
        .WithName("EliminarProveedor");
    }

    private static bool EstadoPagoValido(string? estadoPago) =>
        string.IsNullOrWhiteSpace(estadoPago) || EstadosPagoValidos.Contains(estadoPago);

    private static IResult Responder<T>(ServiceResult<T> result) => result.Status switch
    {
        ServiceResultStatus.Success => Results.Ok(result.Data),
        ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
        ServiceResultStatus.Conflict => Results.Conflict(new { mensaje = result.Error }),
        ServiceResultStatus.Forbidden => Results.Forbid(),
        _ => Results.BadRequest(new { mensaje = result.Error })
    };
}
