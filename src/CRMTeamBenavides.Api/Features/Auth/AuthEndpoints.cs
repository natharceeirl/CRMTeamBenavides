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
    }
}
