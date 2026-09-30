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

        group.MapGet("/", async (
            Guid? clienteId,
            EstadoVenta? estado,
            Guid? ordenServicioId,
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

            var ventas = await service.GetAllAsync(clienteId, estado, ordenServicioId, fechaDesde, fechaHasta, isolation.SoloClienteId);
            return Results.Ok(ventas);
        })
        .RequireAuthorization(PoliticaVerVentas)
        .WithName("GetVentas");

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

            var result = await service.CreateAsync(request, puedeModificarPrecios, puedeAplicarDescuentos, isolation.SoloClienteId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ventas/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.VentasCrear)
        .WithName("CreateVenta");

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

        group.MapPut("/{id:guid}/anular", async (Guid id, IVentaService service) =>
        {
            var result = await service.AnularAsync(id);
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

        group.MapPost("/{id:guid}/comprobante", async (Guid id, RegistrarComprobanteRequest request, IVentaService service) =>
        {
            var result = await service.RegistrarComprobanteAsync(id, request);
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

        group.MapPut("/{id:guid}/comprobante/anular", async (Guid id, IVentaService service) =>
        {
            var result = await service.AnularComprobanteAsync(id);
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
