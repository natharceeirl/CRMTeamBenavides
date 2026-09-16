using CRMTeamBenavides.Api.Features.Roles;
using CRMTeamBenavides.Api.Features.Usuarios;

namespace CRMTeamBenavides.Api.Services;

public interface IUsuarioService
{
    Task<List<UsuarioResponse>> GetAllAsync();
    Task<ServiceResult<UsuarioResponse>> GetByIdAsync(Guid id);
    Task<ServiceResult<UsuarioResponse>> CreateAsync(CreateUsuarioRequest request);
    Task<ServiceResult<UsuarioResponse>> UpdateAsync(Guid id, UpdateUsuarioRequest request);
    Task<ServiceResult<bool>> DeleteAsync(Guid id);
    Task<ServiceResult<List<RolResponse>>> GetRolesAsync(Guid usuarioId);
    Task<ServiceResult<bool>> AsignarRolAsync(Guid usuarioId, Guid rolId);
    Task<ServiceResult<bool>> QuitarRolAsync(Guid usuarioId, Guid rolId);
}
