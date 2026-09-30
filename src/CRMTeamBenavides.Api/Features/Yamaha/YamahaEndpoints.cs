using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Data;

namespace CRMTeamBenavides.Api.Features.Yamaha;

public static class YamahaEndpoints
{
    public static void MapYamahaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/yamaha/mock")
            .RequireAuthorization();

        group.MapGet("/consultar", async (
            string? criterio,
            string? vin,
            string? q,
            ClaimsPrincipal user,
            IYamahaService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);

            // Regla: Los clientes no tienen acceso al catálogo técnico interno de Yamaha
            if (isolation.EsCliente || isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var queryTerm = !string.IsNullOrWhiteSpace(criterio)
                ? criterio
                : (!string.IsNullOrWhiteSpace(vin) ? vin : q);

            if (string.IsNullOrWhiteSpace(queryTerm))
            {
                return Results.BadRequest(new
                {
                    error = "Debe proporcionar un criterio de búsqueda (VIN, serie o modelo Yamaha).",
                    esMock = true
                });
            }

            var resultado = await service.ConsultarAsync(queryTerm);
            if (resultado == null)
            {
                return Results.NotFound(new
                {
                    error = $"No se encontraron registros en el catálogo Mock de Yamaha para '{queryTerm}'.",
                    criterio = queryTerm,
                    esMock = true
                });
            }

            return Results.Ok(resultado);
        })
        .RequireAuthorization(PermisosDefinidos.UnidadesVer)
        .WithName("ConsultarYamahaMock");

        // Alias complementario: /api/yamaha/mock/buscar
        group.MapGet("/buscar", async (
            string? criterio,
            string? vin,
            string? q,
            ClaimsPrincipal user,
            IYamahaService service,
            ApplicationDbContext dbContext) =>
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (isolation.EsCliente || isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var queryTerm = !string.IsNullOrWhiteSpace(criterio)
                ? criterio
                : (!string.IsNullOrWhiteSpace(vin) ? vin : q);

            if (string.IsNullOrWhiteSpace(queryTerm))
            {
                return Results.BadRequest(new
                {
                    error = "Debe proporcionar un criterio de búsqueda (VIN, serie o modelo Yamaha).",
                    esMock = true
                });
            }

            var resultado = await service.ConsultarAsync(queryTerm);
            if (resultado == null)
            {
                return Results.NotFound(new
                {
                    error = $"No se encontraron registros en el catálogo Mock de Yamaha para '{queryTerm}'.",
                    criterio = queryTerm,
                    esMock = true
                });
            }

            return Results.Ok(resultado);
        })
        .RequireAuthorization(PermisosDefinidos.UnidadesVer)
        .WithName("BuscarYamahaMock");
    }
}
