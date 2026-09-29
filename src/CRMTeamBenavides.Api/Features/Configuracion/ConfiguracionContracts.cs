namespace CRMTeamBenavides.Api.Features.Configuracion;

public record ConfiguracionEmpresaResponse(
    Guid Id,
    string NombreEmpresa,
    string? Ruc,
    decimal PorcentajeIgv
);

public record ActualizarConfiguracionEmpresaRequest(
    string NombreEmpresa,
    string? Ruc,
    decimal PorcentajeIgv
);
