namespace CRMTeamBenavides.Api.Features.Inventario;

public record CreateProductoRequest(
    Guid CategoriaId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string? Unidad,
    decimal PrecioVenta,
    int StockInicial,
    int? StockMinimo = null,
    decimal Costo = 0m);

public record UpdateProductoRequest(
    Guid CategoriaId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string? Unidad,
    decimal PrecioVenta,
    int? StockMinimo = null,
    decimal? Costo = null);

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
    int StockMinimoEfectivo = 4);

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
