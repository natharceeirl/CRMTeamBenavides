namespace CRMTeamBenavides.Api.Features.CategoriasProducto;

public record CreateCategoriaProductoRequest(
    string Nombre);

public record UpdateCategoriaProductoRequest(
    string Nombre);

public record CategoriaProductoResponse(
    Guid Id,
    string Nombre,
    int CantidadProductos,
    bool Activo,
    DateTime FechaCreacion);
