using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.CajaChica;

public record AperturaCajaRequest(
    decimal MontoApertura,
    string? Observaciones
);

public record CierreCajaRequest(
    string? Observaciones
);

public record RegistrarMovimientoCajaRequest(
    TipoMovimientoCaja Tipo,
    decimal Monto,
    string Concepto,
    string? Referencia,
    Guid? CajaChicaId = null
);

public record MovimientoCajaResponse(
    Guid Id,
    Guid CajaChicaId,
    TipoMovimientoCaja Tipo,
    string TipoDescripcion,
    decimal Monto,
    string Concepto,
    string? Referencia,
    DateTime Fecha,
    Guid? UsuarioId,
    string? UsuarioNombre,
    Guid? PagoId = null,
    Guid? MetodoPagoId = null,
    string? MetodoPagoNombre = null
);

public record ResumenMetodosPagoCajaResponse(
    Guid? CajaChicaId,
    decimal TotalEfectivo,
    decimal TotalYapePlin,
    decimal TotalTarjeta,
    decimal TotalTransferencia,
    Dictionary<string, decimal> PorMetodo,
    decimal TotalGeneral
);

public record CajaChicaResponse(
    Guid Id,
    decimal MontoApertura,
    decimal? MontoCierre,
    decimal SaldoCalculado,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    EstadoCajaChica Estado,
    string EstadoDescripcion,
    string? ObservacionesApertura,
    string? ObservacionesCierre,
    Guid? UsuarioAperturaId,
    string? UsuarioAperturaNombre,
    Guid? UsuarioCierreId,
    string? UsuarioCierreNombre,
    decimal TotalIngresos,
    decimal TotalEgresos,
    int CantidadMovimientos,
    DateTime FechaCreacion,
    // TotalIngresos suma todos los métodos; el saldo solo cuenta efectivo:
    // SaldoCalculado = MontoApertura + TotalIngresosEfectivo - TotalEgresos.
    decimal TotalIngresosEfectivo = 0m,
    decimal TotalIngresosOtrosMetodos = 0m
);

public record CajaChicaDetalleResponse(
    Guid Id,
    decimal MontoApertura,
    decimal? MontoCierre,
    decimal SaldoCalculado,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    EstadoCajaChica Estado,
    string EstadoDescripcion,
    string? ObservacionesApertura,
    string? ObservacionesCierre,
    Guid? UsuarioAperturaId,
    string? UsuarioAperturaNombre,
    Guid? UsuarioCierreId,
    string? UsuarioCierreNombre,
    decimal TotalIngresos,
    decimal TotalEgresos,
    List<MovimientoCajaResponse> Movimientos,
    // TotalIngresos suma todos los métodos; el saldo solo cuenta efectivo:
    // SaldoCalculado = MontoApertura + TotalIngresosEfectivo - TotalEgresos.
    decimal TotalIngresosEfectivo = 0m,
    decimal TotalIngresosOtrosMetodos = 0m
);

public record EstadoCajaActualResponse(
    bool TieneCajaAbierta,
    CajaChicaDetalleResponse? Caja
);
