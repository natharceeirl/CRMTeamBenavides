using CRMTeamBenavides.Api.Features.Permisos;

namespace CRMTeamBenavides.Api.Services;

public interface IPermisoService
{
    Task<List<PermisoResponse>> GetAllAsync();
    Task<ServiceResult<PermisoResponse>> GetByIdAsync(Guid id);
}
