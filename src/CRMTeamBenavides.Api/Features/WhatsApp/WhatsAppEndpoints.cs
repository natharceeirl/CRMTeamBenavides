using System.Security.Claims;
using System.Text;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.WhatsApp;
using CRMTeamBenavides.Api.Services.WhatsApp.IA;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Features.WhatsApp;

public static class WhatsAppEndpoints
{
    public static void MapWhatsAppEndpoints(this IEndpointRouteBuilder app)
    {
        // =====================================================================
        // WEBHOOK PÚBLICO DE META WHATSAPP CLOUD API
        // Protegido por hub.verify_token (GET) y firma HMAC-SHA256 (POST).
        // =====================================================================
        var webhookGroup = app.MapGroup("/api/whatsapp/webhook")
            .AllowAnonymous();

        webhookGroup.MapGet("/", (
            HttpRequest request,
            IOptionsMonitor<WhatsAppSettings> settingsMonitor,
            ILoggerFactory loggerFactory) =>
        {
            var settings = settingsMonitor.CurrentValue;
            if (!settings.Enabled)
            {
                return Results.Json(
                    new { error = "Integración WhatsApp deshabilitada." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var mode = request.Query["hub.mode"].ToString();
            var verifyToken = request.Query["hub.verify_token"].ToString();
            var challenge = request.Query["hub.challenge"].ToString();

            var esValido = WhatsAppWebhookSecurity.ValidarVerificacionGet(
                mode,
                verifyToken,
                challenge,
                settings.WebhookVerifyToken);

            if (!esValido)
            {
                var logger = loggerFactory.CreateLogger("WhatsAppWebhook");
                logger.LogWarning("WhatsApp webhook GET rechazado: verificación de token o modo inválida.");
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Text(challenge, "text/plain", Encoding.UTF8, StatusCodes.Status200OK);
        })
        .WithName("VerificarWebhookWhatsApp");

        webhookGroup.MapPost("/", async (
            HttpContext httpContext,
            IWhatsAppWebhookService webhookService,
            IOptionsMonitor<WhatsAppSettings> settingsMonitor,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var settings = settingsMonitor.CurrentValue;
            if (!settings.Enabled)
            {
                return Results.Json(
                    new { error = "Integración WhatsApp deshabilitada." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var maxBytes = settings.MaxWebhookPayloadBytes > 0 ? settings.MaxWebhookPayloadBytes : 262_144;
            if (httpContext.Request.ContentLength.HasValue && httpContext.Request.ContentLength.Value > maxBytes)
            {
                return Results.Json(
                    new { error = $"El payload del webhook excede el límite máximo permitido de {maxBytes} bytes." },
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            var (excedeLimite, rawBody) = await LeerCuerpoCrudoConLimiteAsync(httpContext.Request.Body, maxBytes, ct);
            if (excedeLimite)
            {
                return Results.Json(
                    new { error = $"El payload del webhook excede el límite máximo permitido de {maxBytes} bytes." },
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            var firmaHeader = httpContext.Request.Headers[WhatsAppWebhookSecurity.HeaderFirmaMeta].FirstOrDefault();
            var firmaValida = WhatsAppWebhookSecurity.ValidarFirmaHmacSha256(
                rawBody,
                firmaHeader,
                settings.AppSecret);

            if (!firmaValida)
            {
                var logger = loggerFactory.CreateLogger("WhatsAppWebhook");
                logger.LogWarning("WhatsApp webhook POST rechazado: firma X-Hub-Signature-256 ausente o inválida.");
                return Results.Unauthorized();
            }

            var resultado = await webhookService.ProcesarNotificacionAsync(rawBody, ct);
            if (!resultado.EsValido)
            {
                return Results.BadRequest(new { error = resultado.Error });
            }

            return Results.Ok(new
            {
                recibido = true,
                resultado.MensajesProcesados,
                resultado.MensajesDuplicados,
                resultado.EstadosActualizados,
                resultado.EstadosIgnorados,
                resultado.EventosDesconocidos
            });
        })
        .WithName("RecibirWebhookWhatsApp");

        // =====================================================================
        // ENDPOINTS ADMINISTRATIVOS DEL CRM (Requiere JWT + Permisos WhatsApp)
        // =====================================================================
        var adminGroup = app.MapGroup("/api/whatsapp/admin")
            .RequireAuthorization();

        adminGroup.MapGet("/estado", async (
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppVer);
            if (denegado is not null) return denegado;

            return Results.Ok(service.ObtenerEstadoIntegracion());
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .WithName("ObtenerEstadoIntegracionWhatsApp");

        adminGroup.MapGet("/ia/estado", async (
            ClaimsPrincipal user,
            [FromServices] IOptionsMonitor<WhatsAppAiSettings> aiSettingsMonitor,
            [FromServices] IOptionsMonitor<WhatsAppSettings> waSettingsMonitor,
            [FromServices] IWhatsAppProvider waProvider,
            [FromServices] IWhatsAppVerificadorIdentidad verificadorIdentidad,
            [FromServices] ApplicationDbContext dbContext,
            IServiceProvider serviceProvider,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppVer);
            if (denegado is not null) return denegado;

            var s = aiSettingsMonitor.CurrentValue;
            var wa = waSettingsMonitor.CurrentValue;
            var aiProvider = serviceProvider.GetService<IWhatsAppAiProvider>();

            // F3-H03: reflejar siempre los servicios realmente resueltos por DI (nunca inventar "Mock" ni "operativo").
            var proveedorConfigurado = s.Provider?.Trim() ?? string.Empty;
            var proveedorEfectivo = aiProvider?.NombreProveedor ?? DisabledExternalWhatsAppAiProvider.NombreProveedorDeshabilitado;
            var proveedorOperativo = s.Enabled
                && aiProvider is not null
                && aiProvider is not DisabledExternalWhatsAppAiProvider;
            var verificacionIdentidadHabilitada = verificadorIdentidad is not WhatsAppVerificadorIdentidadNoImplementado;
            var whatsAppHabilitadoParaEnvios = wa.ValidarConfiguracionEnvio() is null;

            return Results.Ok(new WhatsAppEstadoIaResponse(
                Enabled: s.Enabled,
                ProveedorConfigurado: proveedorConfigurado,
                ProveedorEfectivo: proveedorEfectivo,
                ProveedorOperativo: proveedorOperativo,
                Model: string.IsNullOrWhiteSpace(s.Model) ? "mock-deterministic-v1" : s.Model.Trim(),
                TieneCredencialesExternasConfiguradas: s.TieneCredencialesExternasConfiguradas,
                VerificacionIdentidadHabilitada: verificacionIdentidadHabilitada,
                WhatsAppHabilitadoParaEnvios: whatsAppHabilitadoParaEnvios,
                WhatsAppProveedorEfectivo: waProvider.NombreProveedor,
                AllowFaqTools: s.AllowFaqTools,
                AllowPublicCatalogTools: s.AllowPublicCatalogTools,
                MaxInputChars: s.ObtenerMaxInputCharsSeguro(),
                MaxOutputChars: s.ObtenerMaxOutputCharsSeguro(),
                MaxOutputTokens: s.ObtenerMaxOutputTokensSeguro(),
                TimeoutSeconds: s.ObtenerTimeoutSegundosSeguro()));
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .WithName("ObtenerEstadoIaWhatsApp");

        adminGroup.MapGet("/conversaciones", async (
            int? pagina,
            int? tamanoPagina,
            EstadoConversacionWhatsApp? estado,
            ModoAtencionWhatsApp? modoAtencion,
            bool? soloSinAsignar,
            string? busqueda,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppVer);
            if (denegado is not null) return denegado;

            var resultado = await service.ListarConversacionesAsync(
                pagina ?? 1,
                tamanoPagina ?? 20,
                estado,
                modoAtencion,
                soloSinAsignar,
                busqueda,
                ct);

            return Results.Ok(resultado);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .WithName("ListarConversacionesWhatsApp");

        adminGroup.MapGet("/conversaciones/{id:guid}", async (
            Guid id,
            bool? incluirDocumento,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppVer);
            if (denegado is not null) return denegado;

            var puedeVerDocumento = incluirDocumento == true
                && await TienePermisoEnBdAsync(usuarioId!.Value, PermisosDefinidos.ClientesVer, dbContext, ct);

            var result = await service.ObtenerConversacionAsync(id, puedeVerDocumento, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .WithName("ObtenerConversacionWhatsApp");

        adminGroup.MapGet("/conversaciones/{id:guid}/mensajes", async (
            Guid id,
            int? pagina,
            int? tamanoPagina,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppVer);
            if (denegado is not null) return denegado;

            var result = await service.ListarMensajesAsync(id, pagina ?? 1, tamanoPagina ?? 50, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .WithName("ListarMensajesWhatsApp");

        adminGroup.MapPut("/conversaciones/{id:guid}/asignar", async (
            Guid id,
            WhatsAppAsignarAgenteRequest request,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppGestionar);
            if (denegado is not null) return denegado;

            var result = await service.AsignarAgenteAsync(id, request, usuarioId!.Value, incluirDocumentoCompleto: false, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppGestionar)
        .WithName("AsignarAgenteConversacionWhatsApp");

        adminGroup.MapPut("/conversaciones/{id:guid}/modo", async (
            Guid id,
            WhatsAppCambiarModoRequest request,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppGestionar);
            if (denegado is not null) return denegado;

            var result = await service.CambiarModoAtencionAsync(id, request, usuarioId!.Value, incluirDocumentoCompleto: false, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppGestionar)
        .WithName("CambiarModoAtencionWhatsApp");

        adminGroup.MapPut("/conversaciones/{id:guid}/estado", async (
            Guid id,
            WhatsAppCambiarEstadoRequest request,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppGestionar);
            if (denegado is not null) return denegado;

            var result = await service.CambiarEstadoAsync(id, request, usuarioId!.Value, incluirDocumentoCompleto: false, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppGestionar)
        .WithName("CambiarEstadoConversacionWhatsApp");

        adminGroup.MapPut("/conversaciones/{id:guid}/vincular-cliente", async (
            Guid id,
            WhatsAppVincularClienteRequest request,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppGestionar);
            if (denegado is not null) return denegado;

            var result = await service.VincularClienteAsync(id, request, usuarioId!.Value, incluirDocumentoCompleto: false, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppGestionar)
        .WithName("VincularClienteConversacionWhatsApp");

        adminGroup.MapDelete("/conversaciones/{id:guid}/vincular-cliente", async (
            Guid id,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppGestionar);
            if (denegado is not null) return denegado;

            var result = await service.DesvincularClienteAsync(id, usuarioId!.Value, incluirDocumentoCompleto: false, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppGestionar)
        .WithName("DesvincularClienteConversacionWhatsApp");

        adminGroup.MapGet("/conversaciones/{id:guid}/vinculacion", async (
            Guid id,
            bool? incluirDocumento,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppVer);
            if (denegado is not null) return denegado;

            var puedeVerDocumento = incluirDocumento == true
                && await TienePermisoEnBdAsync(usuarioId!.Value, PermisosDefinidos.ClientesVer, dbContext, ct);
            var result = await service.ObtenerVinculacionClienteAsync(id, puedeVerDocumento, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .WithName("ObtenerVinculacionClienteWhatsApp");

        adminGroup.MapPost("/conversaciones/{id:guid}/identificar-cliente", async (
            Guid id,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppGestionar);
            if (denegado is not null) return denegado;

            var result = await service.ReintentarIdentificacionClienteAsync(id, usuarioId!.Value, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppGestionar)
        .WithName("ReintentarIdentificacionClienteWhatsApp");

        adminGroup.MapGet("/clientes/buscar", async (
            string? busqueda,
            int? pagina,
            int? tamanoPagina,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(
                user,
                dbContext,
                ct,
                PermisosDefinidos.WhatsAppGestionar,
                PermisosDefinidos.ClientesVer);
            if (denegado is not null) return denegado;

            var result = await service.BuscarClientesParaVincularAsync(
                busqueda,
                pagina ?? 1,
                tamanoPagina ?? 15,
                ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppGestionar)
        .RequireAuthorization(PermisosDefinidos.ClientesVer)
        .WithName("BuscarClientesParaVincularWhatsApp");

        // --- Adaptadores administrativos de consulta controlada del CRM vinculados a una conversación ---
        // Cada ruta exige whatsapp.ver ADEMÁS del permiso granular del módulo CRM consultado (H-03.B).

        adminGroup.MapGet("/conversaciones/{id:guid}/crm/perfil", async (
            Guid id,
            bool? incluirDocumento,
            ClaimsPrincipal user,
            IWhatsAppCrmConsultaService crmConsulta,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(
                user,
                dbContext,
                ct,
                PermisosDefinidos.WhatsAppVer,
                PermisosDefinidos.ClientesVer);
            if (denegado is not null) return denegado;

            var contexto = WhatsAppContextoConsultaCrm.Administrativo(
                permitirDocumentoCompleto: incluirDocumento == true);

            var result = await crmConsulta.ConsultarPerfilClienteAsync(id, contexto, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .RequireAuthorization(PermisosDefinidos.ClientesVer)
        .WithName("ConsultarPerfilClienteWhatsApp");

        adminGroup.MapGet("/conversaciones/{id:guid}/crm/vehiculos", async (
            Guid id,
            ClaimsPrincipal user,
            IWhatsAppCrmConsultaService crmConsulta,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(
                user,
                dbContext,
                ct,
                PermisosDefinidos.WhatsAppVer,
                PermisosDefinidos.UnidadesVer);
            if (denegado is not null) return denegado;

            var result = await crmConsulta.ConsultarVehiculosClienteAsync(
                id,
                WhatsAppContextoConsultaCrm.Administrativo(),
                ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .RequireAuthorization(PermisosDefinidos.UnidadesVer)
        .WithName("ConsultarVehiculosClienteWhatsApp");

        adminGroup.MapGet("/conversaciones/{id:guid}/crm/ordenes", async (
            Guid id,
            bool? incluirFinalizadasYCanceladas,
            int? limite,
            ClaimsPrincipal user,
            IWhatsAppCrmConsultaService crmConsulta,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(
                user,
                dbContext,
                ct,
                PermisosDefinidos.WhatsAppVer,
                PermisosDefinidos.OrdenesVerTodas);
            if (denegado is not null) return denegado;

            var result = await crmConsulta.ConsultarOrdenesServicioClienteAsync(
                id,
                incluirFinalizadasYCanceladas ?? true,
                limite ?? 10,
                WhatsAppContextoConsultaCrm.Administrativo(),
                ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .RequireAuthorization(PermisosDefinidos.OrdenesVerTodas)
        .WithName("ConsultarOrdenesClienteWhatsApp");

        adminGroup.MapGet("/conversaciones/{id:guid}/crm/ordenes/{ordenId:guid}", async (
            Guid id,
            Guid ordenId,
            ClaimsPrincipal user,
            IWhatsAppCrmConsultaService crmConsulta,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(
                user,
                dbContext,
                ct,
                PermisosDefinidos.WhatsAppVer,
                PermisosDefinidos.OrdenesVerTodas);
            if (denegado is not null) return denegado;

            var result = await crmConsulta.ConsultarEstadoOrdenServicioAsync(
                id,
                ordenServicioId: ordenId,
                contexto: WhatsAppContextoConsultaCrm.Administrativo(),
                ct: ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .RequireAuthorization(PermisosDefinidos.OrdenesVerTodas)
        .WithName("ConsultarEstadoOrdenClienteWhatsApp");

        adminGroup.MapGet("/conversaciones/{id:guid}/crm/citas", async (
            Guid id,
            bool? soloActivas,
            int? limite,
            ClaimsPrincipal user,
            IWhatsAppCrmConsultaService crmConsulta,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(
                user,
                dbContext,
                ct,
                PermisosDefinidos.WhatsAppVer,
                PermisosDefinidos.CitasVer);
            if (denegado is not null) return denegado;

            var result = await crmConsulta.ConsultarCitasClienteAsync(
                id,
                soloActivas ?? false,
                limite ?? 10,
                WhatsAppContextoConsultaCrm.Administrativo(),
                ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .RequireAuthorization(PermisosDefinidos.CitasVer)
        .WithName("ConsultarCitasClienteWhatsApp");

        adminGroup.MapGet("/crm/catalogo", async (
            string? busqueda,
            bool? incluirProductos,
            bool? incluirServicios,
            int? limite,
            ClaimsPrincipal user,
            IWhatsAppCrmConsultaService crmConsulta,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var incProd = incluirProductos ?? true;
            var incServ = incluirServicios ?? true;

            var permisos = new List<string> { PermisosDefinidos.WhatsAppVer };
            if (incProd) permisos.Add(PermisosDefinidos.InventarioVer);
            if (incServ) permisos.Add(PermisosDefinidos.ServiciosVer);

            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(
                user,
                dbContext,
                ct,
                permisos.ToArray());
            if (denegado is not null) return denegado;

            if (!incProd && !incServ)
            {
                var tieneAlguno = await TienePermisoEnBdAsync(usuarioId!.Value, PermisosDefinidos.InventarioVer, dbContext, ct)
                    || await TienePermisoEnBdAsync(usuarioId.Value, PermisosDefinidos.ServiciosVer, dbContext, ct);
                if (!tieneAlguno)
                {
                    return Results.Forbid();
                }
            }

            var catalogo = await crmConsulta.ConsultarProductosYServiciosAsync(
                busqueda,
                incProd,
                incServ,
                limite ?? 15,
                WhatsAppModoConsultaCrm.AdministrativoInterno,
                ct);
            return Results.Ok(catalogo);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .WithName("ConsultarCatalogoWhatsApp");

        adminGroup.MapPost("/conversaciones/{id:guid}/mensajes", async (
            Guid id,
            WhatsAppEnviarMensajeRequest request,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct, PermisosDefinidos.WhatsAppGestionar);
            if (denegado is not null) return denegado;

            var result = await service.EnviarMensajeManualAsync(id, request, usuarioId!.Value, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppGestionar)
        .WithName("EnviarMensajeManualWhatsApp");
    }

    /// <summary>
    /// Valida contra base de datos que el usuario esté activo, posea roles activos no correspondientes
    /// al portal de clientes (soportando roles personalizados de personal interno) y conserve vigentes
    /// todos los permisos requeridos (sin confiar en claims de permisos o roles que hayan podido revocarse).
    /// </summary>
    private static async Task<(IResult? Denegado, Guid? UsuarioId)> ValidarPersonalAutorizadoAsync(
        ClaimsPrincipal user,
        ApplicationDbContext dbContext,
        CancellationToken ct,
        params string[] permisosRequeridos)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return (Results.Unauthorized(), null);
        }

        var usuarioId = UserIsolationHelper.ObtenerUsuarioId(user);
        if (!usuarioId.HasValue)
        {
            return (Results.Forbid(), null);
        }

        var usuarioActivo = await dbContext.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Id == usuarioId.Value && u.Activo, ct);

        if (!usuarioActivo)
        {
            return (Results.Forbid(), null);
        }

        var rolesActivos = await dbContext.UsuarioRoles
            .AsNoTracking()
            .Where(ur => ur.UsuarioId == usuarioId.Value && ur.Rol.Activo)
            .Select(ur => ur.Rol.Nombre)
            .ToListAsync(ct);

        if (rolesActivos.Count == 0)
        {
            return (Results.Forbid(), null);
        }

        var esGerenciaEnBd = rolesActivos.Contains(RolesDefinidos.GerenciaAdmin);
        var esRolCliente = rolesActivos.Contains(RolesDefinidos.Cliente)
            || user.IsInRole(RolesDefinidos.Cliente);

        // El rol Cliente del portal jamás puede ingresar a endpoints administrativos de WhatsApp.
        if (esRolCliente && !esGerenciaEnBd)
        {
            return (Results.Forbid(), null);
        }

        if (!esGerenciaEnBd && permisosRequeridos.Length > 0)
        {
            var permisosVigentes = await dbContext.UsuarioRoles
                .AsNoTracking()
                .Where(ur => ur.UsuarioId == usuarioId.Value && ur.Rol.Activo)
                .SelectMany(ur => ur.Rol.RolPermisos)
                .Where(rp => rp.Permiso.Activo && permisosRequeridos.Contains(rp.Permiso.Codigo))
                .Select(rp => rp.Permiso.Codigo)
                .Distinct()
                .ToListAsync(ct);

            foreach (var requerido in permisosRequeridos)
            {
                if (!permisosVigentes.Contains(requerido, StringComparer.Ordinal))
                {
                    return (Results.Forbid(), null);
                }
            }
        }

        return (null, usuarioId.Value);
    }

    private static async Task<bool> TienePermisoEnBdAsync(
        Guid usuarioId,
        string codigoPermiso,
        ApplicationDbContext dbContext,
        CancellationToken ct)
    {
        var esGerenciaEnBd = await dbContext.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(ur => ur.UsuarioId == usuarioId
                         && ur.Rol.Activo
                         && ur.Rol.Nombre == RolesDefinidos.GerenciaAdmin, ct);

        if (esGerenciaEnBd)
        {
            return true;
        }

        return await dbContext.UsuarioRoles
            .AsNoTracking()
            .Where(ur => ur.UsuarioId == usuarioId && ur.Rol.Activo)
            .SelectMany(ur => ur.Rol.RolPermisos)
            .AnyAsync(rp => rp.Permiso.Activo && rp.Permiso.Codigo == codigoPermiso, ct);
    }

    private static IResult MapearResultado<T>(ServiceResult<T> result) => result.Status switch
    {
        ServiceResultStatus.Success => Results.Ok(result.Data),
        ServiceResultStatus.NotFound => Results.NotFound(new { error = result.Error ?? "Recurso no encontrado." }),
        ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
        ServiceResultStatus.Forbidden => Results.Forbid(),
        ServiceResultStatus.Conflict => Results.Conflict(new { error = result.Error }),
        _ => Results.Problem()
    };

    private static async Task<(bool ExcedeLimite, byte[] Bytes)> LeerCuerpoCrudoConLimiteAsync(
        Stream body,
        int maxBytes,
        CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[Math.Min(8192, maxBytes + 1)];
        int totalLeidos = 0;

        while (true)
        {
            var leidos = await body.ReadAsync(buffer, ct);
            if (leidos == 0)
            {
                break;
            }

            totalLeidos += leidos;
            if (totalLeidos > maxBytes)
            {
                return (true, Array.Empty<byte>());
            }

            ms.Write(buffer, 0, leidos);
        }

        return (false, ms.ToArray());
    }
}
