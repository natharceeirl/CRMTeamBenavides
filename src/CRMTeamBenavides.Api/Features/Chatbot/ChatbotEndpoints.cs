using System.Security.Claims;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Chatbot;

public static class ChatbotEndpoints
{
    public static void MapChatbotEndpoints(this IEndpointRouteBuilder app)
    {
        // =====================================================================
        // ENDPOINTS PÚBLICOS (Widgets web, clientes o personal sin token obligatorio)
        // =====================================================================
        var groupPublic = app.MapGroup("/api/chatbot");

        groupPublic.MapGet("/faqs", async (
            string? categoria,
            string? busqueda,
            IChatbotService service) =>
        {
            var faqs = await service.GetFaqsPublicasAsync(categoria, busqueda);
            return Results.Ok(faqs);
        })
        .WithName("GetFaqsPublicas");

        groupPublic.MapPost("/consultar", async (
            ConsultaChatbotRequest request,
            ClaimsPrincipal user,
            IChatbotService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.ConsultarAsync(request, usuarioId);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("ConsultarChatbot");

        groupPublic.MapPost("/solicitar-agente", async (
            SolicitarAgenteRequest request,
            ClaimsPrincipal user,
            IChatbotService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            var result = await service.SolicitarAgenteAsync(request, usuarioId);

            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("SolicitarAgente");

        // =====================================================================
        // ENDPOINTS ADMINISTRATIVOS (Requiere JWT)
        // =====================================================================
        var groupAdmin = app.MapGroup("/api/chatbot/admin").RequireAuthorization();

        // --- FAQs (Solo Admin) ---

        groupAdmin.MapGet("/faqs", async (
            string? categoria,
            bool? soloActivos,
            ClaimsPrincipal user,
            IChatbotService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            if (!usuarioId.HasValue || !await service.TieneRolAsync(usuarioId.Value, "Admin", "Administrador"))
            {
                return Results.Forbid();
            }

            var faqs = await service.GetFaqsAdminAsync(categoria, soloActivos);
            return Results.Ok(faqs);
        })
        .WithName("GetFaqsAdmin");

        groupAdmin.MapPost("/faqs", async (
            CreateFaqRequest request,
            ClaimsPrincipal user,
            IChatbotService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            if (!usuarioId.HasValue || !await service.TieneRolAsync(usuarioId.Value, "Admin", "Administrador"))
            {
                return Results.Forbid();
            }

            var result = await service.CreateFaqAsync(request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Created($"/api/chatbot/admin/faqs/{result.Data!.Id}", result.Data),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("CreateFaq");

        groupAdmin.MapPut("/faqs/{id:guid}", async (
            Guid id,
            UpdateFaqRequest request,
            ClaimsPrincipal user,
            IChatbotService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            if (!usuarioId.HasValue || !await service.TieneRolAsync(usuarioId.Value, "Admin", "Administrador"))
            {
                return Results.Forbid();
            }

            var result = await service.UpdateFaqAsync(id, request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("UpdateFaq");

        groupAdmin.MapDelete("/faqs/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IChatbotService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            if (!usuarioId.HasValue || !await service.TieneRolAsync(usuarioId.Value, "Admin", "Administrador"))
            {
                return Results.Forbid();
            }

            var result = await service.DeleteFaqAsync(id, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.NoContent(),
                ServiceResultStatus.NotFound => Results.NotFound(),
                _ => Results.Problem()
            };
        })
        .WithName("DeleteFaq");

        // --- Bandeja de Consultas (Admin y Recepcion) ---

        groupAdmin.MapGet("/consultas", async (
            bool? requiereAtencion,
            EstadoAtencionConsulta? estado,
            string? canal,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            ClaimsPrincipal user,
            IChatbotService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            if (!usuarioId.HasValue || !await service.TieneRolAsync(usuarioId.Value, "Admin", "Administrador", "Recepcion", "Recepción"))
            {
                return Results.Forbid();
            }

            var consultas = await service.GetConsultasAdminAsync(requiereAtencion, estado, canal, fechaDesde, fechaHasta);
            return Results.Ok(consultas);
        })
        .WithName("GetConsultasAdmin");

        groupAdmin.MapPut("/consultas/{id:guid}/asignar", async (
            Guid id,
            AsignarAgenteRequest request,
            ClaimsPrincipal user,
            IChatbotService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            if (!usuarioId.HasValue || !await service.TieneRolAsync(usuarioId.Value, "Admin", "Administrador", "Recepcion", "Recepción"))
            {
                return Results.Forbid();
            }

            var result = await service.AsignarAgenteAsync(id, request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("AsignarAgenteConsulta");

        groupAdmin.MapPut("/consultas/{id:guid}/resolver", async (
            Guid id,
            ResolverConsultaRequest request,
            ClaimsPrincipal user,
            IChatbotService service) =>
        {
            var usuarioId = ObtenerUsuarioId(user);
            if (!usuarioId.HasValue || !await service.TieneRolAsync(usuarioId.Value, "Admin", "Administrador", "Recepcion", "Recepción"))
            {
                return Results.Forbid();
            }

            var result = await service.ResolverConsultaAsync(id, request, usuarioId);
            return result.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(result.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = result.Error }),
                _ => Results.Problem()
            };
        })
        .WithName("ResolverConsulta");
    }

    private static Guid? ObtenerUsuarioId(ClaimsPrincipal? user)
    {
        if (user is null || !user.Identity?.IsAuthenticated == true) return null;

        var sub = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
