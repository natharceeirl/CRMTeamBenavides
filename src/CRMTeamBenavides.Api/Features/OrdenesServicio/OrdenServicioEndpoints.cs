using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Ventas;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Features.OrdenesServicio;

public static class OrdenServicioEndpoints
{
    public const string PoliticaVerOrdenes = "PoliticaVerOrdenes";
    public const string PoliticaAprobacionCliente = "PoliticaAprobacionCliente";

    public static void MapOrdenServicioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ordenes-servicio").RequireAuthorization();

        group.MapGet("/", async (
            Guid? vehiculoId,
            EstadoOrdenServicio? estado,
            Guid? clienteId,
            string? busqueda,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            Guid? tecnicoId,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, soloClienteId, debeDenegarAcceso) = await ResolverAislamientoAsync(user, dbContext);
            if (debeDenegarAcceso)
            {
                return Results.Ok(new List<OrdenServicioResponse>());
            }

            var ordenes = await service.GetAllAsync(
                vehiculoId,
                estado,
                clienteId,
                soloTecnicoId,
                soloClienteId,
                busqueda,
                fechaDesde,
                fechaHasta,
                tecnicoId);
            return Results.Ok(ordenes);
        })
        .RequireAuthorization(PoliticaVerOrdenes)
        .WithName("GetOrdenesServicio");

        group.MapGet("/exportar-excel", async (
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EstadoOrdenServicio? estado,
            Guid? tecnicoId,
            ClaimsPrincipal user,
            IReporteService reporteService,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, soloClienteId, debeDenegarAcceso) = await ResolverAislamientoAsync(user, dbContext);
            if (debeDenegarAcceso)
            {
                return Results.Forbid();
            }

            var efectivoTecnicoId = soloTecnicoId ?? tecnicoId;
            bool incluirFinanciero = !soloTecnicoId.HasValue && !soloClienteId.HasValue && (user.IsInRole(RolesDefinidos.GerenciaAdmin) || user.HasClaim("permission", PermisosDefinidos.ReportesVerFinancieros));
            var bytes = await reporteService.ExportarOrdenesServicioExcelAsync(fechaDesde, fechaHasta, estado, efectivoTecnicoId, incluirFinanciero);
            var fileName = $"ordenes_servicio_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        })
        .RequireAuthorization(PoliticaVerOrdenes)
        .WithName("ExportarOrdenesServicioExcel");


        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, soloClienteId, debeDenegarAcceso) = await ResolverAislamientoAsync(user, dbContext);
            if (debeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.GetByIdAsync(id, soloTecnicoId, soloClienteId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PoliticaVerOrdenes)
        .WithName("GetOrdenServicioById");

        group.MapGet("/{id:guid}/historial", async (
            Guid id,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, soloClienteId, debeDenegarAcceso) = await ResolverAislamientoAsync(user, dbContext);
            if (debeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.GetHistorialAsync(id, soloTecnicoId, soloClienteId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PoliticaVerOrdenes)
        .WithName("GetHistorialOrdenServicio");

        group.MapGet("/{id:guid}/formato-atencion", async (
            Guid id,
            string? formato,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, soloClienteId, debeDenegarAcceso) = await ResolverAislamientoAsync(user, dbContext);
            if (debeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.GenerarFormatoAtencionAsync(id, soloTecnicoId, soloClienteId);
            if (result.Status != ServiceResultStatus.Success)
            {
                return result.Status switch
                {
                    ServiceResultStatus.NotFound => Results.NotFound(),
                    ServiceResultStatus.Forbidden => Results.Forbid(),
                    _ => Results.Problem()
                };
            }

            if (string.Equals(formato, "html", StringComparison.OrdinalIgnoreCase))
            {
                var html = FormatoAtencionHtmlBuilder.BuildHtml(result.Data!);
                return Results.Content(html, "text/html", System.Text.Encoding.UTF8);
            }

            return Results.Ok(result.Data);
        })
        .RequireAuthorization(PoliticaVerOrdenes)
        .WithName("GetFormatoAtencionOrdenServicio");

        group.MapGet("/{id:guid}/formato-atencion/imprimir", async (
            Guid id,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, soloClienteId, debeDenegarAcceso) = await ResolverAislamientoAsync(user, dbContext);
            if (debeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await service.GenerarFormatoAtencionAsync(id, soloTecnicoId, soloClienteId);
            if (result.Status != ServiceResultStatus.Success)
            {
                return result.Status switch
                {
                    ServiceResultStatus.NotFound => Results.NotFound(),
                    ServiceResultStatus.Forbidden => Results.Forbid(),
                    _ => Results.Problem()
                };
            }

            var html = FormatoAtencionHtmlBuilder.BuildHtml(result.Data!);
            return Results.Content(html, "text/html", System.Text.Encoding.UTF8);
        })
        .RequireAuthorization(PoliticaVerOrdenes)
        .WithName("ImprimirFormatoAtencionOrdenServicio");

        group.MapPost("/", async (
            AperturaOrdenServicioRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.CreateAperturaAsync(request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ordenes-servicio/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesCrear)
        .WithName("CreateAperturaOrdenServicio");

        group.MapPost("/apertura", async (
            AperturaOrdenServicioRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.CreateAperturaAsync(request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ordenes-servicio/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesCrear)
        .WithName("CreateAperturaOrdenServicioAlias");

        group.MapPut("/{id:guid}", async (
            Guid id,
            ActualizarOrdenServicioRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, _, _) = await ResolverAislamientoAsync(user, dbContext);
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.ActualizarAsync(id, request, soloTecnicoId, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesEditar)
        .WithName("ActualizarOrdenServicio");

        group.MapPut("/{id:guid}/diagnostico", async (
            Guid id,
            RegistrarDiagnosticoRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, _, _) = await ResolverAislamientoAsync(user, dbContext);
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.RegistrarDiagnosticoAsync(id, request, soloTecnicoId, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesDiagnostico)
        .WithName("RegistrarDiagnosticoOrdenServicio");

        group.MapPost("/{id:guid}/diagnostico", async (
            Guid id,
            RegistrarDiagnosticoRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, _, _) = await ResolverAislamientoAsync(user, dbContext);
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.RegistrarDiagnosticoAsync(id, request, soloTecnicoId, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesDiagnostico)
        .WithName("RegistrarDiagnosticoOrdenServicioPost");

        group.MapPost("/{id:guid}/detalles", async (
            Guid id,
            AgregarDetalleServicioRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, _, _) = await ResolverAislamientoAsync(user, dbContext);
            var puedeModificarPrecios = await PuedeModificarPreciosAsync(user, dbContext);
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.AgregarDetalleAsync(id, request, puedeModificarPrecios, soloTecnicoId, usuarioId);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ordenes-servicio/{id}/detalles/{result.Data!.Id}", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesAgregarItems)
        .WithName("AgregarDetalleOrdenServicio");

        group.MapDelete("/{id:guid}/detalles/{detalleId:guid}", async (Guid id, Guid detalleId, IOrdenServicioService service) =>
        {
            var result = await service.EliminarDetalleAsync(id, detalleId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(new { message = "Detalle eliminado correctamente." }),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesEditar)
        .WithName("EliminarDetalleOrdenServicio");

        group.MapPut("/{id:guid}/estado", async (
            Guid id,
            CambiarEstadoOrdenServicioRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service) =>
        {
            var esTecnico = user.IsInRole(RolesDefinidos.Tecnico)
                && !user.IsInRole(RolesDefinidos.GerenciaAdmin)
                && !user.IsInRole(RolesDefinidos.Recepcion);

            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.CambiarEstadoAsync(id, request, esTecnico, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesCambiarEstado)
        .WithName("CambiarEstadoOrdenServicio");

        group.MapPost("/{id:guid}/estado", async (
            Guid id,
            CambiarEstadoOrdenServicioRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service) =>
        {
            var esTecnico = user.IsInRole(RolesDefinidos.Tecnico)
                && !user.IsInRole(RolesDefinidos.GerenciaAdmin)
                && !user.IsInRole(RolesDefinidos.Recepcion);

            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.CambiarEstadoAsync(id, request, esTecnico, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.OrdenesCambiarEstado)
        .WithName("CambiarEstadoOrdenServicioPost");

        var handleAsignarTecnico = async (
            Guid id,
            AsignarTecnicoRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.AsignarTecnicoAsync(id, request.TecnicoEfectivoId, request.Observaciones, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        };

        group.MapPut("/{id:guid}/asignar-tecnico", handleAsignarTecnico)
            .RequireAuthorization(PermisosDefinidos.OrdenesAsignarTecnico)
            .WithName("AsignarTecnicoOrdenServicio");

        group.MapPost("/{id:guid}/asignar-tecnico", handleAsignarTecnico)
            .RequireAuthorization(PermisosDefinidos.OrdenesAsignarTecnico)
            .WithName("AsignarTecnicoOrdenServicioPost");

        var handleAprobacionCliente = async (
            Guid id,
            ResponderPresupuestoClienteRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (_, soloClienteId, debeDenegarAcceso) = await ResolverAislamientoAsync(user, dbContext);
            if (debeDenegarAcceso)
            {
                return Results.Json(new { error = "Usuario cliente sin cliente activo asociado." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.ResponderPresupuestoClienteAsync(id, request, soloClienteId, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        };

        group.MapPut("/{id:guid}/aprobacion-cliente", handleAprobacionCliente)
            .RequireAuthorization(PoliticaAprobacionCliente)
            .WithName("AprobacionClienteOrdenServicio");

        group.MapPost("/{id:guid}/aprobacion-cliente", handleAprobacionCliente)
            .RequireAuthorization(PoliticaAprobacionCliente)
            .WithName("AprobacionClienteOrdenServicioPost");

        var handleSolicitarAprobacionGerencia = async (
            Guid id,
            SolicitarAprobacionGerenciaRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.SolicitarAprobacionGerenciaAsync(id, request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        };

        group.MapPut("/{id:guid}/solicitar-aprobacion-gerencia", handleSolicitarAprobacionGerencia)
            .RequireAuthorization(PermisosDefinidos.OrdenesEditar)
            .WithName("SolicitarAprobacionGerenciaOrdenServicio");

        group.MapPost("/{id:guid}/solicitar-aprobacion-gerencia", handleSolicitarAprobacionGerencia)
            .RequireAuthorization(PermisosDefinidos.OrdenesEditar)
            .WithName("SolicitarAprobacionGerenciaOrdenServicioPost");

        var handleAprobacionGerencia = async (
            Guid id,
            AprobacionGerenciaRequest request,
            ClaimsPrincipal user,
            IOrdenServicioService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.AprobacionGerenciaAsync(id, request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Forbidden => Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        };

        group.MapPut("/{id:guid}/aprobacion-gerencia", handleAprobacionGerencia)
            .RequireAuthorization(PermisosDefinidos.OrdenesAprobarGerencia)
            .WithName("AprobacionGerenciaOrdenServicio");

        group.MapPost("/{id:guid}/aprobacion-gerencia", handleAprobacionGerencia)
            .RequireAuthorization(PermisosDefinidos.OrdenesAprobarGerencia)
            .WithName("AprobacionGerenciaOrdenServicioPost");

        // -------------------------------------------------------------------
        // Pagos y Anticipos sobre Orden de Servicio
        // -------------------------------------------------------------------
        group.MapPost("/{id:guid}/pagos", async (
            Guid id,
            RegistrarPagoRequest request,
            ClaimsPrincipal user,
            IPagoService pagoService) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await pagoService.RegistrarPagoOrdenServicioAsync(id, request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/ordenes-servicio/{id}/pagos/{result.Data!.Id}", result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.VentasCrear)
        .WithName("RegistrarPagoOrdenServicio");

        group.MapGet("/{id:guid}/pagos", async (
            Guid id,
            ClaimsPrincipal user,
            IPagoService pagoService,
            ApplicationDbContext dbContext) =>
        {
            var (_, soloClienteId, debeDenegarAcceso) = await ResolverAislamientoAsync(user, dbContext);
            if (debeDenegarAcceso)
            {
                return Results.NotFound();
            }

            var result = await pagoService.GetPagosByOrdenServicioIdAsync(id, soloClienteId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PoliticaVerOrdenes)
        .WithName("GetPagosOrdenServicio");
    }

    private static Guid? ObtenerUsuarioId(ClaimsPrincipal user)
    {
        return UserIsolationHelper.ObtenerUsuarioId(user);
    }

    private static async Task<(Guid? soloTecnicoId, Guid? soloClienteId, bool debeDenegarAcceso)> ResolverAislamientoAsync(
        ClaimsPrincipal user, ApplicationDbContext dbContext)
    {
        var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext);
        var soloTecnicoId = isolation.EsTecnico ? isolation.UsuarioId : null;
        var soloClienteId = isolation.SoloClienteId;
        return (soloTecnicoId, soloClienteId, isolation.DebeDenegarAcceso);
    }

    private static async Task<bool> PuedeModificarPreciosAsync(
        ClaimsPrincipal user, ApplicationDbContext dbContext)
    {
        if (user.IsInRole(RolesDefinidos.GerenciaAdmin) || user.IsInRole(RolesDefinidos.Recepcion))
        {
            return true;
        }

        var usuarioId = ObtenerUsuarioId(user);
        if (!usuarioId.HasValue) return false;

        return await dbContext.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId.Value && ur.Rol.Activo)
            .SelectMany(ur => ur.Rol.RolPermisos)
            .AnyAsync(rp => rp.Permiso.Activo && rp.Permiso.Codigo == PermisosDefinidos.PreciosModificar);
    }
}
