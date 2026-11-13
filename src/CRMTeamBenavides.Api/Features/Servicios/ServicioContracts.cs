using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Servicios;

/// <summary>El precio sugerido va con IGV si <c>PrecioIncluyeIgv</c>, igual que se escribe en la orden.</summary>
public record ServicioResponse(
    Guid Id,
    string Nombre,
    decimal PrecioSugerido,
    bool PrecioIncluyeIgv,
    TipoAfectacionIgv TipoAfectacionIgv,
    bool Activo,
    DateTime FechaCreacion
);

/// <summary>Un servicio nuevo va con IGV incluido, como los repuestos.</summary>
public record CrearServicioRequest(
    string Nombre,
    decimal PrecioSugerido,
    TipoAfectacionIgv TipoAfectacionIgv = TipoAfectacionIgv.Gravado,
    bool PrecioIncluyeIgv = true
);

/// <summary>Sin <c>PrecioIncluyeIgv</c>, el servicio lo conserva.</summary>
public record ActualizarServicioRequest(
    string Nombre,
    decimal PrecioSugerido,
    TipoAfectacionIgv TipoAfectacionIgv = TipoAfectacionIgv.Gravado,
    bool? Activo = null,
    bool? PrecioIncluyeIgv = null
);
