using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
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
            var (soloTecnicoId, soloClienteId) = await ResolverAislamientoAsync(user, dbContext);
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

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IOrdenServicioService service,
            ApplicationDbContext dbContext) =>
        {
            var (soloTecnicoId, soloClienteId) = await ResolverAislamientoAsync(user, dbContext);
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
            var (soloTecnicoId, soloClienteId) = await ResolverAislamientoAsync(user, dbContext);
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
            var (soloTecnicoId, _) = await ResolverAislamientoAsync(user, dbContext);
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
            var (soloTecnicoId, _) = await ResolverAislamientoAsync(user, dbContext);
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
            var (soloTecnicoId, _) = await ResolverAislamientoAsync(user, dbContext);
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
            var (soloTecnicoId, _) = await ResolverAislamientoAsync(user, dbContext);
            var puedeModificarPrecios = await PuedeModificarPreciosAsync(user, dbContext);
            var result = await service.AgregarDetalleAsync(id, request, puedeModificarPrecios, soloTecnicoId);
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
            var (_, soloClienteId) = await ResolverAislamientoAsync(user, dbContext);
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
    }

    private static Guid? ObtenerUsuarioId(ClaimsPrincipal user)
    {
        var subClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subClaim, out var usuarioId) ? usuarioId : null;
    }

    private static async Task<(Guid? soloTecnicoId, Guid? soloClienteId)> ResolverAislamientoAsync(
        ClaimsPrincipal user, ApplicationDbContext dbContext)
    {
        var usuarioId = ObtenerUsuarioId(user);
        if (!usuarioId.HasValue) return (null, null);

        if (user.IsInRole(RolesDefinidos.GerenciaAdmin) || user.IsInRole(RolesDefinidos.Recepcion))
        {
            return (null, null);
        }

        if (user.IsInRole(RolesDefinidos.Tecnico))
        {
            return (usuarioId.Value, null);
        }

        if (user.IsInRole(RolesDefinidos.Cliente))
        {
            var clienteId = await dbContext.Clientes
                .Where(c => c.UsuarioId == usuarioId.Value && c.Activo)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync();
            return (null, clienteId);
        }

        return (null, null);
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
