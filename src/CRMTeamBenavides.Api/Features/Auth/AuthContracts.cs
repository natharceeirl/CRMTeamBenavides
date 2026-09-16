namespace CRMTeamBenavides.Api.Features.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    DateTime AccessTokenExpiration,
    string RefreshToken,
    DateTime RefreshTokenExpiration);

public record RefreshRequest(string RefreshToken);

public record MeResponse(
    Guid Id,
    string Email,
    string NombreCompleto,
    bool Activo,
    List<string> Roles);