using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace CRMTeamBenavides.Api.Features.CajaChica;

public static class CajaChicaEndpoints
{
    public static void MapCajaChicaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/caja-chica").RequireAuthorization();

        // -------------------------------------------------------------------
        // Consultas
        // -------------------------------------------------------------------
        group.MapGet("/actual", async (ICajaChicaService service, CancellationToken ct) =>
        {
            var res = await service.ObtenerCajaActualAsync(ct);
            return Results.Ok(res);
        })
        .RequireAuthorization(PermisosDefinidos.CajaConsultar)
        .WithName("GetCajaChicaActual");

        group.MapGet("/", async (ICajaChicaService service, CancellationToken ct) =>
        {
            var res = await service.ObtenerHistorialCajasAsync(ct);
            return Results.Ok(res);
        })
        .RequireAuthorization(PermisosDefinidos.CajaConsultar)
        .WithName("GetCajasChicas");

        group.MapGet("/historial", async (ICajaChicaService service, CancellationToken ct) =>
        {
            var res = await service.ObtenerHistorialCajasAsync(ct);
            return Results.Ok(res);
        })
        .RequireAuthorization(PermisosDefinidos.CajaConsultar)
        .WithName("GetHistorialCajasChicas");

        group.MapGet("/{id:guid}", async (Guid id, ICajaChicaService service, CancellationToken ct) =>
        {
            var caja = await service.ObtenerCajaPorIdAsync(id, ct);
            return caja == null ? Results.NotFound(new { mensaje = "Caja chica no encontrada." }) : Results.Ok(caja);
        })
        .RequireAuthorization(PermisosDefinidos.CajaConsultar)
        .WithName("GetCajaChicaPorId");

        group.MapGet("/movimientos", async (
            Guid? cajaChicaId,
            TipoMovimientoCaja? tipo,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            ICajaChicaService service,
            CancellationToken ct) =>
        {
            var movimientos = await service.ObtenerMovimientosAsync(cajaChicaId, tipo, fechaDesde, fechaHasta, ct);
            return Results.Ok(movimientos);
        })
        .RequireAuthorization(PermisosDefinidos.CajaConsultar)
        .WithName("GetMovimientosCajaChica");

        // -------------------------------------------------------------------
        // Operaciones de Apertura y Cierre
        // -------------------------------------------------------------------
        group.MapPost("/apertura", async (
            AperturaCajaRequest request,
            ClaimsPrincipal user,
            ICajaChicaService service,
            CancellationToken ct) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.AperturarCajaAsync(request, usuarioId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/caja-chica/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                ServiceResultStatus.Conflict => Results.Conflict(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.CajaAperturar)
        .WithName("AperturarCajaChica");

        group.MapPost("/cierre", async (
            CierreCajaRequest request,
            ClaimsPrincipal user,
            ICajaChicaService service,
            CancellationToken ct) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.CerrarCajaAsync(request, usuarioId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.CajaCerrar)
        .WithName("CerrarCajaChica");

        // -------------------------------------------------------------------
        // Movimientos (Ingresos / Egresos)
        // -------------------------------------------------------------------
        group.MapPost("/movimientos", async (
            RegistrarMovimientoCajaRequest request,
            ClaimsPrincipal user,
            IAuthorizationService authService,
            ICajaChicaService service,
            CancellationToken ct) =>
        {
            // Validar permiso según el tipo de movimiento
            var permisoRequerido = request.Tipo == TipoMovimientoCaja.Ingreso
                ? PermisosDefinidos.CajaRegistrarIngreso
                : PermisosDefinidos.CajaRegistrarEgreso;

            var authResult = await authService.AuthorizeAsync(user, permisoRequerido);
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.RegistrarMovimientoAsync(request, usuarioId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/caja-chica/movimientos/{result.Data!.Id}", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .WithName("RegistrarMovimientoCajaChica");

        group.MapPost("/ingresos", async (
            RegistrarMovimientoCajaRequest request,
            ClaimsPrincipal user,
            ICajaChicaService service,
            CancellationToken ct) =>
        {
            var reqIngreso = request with { Tipo = TipoMovimientoCaja.Ingreso };
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.RegistrarMovimientoAsync(reqIngreso, usuarioId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/caja-chica/movimientos/{result.Data!.Id}", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.CajaRegistrarIngreso)
        .WithName("RegistrarIngresoCajaChica");

        group.MapPost("/egresos", async (
            RegistrarMovimientoCajaRequest request,
            ClaimsPrincipal user,
            ICajaChicaService service,
            CancellationToken ct) =>
        {
            var reqEgreso = request with { Tipo = TipoMovimientoCaja.Egreso };
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.RegistrarMovimientoAsync(reqEgreso, usuarioId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/caja-chica/movimientos/{result.Data!.Id}", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.CajaRegistrarEgreso)
        .WithName("RegistrarEgresoCajaChica");
    }

    private static Guid? ObtenerUsuarioId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("uid");
        return Guid.TryParse(idStr, out var id) ? id : null;
    }
}
