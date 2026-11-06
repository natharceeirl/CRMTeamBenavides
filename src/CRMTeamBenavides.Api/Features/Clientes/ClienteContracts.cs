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
    string? NumeroDocumento = null,
    string? Nombres = null,
    string? ApellidoPaterno = null,
    string? ApellidoMaterno = null);

public record CreateClienteRequest(
    string NombreCompleto,
    string? RazonSocial,
    string? DocumentoIdentidad,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Observaciones,
    TipoDocumentoCliente? TipoDocumento = null,
    string? NumeroDocumento = null,
    // Una persona: nombres, primer y segundo apellido, como en su DNI. Con nombres, el
    // nombre completo se arma con ellos; sin nombres (empresa), se usa NombreCompleto.
    string? Nombres = null,
    string? ApellidoPaterno = null,
    string? ApellidoMaterno = null);

public record UpdateClienteRequest(
    string NombreCompleto,
    string? RazonSocial,
    string? DocumentoIdentidad,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Observaciones,
    TipoDocumentoCliente? TipoDocumento = null,
    string? NumeroDocumento = null,
    // Una persona: nombres, primer y segundo apellido, como en su DNI. Con nombres, el
    // nombre completo se arma con ellos; sin nombres (empresa), se usa NombreCompleto.
    string? Nombres = null,
    string? ApellidoPaterno = null,
    string? ApellidoMaterno = null);

public record AltaRapidaClienteRequest(
    string NumeroDocumento,
    string NombreCompleto,
    string? Telefono = null,
    string? Email = null,
    string? Direccion = null,
    TipoDocumentoCliente? TipoDocumento = TipoDocumentoCliente.DNI,
    // Una persona: nombres, primer y segundo apellido, como en su DNI. Con nombres, el
    // nombre completo se arma con ellos; sin nombres (empresa), se usa NombreCompleto.
    string? Nombres = null,
    string? ApellidoPaterno = null,
    string? ApellidoMaterno = null);
