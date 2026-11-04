using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Inventario;

/// <summary>
/// El precio de venta queda en su moneda y con IGV si así se registra (por defecto). El
/// costo se convierte al registrarlo: se guarda en soles y sin IGV.
/// </summary>
public record CreateProductoRequest(
    Guid CategoriaId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string? Unidad,
    decimal PrecioVenta,
    int StockInicial,
    int? StockMinimo = null,
    decimal Costo = 0m,
    string? Marca = null,
    string? FotoUrl = null,
    MonedaPrecio MonedaPrecio = MonedaPrecio.PEN,
    bool PrecioIncluyeIgv = true,
    MonedaPrecio MonedaCosto = MonedaPrecio.PEN,
    bool CostoIncluyeIgv = false);

public record AltaRapidaProductoRequest(
    string Nombre,
    decimal PrecioVenta,
    string? Codigo = null,
    string? Marca = null,
    Guid? CategoriaId = null,
    int StockInicial = 0,
    MonedaPrecio MonedaPrecio = MonedaPrecio.PEN,
    bool PrecioIncluyeIgv = true);


public record UpdateProductoRequest(
    Guid CategoriaId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string? Unidad,
    decimal PrecioVenta,
    int? StockMinimo = null,
    decimal? Costo = null,
    string? Marca = null,
    string? FotoUrl = null,
    MonedaPrecio? MonedaPrecio = null,
    bool? PrecioIncluyeIgv = null,
    MonedaPrecio MonedaCosto = Domain.Entities.MonedaPrecio.PEN,
    bool CostoIncluyeIgv = false);

public record ProductoResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string Unidad,
    decimal PrecioVenta,
    int StockActual,
    int? StockMinimo,
    bool EsBajoStock,
    Guid CategoriaId,
    string CategoriaNombre,
    bool Activo,
    DateTime FechaCreacion,
    decimal Costo = 0m,
    int StockMinimoEfectivo = 4,
    string? Marca = null,
    string? FotoUrl = null,
    MonedaPrecio MonedaPrecio = MonedaPrecio.PEN,
    bool PrecioIncluyeIgv = false,
    // El precio de lista en soles con el tipo de cambio de hoy, en los términos del repuesto
    // (con IGV si lo incluye). Nulo si está en dólares y no hay tipo de cambio.
    decimal? PrecioListaSoles = null);

public record RegistrarEntradaRequest(
    int Cantidad,
    string Motivo,
    decimal? CostoUnitario = null);

public record RegistrarSalidaRequest(
    int Cantidad,
    string Motivo);

public record RegistrarAjusteRequest(
    int NuevoStock,
    string Motivo,
    decimal? CostoUnitario = null);

public record MovimientoInventarioResponse(
    Guid Id,
    Guid ProductoId,
    string ProductoCodigo,
    string ProductoNombre,
    string Tipo,
    int TipoId,
    int Cantidad,
    string? Motivo,
    Guid? OrdenServicioId,
    Guid? VentaId,
    DateTime FechaCreacion,
    decimal? CostoUnitario = null);

public record ActualizarFotoProductoRequest(string FotoUrl);
