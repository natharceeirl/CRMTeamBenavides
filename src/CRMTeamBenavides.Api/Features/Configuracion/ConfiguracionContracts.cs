using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Configuracion;

public record ConfiguracionEmpresaResponse(
    Guid Id,
    string NombreEmpresa,
    string? RazonSocial,
    string? Ruc,
    string? Direccion,
    string? Telefono,
    string? Email,
    decimal PorcentajeIgv,
    string MonedaBase,
    decimal? TipoCambioVigente,
    DateTime? FechaActualizacionTipoCambio,
    MetodoCosteo MetodoCosteo
);

public record ActualizarConfiguracionEmpresaRequest(
    string? NombreEmpresa,
    string? RazonSocial,
    string? Ruc,
    string? Direccion,
    string? Telefono,
    string? Email,
    decimal? PorcentajeIgv,
    string? MonedaBase,
    decimal? TipoCambioVigente,
    MetodoCosteo? MetodoCosteo = null
);

public record TipoCambioResponse(
    bool Configurado,
    decimal? TipoCambio,
    decimal? ValorVenta,
    decimal? ValorCompra,
    string MonedaBase,
    string MonedaExtranjera,
    DateTime? FechaActualizacion,
    string? UltimoUsuarioNombre
);

public record ActualizarTipoCambioRequest(
    decimal? TipoCambio,
    decimal? Valor,
    decimal? ValorVenta,
    decimal? ValorCompra,
    string? MonedaOrigen,
    string? MonedaDestino,
    DateTime? FechaVigencia,
    string? Observaciones
);

public record HistorialTipoCambioResponse(
    Guid Id,
    string MonedaOrigen,
    string MonedaDestino,
    decimal ValorCompra,
    decimal ValorVenta,
    DateTime FechaVigencia,
    string? Observaciones,
    Guid? UsuarioId,
    string? UsuarioNombre,
    DateTime FechaCreacion
);

public record ConversionMonedaResponse(
    decimal MontoUsd,
    decimal TipoCambio,
    decimal MontoPen,
    string Formula
);

public record ConversionMonedaRequest(
    decimal MontoUsd,
    decimal? TipoCambio = null
);
