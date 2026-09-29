using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Configuracion;

public static class ConfiguracionEndpoints
{
    public static void MapConfiguracionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/configuracion").RequireAuthorization();

        group.MapGet("/empresa", async (IConfiguracionService service, CancellationToken ct) =>
        {
            var config = await service.ObtenerConfiguracionEmpresaAsync(ct);
            return Results.Ok(config);
        })
        .WithName("GetConfiguracionEmpresa");

        group.MapPut("/empresa", async (
            ActualizarConfiguracionEmpresaRequest request,
            IConfiguracionService service,
            CancellationToken ct) =>
        {
            var result = await service.ActualizarConfiguracionEmpresaAsync(request, ct);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { mensaje = result.Error }),
                _ => Results.StatusCode(500)
            };
        })
        .RequireAuthorization(PermisosDefinidos.ConfiguracionEditar)
        .WithName("ActualizarConfiguracionEmpresa");
    }
}
