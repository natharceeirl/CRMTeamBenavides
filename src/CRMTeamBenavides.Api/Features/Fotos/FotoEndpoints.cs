using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CRMTeamBenavides.Api.Features.Fotos;

public static class FotoEndpoints
{
    public static void MapFotoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ordenes-servicio/{id:guid}/fotos")
            .RequireAuthorization();

        // 1. Subir fotografía
        group.MapPost("/", async (
            Guid id,
            [FromForm] IFormFile? archivo,
            [FromForm] EtapaFotoOrdenServicio etapa,
            [FromForm] string? observacion,
            ClaimsPrincipal user,
            IFotoOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso || isolation.EsCliente)
            {
                return Results.Forbid();
            }

            if (!isolation.UsuarioId.HasValue)
            {
                return Results.Unauthorized();
            }

            if (archivo == null || archivo.Length == 0)
            {
                return Results.BadRequest(new { error = "Debe proporcionar un archivo de imagen válido." });
            }

            await using var stream = archivo.OpenReadStream();
            var result = await service.SubirFotoAsync(
                id,
                stream,
                archivo.FileName,
                archivo.ContentType,
                archivo.Length,
                etapa,
                observacion,
                isolation.UsuarioId.Value,
                isolation);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created(
                    $"/api/ordenes-servicio/{id}/fotos/{result.Data!.Id}", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(new { error = result.Error }),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem(result.Error ?? "Error al subir la fotografía.")
            };
        })
        .DisableAntiforgery()
        .WithName("SubirFotoOrdenServicio");

        // 2. Listar fotografías de la orden
        group.MapGet("/", async (
            Guid id,
            ClaimsPrincipal user,
            IFotoOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.GetFotosByOrdenIdAsync(id, isolation);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(OrdenServicioEndpoints.PoliticaVerOrdenes)
        .WithName("GetFotosOrdenServicio");

        // 3. Consultar detalle / metadata de una fotografía
        group.MapGet("/{fotoId:guid}", async (
            Guid id,
            Guid fotoId,
            ClaimsPrincipal user,
            IFotoOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.GetFotoByIdAsync(id, fotoId, isolation);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(OrdenServicioEndpoints.PoliticaVerOrdenes)
        .WithName("GetFotoById");

        // 4. Descargar archivo binario de la fotografía
        group.MapGet("/{fotoId:guid}/archivo", async (
            Guid id,
            Guid fotoId,
            ClaimsPrincipal user,
            IFotoOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.GetArchivoFotoAsync(id, fotoId, isolation);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.File(
                    result.Data.Stream,
                    result.Data.ContentType,
                    result.Data.NombreOriginal),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(OrdenServicioEndpoints.PoliticaVerOrdenes)
        .WithName("GetArchivoFoto");

        // 5. Eliminar fotografía
        group.MapDelete("/{fotoId:guid}", async (
            Guid id,
            Guid fotoId,
            ClaimsPrincipal user,
            IFotoOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.DebeDenegarAcceso || isolation.EsCliente)
            {
                return Results.Forbid();
            }

            if (!isolation.UsuarioId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await service.EliminarFotoAsync(id, fotoId, isolation.UsuarioId.Value, isolation);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Forbid(),
                _ => Results.Problem()
            };
        })
        .WithName("EliminarFotoOrdenServicio");
    }
}
