namespace CRMTeamBenavides.Api.Features.Permisos;

public record PermisoResponse(Guid Id, string Codigo, string? Descripcion, bool Activo);
