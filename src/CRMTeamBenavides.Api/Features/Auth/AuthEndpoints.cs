using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CRMTeamBenavides.Api.Services;

namespace CRMTeamBenavides.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, IAuthService authService, HttpContext httpContext) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var result = await authService.LoginAsync(request, ipAddress);

            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Unauthorized();
        })
        .WithName("Login");

        group.MapPost("/refresh", async (RefreshRequest request, IAuthService authService, HttpContext httpContext) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var result = await authService.RefreshAsync(request, ipAddress);

            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Unauthorized();
        })
        .WithName("RefreshToken");
        group.MapGet("/me", async (ClaimsPrincipal user, IAuthService authService) =>
        {
            var subClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(subClaim, out var usuarioId))
            {
                return Results.Unauthorized();
            }

            var result = await authService.GetCurrentUserAsync(usuarioId);

            return result.IsSuccess ? Results.Ok(result.Data) : Results.Unauthorized();
        })
        .RequireAuthorization()
        .WithName("Me");

        group.MapPost("/logout", async (ClaimsPrincipal user, IAuthService authService) =>
        {
            var subClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(subClaim, out var usuarioId))
            {
                return Results.Unauthorized();
            }

            var result = await authService.LogoutAsync(usuarioId);

            return result.IsSuccess
                ? Results.Ok(new { message = "Sesión cerrada correctamente." })
                : Results.Unauthorized();
        })
        .RequireAuthorization()
        .WithName("Logout");
    }
}
