namespace CRMTeamBenavides.Api.Features.CategoriasProducto;

public record CreateCategoriaProductoRequest(
    string Nombre,
    int? StockMinimoDefault = null);

public record UpdateCategoriaProductoRequest(
    string Nombre,
    int? StockMinimoDefault = null);

public record CategoriaProductoResponse(
    Guid Id,
    string Nombre,
    int CantidadProductos,
    bool Activo,
    DateTime FechaCreacion,
    int? StockMinimoDefault = null);
