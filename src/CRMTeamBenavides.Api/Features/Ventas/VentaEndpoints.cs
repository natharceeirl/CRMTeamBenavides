using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Features.Ventas;

public static class VentaEndpoints
{
    public const string PoliticaVerVentas = "PoliticaVerVentas";

    public static void MapVentaEndpoints(this IEndpointRouteBuilder app)
    {
        // -------------------------------------------------------------------
        // Métodos de Pago
        // -------------------------------------------------------------------
        var groupMetodos = app.MapGroup("/api/metodos-pago").RequireAuthorization();

        groupMetodos.MapGet("/", async (IPagoService pagoService) =>
        {
            var metodos = await pagoService.GetMetodosPagoAsync();
            return Results.Ok(metodos);
        })
        .WithName("GetMetodosPago");

        // -------------------------------------------------------------------
        // Ventas
        // -------------------------------------------------------------------
        var group = app.MapGroup("/api/ventas").RequireAuthorization();

        group.MapGet("/metodos-pago", async (IPagoService pagoService) =>
        {
            var metodos = await pagoService.GetMetodosPagoAsync();
            return Results.Ok(metodos);
        })
        .WithName("GetMetodosPagoVenta");

        group.MapGet("/", async (
            Guid? clienteId,
            EstadoVenta? estado,
            Guid? ordenServicioId,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            bool? pendienteComprobante,
            ClaimsPrincipal user,
            IVentaService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Ok(new List<VentaResponse>());
            }

            var ventas = await service.GetAllAsync(clienteId, estado, ordenServicioId, fechaDesde, fechaHasta, isolation.SoloClienteId, pendienteComprobante);
            return Results.Ok(ventas);
        })
        .RequireAuthorization(PoliticaVerVentas)
        .WithName("GetVentas");

