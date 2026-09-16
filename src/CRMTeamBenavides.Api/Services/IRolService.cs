using CRMTeamBenavides.Api.Features.Permisos;
using CRMTeamBenavides.Api.Features.Roles;

namespace CRMTeamBenavides.Api.Services;

public interface IRolService
{
    Task<List<RolResponse>> GetAllAsync();
    Task<ServiceResult<RolResponse>> GetByIdAsync(Guid id);
    Task<ServiceResult<RolResponse>> CreateAsync(CreateRolRequest request);
    Task<ServiceResult<RolResponse>> UpdateAsync(Guid id, UpdateRolRequest request);
    Task<ServiceResult<bool>> DeleteAsync(Guid id);
    Task<ServiceResult<List<PermisoResponse>>> GetPermisosAsync(Guid rolId);
    Task<ServiceResult<bool>> AsignarPermisoAsync(Guid rolId, Guid permisoId);
    Task<ServiceResult<bool>> QuitarPermisoAsync(Guid rolId, Guid permisoId);
}
