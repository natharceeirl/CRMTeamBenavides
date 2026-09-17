namespace CRMTeamBenavides.Api.Features.Reportes;

// --- Órdenes de Servicio ---

public record OrdenServicioReporteResponse(
    Guid Id,
    Guid VehiculoId,
    string VehiculoPlaca,
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
    double? TiempoAtencionHoras);

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
