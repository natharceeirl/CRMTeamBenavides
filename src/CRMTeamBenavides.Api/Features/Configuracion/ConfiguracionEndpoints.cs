using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Configuracion;

public static class ConfiguracionEndpoints
{
    public static void MapConfiguracionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/configuracion").RequireAuthorization();

        // -------------------------------------------------------------------
        // Empresa
        // -------------------------------------------------------------------
        group.MapGet("/empresa", async (IConfiguracionService service, CancellationToken ct) =>
        {
            var config = await service.ObtenerConfiguracionEmpresaAsync(ct);
            return Results.Ok(config);
        })
        .WithName("GetConfiguracionEmpresa");

        group.MapPut("/empresa", async (
            ActualizarConfiguracionEmpresaRequest request,
            ClaimsPrincipal user,
            IConfiguracionService service,
            CancellationToken ct) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.ActualizarConfiguracionEmpresaAsync(request, usuarioId, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.ConfiguracionEditar)
        .WithName("ActualizarConfiguracionEmpresa");

        // -------------------------------------------------------------------
        // Tipo de Cambio
        // -------------------------------------------------------------------
        group.MapGet("/tipo-cambio", async (IConfiguracionService service, CancellationToken ct) =>
        {
            var tc = await service.ObtenerTipoCambioVigenteAsync(ct);
            return Results.Ok(tc);
        })
        .WithName("GetTipoCambioVigente");

        group.MapPut("/tipo-cambio", async (
            ActualizarTipoCambioRequest request,
            ClaimsPrincipal user,
            IConfiguracionService service,
            CancellationToken ct) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.ActualizarTipoCambioAsync(request, usuarioId, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.ConfiguracionEditar)
        .WithName("ActualizarTipoCambio");

        group.MapPost("/tipo-cambio", async (
            ActualizarTipoCambioRequest request,
            ClaimsPrincipal user,
            IConfiguracionService service,
            CancellationToken ct) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.ActualizarTipoCambioAsync(request, usuarioId, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.ConfiguracionEditar)
        .WithName("RegistrarTipoCambio");

        group.MapGet("/tipo-cambio/historial", async (IConfiguracionService service, CancellationToken ct) =>
        {
            var historial = await service.ObtenerHistorialTipoCambioAsync(ct);
            return Results.Ok(historial);
        })
        .WithName("GetHistorialTipoCambio");

        group.MapGet("/tipo-cambio/convertir", async (
            decimal montoUsd,
            decimal? tipoCambio,
            IConfiguracionService service,
            CancellationToken ct) =>
        {
            var result = await service.ConvertirUsdAPenAsync(montoUsd, tipoCambio, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .WithName("ConvertirUsdAPenGet");

        group.MapPost("/tipo-cambio/convertir", async (
            ConversionMonedaRequest request,
            IConfiguracionService service,
            CancellationToken ct) =>
        {
            var result = await service.ConvertirUsdAPenAsync(request.MontoUsd, request.TipoCambio, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .WithName("ConvertirUsdAPenPost");
    }

    private static Guid? ObtenerUsuarioId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("uid");
        return Guid.TryParse(idStr, out var id) ? id : null;
    }
}
