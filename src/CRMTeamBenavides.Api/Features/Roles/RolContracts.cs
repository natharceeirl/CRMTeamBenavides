namespace CRMTeamBenavides.Api.Features.Roles;

public record RolResponse(Guid Id, string Nombre, string? Descripcion, bool Activo);

public record CreateRolRequest(string Nombre, string? Descripcion);

public record UpdateRolRequest(string Nombre, string? Descripcion);

public record AsignarPermisoRequest(Guid PermisoId);
