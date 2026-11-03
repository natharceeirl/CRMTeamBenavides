using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Auth;
using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Clientes;

public static class ActivacionCuentaEndpoints
{
    public static void MapActivacionCuentaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/clientes/{id:guid}/cuenta-app", async (Guid id, ActivacionCuentaService servicio) =>
        {
            var resultado = await servicio.ObtenerEstadoAsync(id);
            return resultado.IsSuccess ? Results.Ok(resultado.Data) : Results.NotFound();
        })
        .RequireAuthorization(PermisosDefinidos.ClientesVer)
        .WithName("ObtenerCuentaAppCliente");

        app.MapPost("/api/clientes/{id:guid}/codigo-activacion", async (
            Guid id, ClaimsPrincipal user, ActivacionCuentaService servicio) =>
        {
            var resultado = await servicio.GenerarCodigoAsync(id, ObtenerUsuarioId(user));
            return resultado.Status switch
            {
                ServiceResultStatus.Success => Results.Ok(resultado.Data),
                ServiceResultStatus.NotFound => Results.NotFound(),
                ServiceResultStatus.Conflict => Results.Conflict(new { error = resultado.Error }),
                ServiceResultStatus.ValidationError => Results.BadRequest(new { error = resultado.Error }),
                _ => Results.Problem()
            };
        })
        .RequireAuthorization(PermisosDefinidos.ClientesEditar)
        .WithName("GenerarCodigoActivacion");

        // Sin sesión: el cliente todavía no tiene cuenta. Al activarla entra de una vez.
        app.MapPost("/api/auth/activar-cuenta", async (
            ActivarCuentaRequest request, ActivacionCuentaService servicio, IAuthService auth, HttpContext httpContext) =>
        {
            var resultado = await servicio.ActivarAsync(request);
            if (!resultado.IsSuccess)
            {
                return Results.BadRequest(new { error = resultado.Error });
            }

            var sesion = await auth.LoginAsync(
                new LoginRequest(request.NumeroDocumento.Trim(), request.Password),
                httpContext.Connection.RemoteIpAddress?.ToString());
            return sesion.IsSuccess ? Results.Ok(sesion.Data) : Results.Problem();
        })
        // El código se adivina probando: el mismo límite por IP que el login.
        .RequireRateLimiting(SeguridadLogin.PoliticaPorIp)
        .WithName("ActivarCuenta");
    }

    private static Guid? ObtenerUsuarioId(ClaimsPrincipal user)
    {
        var id = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var usuarioId) ? usuarioId : null;
    }
}
