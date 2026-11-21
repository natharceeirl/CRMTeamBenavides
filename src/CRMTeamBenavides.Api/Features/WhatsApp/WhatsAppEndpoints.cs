using System.Security.Claims;
using System.Text;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.WhatsApp;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
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
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct);
            if (denegado is not null) return denegado;

            return Results.Ok(service.ObtenerEstadoIntegracion());
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppVer)
        .WithName("ObtenerEstadoIntegracionWhatsApp");

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
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct);
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
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct);
            if (denegado is not null) return denegado;

            var result = await service.ObtenerConversacionAsync(id, ct);
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
            var (denegado, _) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct);
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
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct);
            if (denegado is not null) return denegado;

            var result = await service.AsignarAgenteAsync(id, request, usuarioId!.Value, ct);
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
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct);
            if (denegado is not null) return denegado;

            var result = await service.CambiarModoAtencionAsync(id, request, usuarioId!.Value, ct);
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
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct);
            if (denegado is not null) return denegado;

            var result = await service.CambiarEstadoAsync(id, request, usuarioId!.Value, ct);
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
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct);
            if (denegado is not null) return denegado;

            var result = await service.VincularClienteAsync(id, request, usuarioId!.Value, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppGestionar)
        .WithName("VincularClienteConversacionWhatsApp");

        adminGroup.MapPost("/conversaciones/{id:guid}/mensajes", async (
            Guid id,
            WhatsAppEnviarMensajeRequest request,
            ClaimsPrincipal user,
            IWhatsAppService service,
            ApplicationDbContext dbContext,
            CancellationToken ct) =>
        {
            var (denegado, usuarioId) = await ValidarPersonalAutorizadoAsync(user, dbContext, ct);
            if (denegado is not null) return denegado;

            var result = await service.EnviarMensajeManualAsync(id, request, usuarioId!.Value, ct);
            return MapearResultado(result);
        })
        .RequireAuthorization(PermisosDefinidos.WhatsAppGestionar)
        .WithName("EnviarMensajeManualWhatsApp");
    }

    private static async Task<(IResult? Denegado, Guid? UsuarioId)> ValidarPersonalAutorizadoAsync(
        ClaimsPrincipal user,
        ApplicationDbContext dbContext,
        CancellationToken ct)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return (Results.Unauthorized(), null);
        }

        var isolation = await UserIsolationHelper.ResolverContextoAsync(user, dbContext, ct);
        if (!isolation.UsuarioId.HasValue || !isolation.EsStaff || isolation.EsCliente || isolation.DebeDenegarAcceso)
        {
            return (Results.Forbid(), null);
        }

        return (null, isolation.UsuarioId);
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
