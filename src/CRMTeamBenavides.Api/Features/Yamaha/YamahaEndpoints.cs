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

        // ---------------------------------------------------------------------
        // Simulación local (Dry-Run) de contratos Yamaha v3 — sin persistencia ni envío HTTP
        // ---------------------------------------------------------------------
        var dryRunGroup = app.MapGroup("/api/yamaha/dry-run")
            .RequireAuthorization(PermisosDefinidos.ConfiguracionEditar);

        dryRunGroup.MapPost("/ventas", (
            IReadOnlyList<YamahaVentaV3Dto>? registros,
            ClaimsPrincipal user,
            IYamahaDryRunService dryRunService,
            ApplicationDbContext dbContext) =>
            EjecutarDryRunAsync(
                registros,
                user,
                dbContext,
                lote => dryRunService.SimularLoteVentas(lote, indentarJson: true)))
        .WithName("SimularDryRunYamahaVentas");

        dryRunGroup.MapPost("/stock", (
            IReadOnlyList<YamahaStockV3Dto>? registros,
            ClaimsPrincipal user,
            IYamahaDryRunService dryRunService,
            ApplicationDbContext dbContext) =>
            EjecutarDryRunAsync(
                registros,
                user,
                dbContext,
                lote => dryRunService.SimularLoteStock(lote, indentarJson: true)))
        .WithName("SimularDryRunYamahaStock");

        dryRunGroup.MapPost("/servicio-tecnico", (
            IReadOnlyList<YamahaServicioTecnicoV3Dto>? registros,
            ClaimsPrincipal user,
            IYamahaDryRunService dryRunService,
            ApplicationDbContext dbContext) =>
            EjecutarDryRunAsync(
                registros,
                user,
                dbContext,
                lote => dryRunService.SimularLoteServicioTecnico(lote, indentarJson: true)))
        .WithName("SimularDryRunYamahaServicioTecnico");
    }

    public const int MaxRegistrosDryRun = 5000;

    /// <summary>
    /// Ejecuta un endpoint de simulación local (Dry-Run) verificando en profundidad:
    /// 1. Autenticación del usuario (<see cref="ClaimsPrincipal"/>).
    /// 2. Permiso explícito <see cref="PermisosDefinidos.ConfiguracionEditar"/>.
    /// 3. Aislamiento de usuario (solo personal administrativo/staff, nunca clientes ni contextos huérfanos).
    /// 4. Validación de entrada del lote (no nulo y dentro del límite máximo de registros).
    /// </summary>
    public static async Task<IResult> EjecutarDryRunAsync<TRegistro>(
        IReadOnlyList<TRegistro>? registros,
        ClaimsPrincipal? user,
        ApplicationDbContext? dbContext,
        Func<IReadOnlyList<TRegistro>, YamahaDryRunResultadoDto<TRegistro>> simulador)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        if (!user.HasClaim("permission", PermisosDefinidos.ConfiguracionEditar) ||
            user.IsInRole(RolesDefinidos.Cliente) ||
            user.HasClaim("permission", PermisosDefinidos.PortalAcceso))
        {
            return Results.Forbid();
        }

        if (dbContext is not null)
        {
            var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
            if (!isolation.UsuarioId.HasValue ||
                !isolation.EsStaff ||
                isolation.EsCliente ||
                isolation.DebeDenegarAcceso)
            {
                return Results.Forbid();
            }
        }
        else
        {
            var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
            var esStaff = user.IsInRole(RolesDefinidos.GerenciaAdmin)
                || user.IsInRole(RolesDefinidos.Recepcion)
                || user.IsInRole(RolesDefinidos.Vendedor);

            if (!usuarioId.HasValue || !esStaff)
            {
                return Results.Forbid();
            }
        }

        if (registros is null)
        {
            return Results.BadRequest(new
            {
                error = "Debe proporcionar un arreglo JSON con los registros a evaluar en modo Dry-Run.",
                esSimulacionDryRun = true
            });
        }

        if (registros.Count > MaxRegistrosDryRun)
        {
            return Results.BadRequest(new
            {
                error = $"El lote excede el límite máximo permitido de {MaxRegistrosDryRun} registros por simulación Dry-Run.",
                totalRecibidos = registros.Count,
                maximoPermitido = MaxRegistrosDryRun,
                esSimulacionDryRun = true
            });
        }

        var resultado = simulador(registros);
        return Results.Ok(resultado);
    }
}
