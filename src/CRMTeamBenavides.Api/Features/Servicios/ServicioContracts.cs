using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Servicios;

public record ServicioResponse(
    Guid Id,
    string Nombre,
    decimal PrecioSugerido,
    TipoAfectacionIgv TipoAfectacionIgv,
    bool Activo,
    DateTime FechaCreacion
);

public record CrearServicioRequest(
    string Nombre,
    decimal PrecioSugerido,
    TipoAfectacionIgv TipoAfectacionIgv = TipoAfectacionIgv.Gravado
);

public record ActualizarServicioRequest(
    string Nombre,
    decimal PrecioSugerido,
    TipoAfectacionIgv TipoAfectacionIgv = TipoAfectacionIgv.Gravado,
    bool? Activo = null
);
