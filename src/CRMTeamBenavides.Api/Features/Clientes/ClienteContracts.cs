using CRMTeamBenavides.Domain.Entities;

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
    bool Activo,
    string? TipoDocumento = null,
    int? TipoDocumentoId = null,
    string? NumeroDocumento = null);

public record CreateClienteRequest(
    string NombreCompleto,
    string? RazonSocial,
    string? DocumentoIdentidad,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Observaciones,
    TipoDocumentoCliente? TipoDocumento = null,
    string? NumeroDocumento = null);

public record UpdateClienteRequest(
    string NombreCompleto,
    string? RazonSocial,
    string? DocumentoIdentidad,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Observaciones,
    TipoDocumentoCliente? TipoDocumento = null,
    string? NumeroDocumento = null);
