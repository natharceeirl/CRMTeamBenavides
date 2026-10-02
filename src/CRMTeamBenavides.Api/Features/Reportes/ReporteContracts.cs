namespace CRMTeamBenavides.Api.Features.Reportes;

// --- Órdenes de Servicio ---

public record OrdenServicioReporteResponse(
    Guid Id,
    Guid VehiculoId,
    string? VehiculoPlaca,
    string VehiculoMarca,
    string VehiculoModelo,
    Guid ClienteId,
    string ClienteNombre,
    Guid? TecnicoId,
    string? TecnicoNombre,
    string Estado,
    int EstadoId,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    /// <summary>
    /// Tiempo de atención en horas completas. Null si la OS todavía no tiene FechaCierre.
    /// </summary>
    double? TiempoAtencionHoras,
    string? NumeroOrden = null,
    string? TipoMedidor = null);

// --- Ventas ---

public record VentaReporteResponse(
    Guid Id,
    Guid ClienteId,
    string ClienteNombre,
    Guid? OrdenServicioId,
    string Estado,
    int EstadoId,
    DateTime Fecha,
    decimal Total);

// --- Stock Bajo ---

public record StockBajoResponse(
    Guid ProductoId,
    string Codigo,
    string Nombre,
    Guid CategoriaId,
    string CategoriaNombre,
    int StockActual,
    int StockMinimo,
    int Diferencia,
    decimal PrecioVenta);

// --- Rentabilidad / Costos Históricos (D9) ---

public record RentabilidadResumenResponse(
    decimal IngresosTotalesSinIgv,
    decimal CostoTotalHistorico,
    decimal UtilidadBrutaTotal,
    decimal MargenPorcentualGlobal);

public record RentabilidadPorTipoItemResponse(
    string TipoItem,
    int CantidadItems,
    decimal IngresoNeto,
    decimal CostoHistoricoRegistrado,
    decimal UtilidadBruta,
    decimal MargenPorcentual);

public record RentabilidadOperacionDetalleResponse(
    string DocumentoTipo,
    string NumeroDocumento,
    Guid OperacionId,
    DateTime Fecha,
    string ClienteNombre,
    decimal IngresoNeto,
    decimal CostoHistoricoRegistrado,
    decimal UtilidadBruta,
    decimal MargenPorcentual);

public record RentabilidadRepuestoRankingResponse(
    Guid ProductoId,
    string Codigo,
    string Descripcion,
    int UnidadesVendidas,
    decimal IngresoNeto,
    decimal CostoHistorico,
    decimal UtilidadBruta,
    decimal MargenPorcentual);

public record RentabilidadReporteResponse(
    DateTime? FechaDesde,
    DateTime? FechaHasta,
    RentabilidadResumenResponse Resumen,
    List<RentabilidadPorTipoItemResponse> DesglosePorTipo,
    List<RentabilidadOperacionDetalleResponse> DetalleOperaciones,
    List<RentabilidadRepuestoRankingResponse> RankingRepuestos);
