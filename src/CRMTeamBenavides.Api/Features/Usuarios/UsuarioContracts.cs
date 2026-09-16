namespace CRMTeamBenavides.Api.Features.Usuarios;

public record UsuarioResponse(
    Guid Id,
    string Email,
    string NombreCompleto,
    string? PhoneNumber,
    bool Activo,
    List<string> Roles);

public record CreateUsuarioRequest(
    string Email,
    string Password,
    string NombreCompleto,
    string? PhoneNumber);

public record UpdateUsuarioRequest(
    string NombreCompleto,
    string? PhoneNumber);

public record AsignarRolRequest(Guid RolId);
