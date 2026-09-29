using CRMTeamBenavides.Api.Features.Roles;
using CRMTeamBenavides.Api.Features.Usuarios;

namespace CRMTeamBenavides.Api.Services;

public interface IUsuarioService
{
    Task<List<UsuarioResponse>> GetAllAsync();
    Task<ServiceResult<UsuarioResponse>> GetByIdAsync(Guid id);
    Task<ServiceResult<UsuarioResponse>> CreateAsync(CreateUsuarioRequest request);
    Task<ServiceResult<UsuarioResponse>> UpdateAsync(Guid id, UpdateUsuarioRequest request);
    /// <param name="usuarioActualId">Quien pide la baja: nadie se da de baja a sí mismo.</param>
    Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid? usuarioActualId);
    Task<ServiceResult<List<RolResponse>>> GetRolesAsync(Guid usuarioId);
    Task<ServiceResult<bool>> AsignarRolAsync(Guid usuarioId, Guid rolId);
    Task<ServiceResult<bool>> QuitarRolAsync(Guid usuarioId, Guid rolId);
}
