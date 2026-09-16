namespace CRMTeamBenavides.Api.Features.Clientes;

public record ClienteResponse(
    Guid Id,
    string NombreCompleto,
    string? RazonSocial,
    string? DocumentoIdentidad,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Observaciones,
    bool Activo);

public record CreateClienteRequest(
    string NombreCompleto,
    string? RazonSocial,
    string? DocumentoIdentidad,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Observaciones);

public record UpdateClienteRequest(
    string NombreCompleto,
    string? RazonSocial,
    string? DocumentoIdentidad,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Observaciones);
