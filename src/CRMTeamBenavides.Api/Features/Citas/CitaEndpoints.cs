using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CRMTeamBenavides.Api.Features.Citas;

public static class CitaEndpoints
{
    public static IEndpointRouteBuilder MapCitaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/citas")
            .WithTags("Citas");

        group.MapGet("/", async (
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin,
            [FromQuery] EstadoCita? estado,
            [FromQuery] Guid? clienteId,
            [FromQuery] Guid? vehiculoId,
            ICitaService citaService,
            ApplicationDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var citas = await citaService.GetAllAsync(
                isolation.SoloClienteId,
                clienteId,
                vehiculoId,
                estado,
                fechaInicio,
                fechaFin,
                ct);

            return Results.Ok(citas);
        })
        .RequireAuthorization(PermisosDefinidos.CitasVer)
        .WithName("ListarCitas");

        group.MapGet("/exportar-excel", async (
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin,
            [FromQuery] EstadoCita? estado,
            [FromQuery] Guid? clienteId,
            [FromQuery] Guid? vehiculoId,
            ICitaService citaService,
            ApplicationDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var bytes = await citaService.ExportarExcelAsync(
                isolation.SoloClienteId,
                clienteId,
                vehiculoId,
                estado,
                fechaInicio,
                fechaFin,
                ct);

            var fileName = $"citas_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        })
        .RequireAuthorization(PermisosDefinidos.CitasVer)
        .WithName("ExportarCitasExcel");

        group.MapGet("/{id:guid}", async (
            Guid id,
            ICitaService citaService,
            ApplicationDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var result = await citaService.GetByIdAsync(id, isolation.SoloClienteId, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new { mensaje = result.Error })
            };
        })
        .RequireAuthorization(PermisosDefinidos.CitasVer)
        .WithName("ObtenerCitaPorId");

        group.MapPost("/", async (
            [FromBody] CrearCitaRequest request,
            ICitaService citaService,
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
            var result = await citaService.CrearAsync(request, usuarioId, isolation.SoloClienteId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/citas/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                ServiceResultStatus.Conflict => Results.Conflict(new { mensaje = result.Error }),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                _ => Results.BadRequest(new { mensaje = result.Error })
            };
        })
        .RequireAuthorization(PermisosDefinidos.CitasCrear)
        .WithName("CrearCita");

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] ActualizarCitaRequest request,
            ICitaService citaService,
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
            var result = await citaService.ActualizarAsync(id, request, usuarioId, isolation.SoloClienteId, ct);

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
        .RequireAuthorization(PermisosDefinidos.CitasEditar)
        .WithName("ActualizarCita");

        group.MapPut("/{id:guid}/reprogramar", async (
            Guid id,
            [FromBody] ReprogramarCitaRequest request,
            ICitaService citaService,
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
            var result = await citaService.ReprogramarAsync(id, request, usuarioId, isolation.SoloClienteId, ct);

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
        .RequireAuthorization(PermisosDefinidos.CitasEditar)
        .WithName("ReprogramarCita");

        group.MapPut("/{id:guid}/estado", async (
            Guid id,
            [FromBody] CambiarEstadoCitaRequest request,
            ICitaService citaService,
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
            var result = await citaService.CambiarEstadoAsync(id, request, usuarioId, isolation.SoloClienteId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                _ => Results.BadRequest(new { mensaje = result.Error })
            };
        })
        .RequireAuthorization(PermisosDefinidos.CitasEditar)
        .WithName("CambiarEstadoCita");

        group.MapPut("/{id:guid}/cancelar", async (
            Guid id,
            [FromBody] CancelarCitaRequest request,
            ICitaService citaService,
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
            var result = await citaService.CancelarAsync(id, request, usuarioId, isolation.SoloClienteId, ct);

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
        .RequireAuthorization(PermisosDefinidos.CitasCancelar)
        .WithName("CancelarCita");

        group.MapPut("/{id:guid}/orden-servicio", async (
            Guid id,
            [FromBody] VincularOrdenCitaRequest request,
            ICitaService citaService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
            var result = await citaService.VincularOrdenAsync(id, request, usuarioId, ct);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { mensaje = result.Error }),
                ServiceResultStatus.Conflict => Results.Conflict(new { mensaje = result.Error }),
                _ => Results.BadRequest(new { mensaje = result.Error })
            };
        })
        .RequireAuthorization(PermisosDefinidos.CitasEditar)
        .WithName("VincularOrdenCita");

        return app;
    }
}
