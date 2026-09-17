using CRMTeamBenavides.Api.Features.Inventario;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface IInventarioService
{
    Task<List<ProductoResponse>> GetAllProductosAsync(Guid? categoriaId, string? busqueda, bool? bajoStock);
    Task<ServiceResult<ProductoResponse>> GetProductoByIdAsync(Guid id);
    Task<ServiceResult<ProductoResponse>> CreateProductoAsync(CreateProductoRequest request);
    Task<ServiceResult<ProductoResponse>> UpdateProductoAsync(Guid id, UpdateProductoRequest request);
    Task<ServiceResult<bool>> DeleteProductoAsync(Guid id);

    Task<ServiceResult<ProductoResponse>> RegistrarEntradaAsync(Guid productoId, RegistrarEntradaRequest request);
    Task<ServiceResult<ProductoResponse>> RegistrarSalidaAsync(Guid productoId, RegistrarSalidaRequest request);
    Task<ServiceResult<ProductoResponse>> RegistrarAjusteAsync(Guid productoId, RegistrarAjusteRequest request);

    Task<ServiceResult<List<MovimientoInventarioResponse>>> GetMovimientosByProductoAsync(Guid productoId);
    Task<List<MovimientoInventarioResponse>> GetAllMovimientosAsync(Guid? productoId, TipoMovimientoInventario? tipo, DateTime? fechaDesde, DateTime? fechaHasta);
}