        group.MapGet("/pendientes-comprobante", async (
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            ClaimsPrincipal user,
            IVentaService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Ok(new List<VentaResponse>());
            }

            var ventas = await service.GetAllAsync(null, EstadoVenta.Confirmada, null, fechaDesde, fechaHasta, isolation.SoloClienteId, pendienteComprobante: true);
            return Results.Ok(ventas);
        })
        .RequireAuthorization(PoliticaVerVentas)
        .WithName("GetVentasPendientesComprobante");

        group.MapGet("/exportar-excel", async (
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EstadoVenta? estado,
            Guid? clienteId,
            ClaimsPrincipal user,
            IReporteService reporteService,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var efectivoClienteId = isolation.SoloClienteId ?? clienteId;
            bool incluirFinanciero = !isolation.SoloClienteId.HasValue && (user.IsInRole(RolesDefinidos.GerenciaAdmin) || user.HasClaim("permission", PermisosDefinidos.ReportesVerFinancieros));
            var bytes = await reporteService.ExportarVentasExcelAsync(fechaDesde, fechaHasta, estado, efectivoClienteId, incluirFinanciero);
            var fileName = $"ventas_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        })
        .RequireAuthorization(PoliticaVerVentas)
        .WithName("ExportarVentasExcel");


        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IVentaService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.GetByIdAsync(id, isolation.SoloClienteId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PoliticaVerVentas)
        .WithName("GetVentaById");

        group.MapPost("/", async (
            CreateVentaRequest request,
            ClaimsPrincipal user,
            IVentaService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.BadRequest(new { error = "Usuario cliente sin cliente activo asociado." });
            }

            var puedeModificarPrecios = await PuedeModificarPreciosAsync(user, dbContext);
            var puedeAplicarDescuentos = await PuedeAplicarDescuentosAsync(user, dbContext);
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.CreateAsync(
                request, puedeModificarPrecios, puedeAplicarDescuentos, isolation.SoloClienteId, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ventas/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                ServiceResultStatus.Conflict => Results.Conflict(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.VentasCrear)
        .WithName("CreateVenta");

        group.MapPut("/{id:guid}", async (
            Guid id,
            ActualizarVentaRequest request,
            ClaimsPrincipal user,
            IVentaService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.BadRequest(new { error = "Usuario cliente sin cliente activo asociado." });
            }

            var puedeModificarPrecios = await PuedeModificarPreciosAsync(user, dbContext);
            var puedeAplicarDescuentos = await PuedeAplicarDescuentosAsync(user, dbContext);
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.ActualizarAsync(
                id, request, puedeModificarPrecios, puedeAplicarDescuentos, isolation.SoloClienteId, usuarioId);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.VentasCrear)
        .WithName("ActualizarVenta");

        group.MapPut("/{id:guid}/confirmar", async (
            Guid id,
            ClaimsPrincipal user,
            IVentaService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.ConfirmarCotizacionAsync(id, isolation.SoloClienteId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.VentasCrear)
        .WithName("ConfirmarCotizacion");

        group.MapPut("/{id:guid}/anular", async (Guid id, ClaimsPrincipal user, IVentaService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.AnularAsync(id, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.VentasAnular)
        .WithName("AnularVenta");

        group.MapPut("/{id:guid}/aprobacion-gerencia", async (
            Guid id,
            CRMTeamBenavides.Api.Features.OrdenesServicio.AprobacionGerenciaRequest request,
            ClaimsPrincipal user,
            IVentaService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.AprobacionGerenciaAsync(id, request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesAprobarGerencia)
        .WithName("AprobacionGerenciaVenta");


        // -------------------------------------------------------------------
        // Pagos sobre Ventas
        // -------------------------------------------------------------------
        group.MapPost("/{id:guid}/pagos", async (
            Guid id,
            RegistrarPagoRequest request,
            ClaimsPrincipal user,
            IPagoService pagoService) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await pagoService.RegistrarPagoVentaAsync(id, request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ventas/{id}/pagos/{result.Data!.Id}", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.VentasCrear)
        .WithName("RegistrarPagoVenta");

        group.MapGet("/{id:guid}/pagos", async (
            Guid id,
            ClaimsPrincipal user,
            IPagoService pagoService,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await pagoService.GetPagosByVentaIdAsync(id, isolation.SoloClienteId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PoliticaVerVentas)
        .WithName("GetPagosVenta");

        // -------------------------------------------------------------------
        // Comprobantes
        // -------------------------------------------------------------------
        group.MapGet("/{id:guid}/comprobante", async (
            Guid id,
            ClaimsPrincipal user,
            IVentaService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.GetComprobanteAsync(id, isolation.SoloClienteId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PoliticaVerVentas)
        .WithName("GetComprobanteVenta");

        group.MapPost("/{id:guid}/comprobante", async (Guid id, RegistrarComprobanteRequest request, ClaimsPrincipal user, IVentaService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.RegistrarComprobanteAsync(id, request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ventas/{id}/comprobante", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.VentasCrear)
        .WithName("RegistrarComprobanteVenta");

        group.MapPut("/{id:guid}/comprobante/anular", async (Guid id, ClaimsPrincipal user, IVentaService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.AnularComprobanteAsync(id, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.VentasAnular)
        .WithName("AnularComprobanteVenta");
    }

    private static Guid? ObtenerUsuarioId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("uid");

        return Guid.TryParse(idStr, out var id) ? id : null;
    }


    private static async Task<bool> PuedeModificarPreciosAsync(
        ClaimsPrincipal user, ApplicationDbContext dbContext)
    {
        if (user.IsInRole(RolesDefinidos.GerenciaAdmin)) return true;

        var usuarioId = ObtenerUsuarioId(user);
        if (!usuarioId.HasValue) return false;

        return await dbContext.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId.Value && ur.Rol.Activo)
            .SelectMany(ur => ur.Rol.RolPermisos)
            .AnyAsync(rp => rp.Permiso.Activo && rp.Permiso.Codigo == PermisosDefinidos.PreciosModificar);
    }

    private static async Task<bool> PuedeAplicarDescuentosAsync(
        ClaimsPrincipal user, ApplicationDbContext dbContext)
    {
        if (user.IsInRole(RolesDefinidos.GerenciaAdmin)) return true;

        var usuarioId = ObtenerUsuarioId(user);
        if (!usuarioId.HasValue) return false;

        return await dbContext.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId.Value && ur.Rol.Activo)
            .SelectMany(ur => ur.Rol.RolPermisos)
            .AnyAsync(rp => rp.Permiso.Activo && rp.Permiso.Codigo == PermisosDefinidos.DescuentosAplicar);
    }
}
