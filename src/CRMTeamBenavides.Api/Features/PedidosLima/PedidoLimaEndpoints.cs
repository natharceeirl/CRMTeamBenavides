using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Features.PedidosLima;

public static class PedidoLimaEndpoints
{
    public static IEndpointRouteBuilder MapPedidoLimaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pedidos-lima")
            .WithTags("Pedidos Lima");

        group.MapGet("/", async (
            [FromQuery] Guid? clienteId,
            [FromQuery] EstadoPedidoLima? estado,
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin,
            [FromQuery] string? guia,
            IPedidoLimaService pedidoLimaService,
            ApplicationDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var pedidos = await pedidoLimaService.GetAllAsync(
                isolation.SoloClienteId,
                clienteId,
                estado,
                fechaInicio,
                fechaFin,
                guia,
                ct);

            return Results.Ok(pedidos);
        })
        .RequireAuthorization(PermisosDefinidos.PedidosLimaVer)
        .WithName("ListarPedidosLima");

        group.MapGet("/exportar-excel", async (
            [FromQuery] Guid? clienteId,
            [FromQuery] EstadoPedidoLima? estado,
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin,
            IPedidoLimaService pedidoLimaService,
            ApplicationDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var bytes = await pedidoLimaService.ExportarExcelAsync(
                isolation.SoloClienteId,
                clienteId,
                estado,
                fechaInicio,
                fechaFin,
                ct);

            var fileName = $"pedidos_lima_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        })
        .RequireAuthorization(PermisosDefinidos.PedidosLimaVer)
        .WithName("ExportarPedidosLimaExcel");

        group.MapGet("/{id:guid}", async (
            Guid id,
            IPedidoLimaService pedidoLimaService,
            ApplicationDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var result = await pedidoLimaService.GetByIdAsync(id, isolation.SoloClienteId, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new { mensaje = result.Error })
            };
        })
        .RequireAuthorization(PermisosDefinidos.PedidosLimaVer)
        .WithName("ObtenerPedidoLimaPorId");

        group.MapPost("/", async (
            [FromBody] CrearPedidoLimaRequest request,
            IPedidoLimaService pedidoLimaService,
            ApplicationDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var puedeModificarPrecios = await PuedeModificarPreciosAsync(user, dbContext);
            var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
            var result = await pedidoLimaService.CrearAsync(request, usuarioId, isolation.SoloClienteId, puedeModificarPrecios, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/pedidos-lima/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                ServiceResultStatus.Conflict => Results.Conflict(new { mensaje = result.Error }),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                _ => Results.BadRequest(new { mensaje = result.Error })
            };
        })
        .RequireAuthorization(PermisosDefinidos.PedidosLimaCrear)
        .WithName("CrearPedidoLima");

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] ActualizarPedidoLimaRequest request,
            IPedidoLimaService pedidoLimaService,
            ApplicationDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
            var result = await pedidoLimaService.ActualizarAsync(id, request, usuarioId, isolation.SoloClienteId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                ServiceResultStatus.Conflict => Results.Conflict(new { mensaje = result.Error }),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new { mensaje = result.Error })
            };
        })
        .RequireAuthorization(PermisosDefinidos.PedidosLimaEditar)
        .WithName("ActualizarPedidoLima");

        group.MapPut("/{id:guid}/estado", async (
            Guid id,
            [FromBody] CambiarEstadoPedidoLimaRequest request,
            IPedidoLimaService pedidoLimaService,
            ApplicationDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var puedeDespachar = await PuedeDespacharAsync(user, dbContext);
            var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
            var result = await pedidoLimaService.CambiarEstadoAsync(id, request, usuarioId, isolation.SoloClienteId, puedeDespachar, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new { mensaje = result.Error })
            };
        })
        .RequireAuthorization(PermisosDefinidos.PedidosLimaEditar)
        .WithName("CambiarEstadoPedidoLima");

        group.MapPut("/{id:guid}/cancelar", async (
            Guid id,
            [FromBody] CancelarPedidoLimaRequest request,
            IPedidoLimaService pedidoLimaService,
            ApplicationDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
            var result = await pedidoLimaService.CancelarAsync(id, request, usuarioId, isolation.SoloClienteId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                ServiceResultStatus.Conflict => Results.Conflict(new { mensaje = result.Error }),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new { mensaje = result.Error })
            };
        })
        .RequireAuthorization(PermisosDefinidos.PedidosLimaCancelar)
        .WithName("CancelarPedidoLima");

        return app;
    }

    private static async Task<bool> PuedeModificarPreciosAsync(ClaimsPrincipal user, ApplicationDbContext dbContext)
    {
        if (user.IsInRole(RolesDefinidos.GerenciaAdmin)) return true;
        var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
        if (!usuarioId.HasValue) return false;

        return await dbContext.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId.Value && ur.Rol.Activo)
            .SelectMany(ur => ur.Rol.RolPermisos)
            .AnyAsync(rp => rp.Permiso.Activo && rp.Permiso.Codigo == PermisosDefinidos.PreciosModificar);
    }

    private static async Task<bool> PuedeDespacharAsync(ClaimsPrincipal user, ApplicationDbContext dbContext)
    {
        if (user.IsInRole(RolesDefinidos.GerenciaAdmin)) return true;
        var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
        if (!usuarioId.HasValue) return false;

        return await dbContext.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId.Value && ur.Rol.Activo)
            .SelectMany(ur => ur.Rol.RolPermisos)
            .AnyAsync(rp => rp.Permiso.Activo && rp.Permiso.Codigo == PermisosDefinidos.PedidosLimaDespachar);
    }
}
