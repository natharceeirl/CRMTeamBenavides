namespace CRMTeamBenavides.Api.Features.ConsultaDocumento;

/// <summary>
/// Los datos de un DNI (RENIEC) o un RUC (SUNAT) para completar un formulario. Con DNI
/// vienen nombres y apellidos; con RUC, razón social, dirección, estado y condición.
/// </summary>
public record ConsultaDocumentoResponse(
    /// <summary>«DNI» o «RUC».</summary>
    string TipoDocumento,
    /// <summary>Enum TipoDocumentoCliente: DNI = 0, RUC = 1.</summary>
    int TipoDocumentoId,
    string NumeroDocumento,
    /// <summary>Como se guarda en el cliente: «Roxana Karina Delgado Huamani» o la razón social.</summary>
    string NombreCompleto,
    string? Nombres = null,
    string? ApellidoPaterno = null,
    string? ApellidoMaterno = null,
    string? RazonSocial = null,
    string? Direccion = null,
    string? Estado = null,
    string? Condicion = null,
    /// <summary>Si ya hay un cliente activo con ese documento, para avisar antes de duplicarlo.</summary>
    Guid? ClienteExistenteId = null,
    string? ClienteExistenteNombre = null);
