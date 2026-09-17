using CRMTeamBenavides.Api.Features.CategoriasProducto;

namespace CRMTeamBenavides.Api.Services;

public interface ICategoriaProductoService
{
    Task<List<CategoriaProductoResponse>> GetAllAsync();
    Task<ServiceResult<CategoriaProductoResponse>> GetByIdAsync(Guid id);
    Task<ServiceResult<CategoriaProductoResponse>> CreateAsync(CreateCategoriaProductoRequest request);
    Task<ServiceResult<CategoriaProductoResponse>> UpdateAsync(Guid id, UpdateCategoriaProductoRequest request);
    Task<ServiceResult<bool>> DeleteAsync(Guid id);
}
