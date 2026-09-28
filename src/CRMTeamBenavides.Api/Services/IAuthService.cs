using CRMTeamBenavides.Api.Features.Auth;

namespace CRMTeamBenavides.Api.Services;

public interface IAuthService
{
    Task<AuthResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress);
    Task<AuthResult<LoginResponse>> RefreshAsync(RefreshRequest request, string? ipAddress);
    Task<ServiceResult<MeResponse>> GetCurrentUserAsync(Guid usuarioId);
    Task<ServiceResult<bool>> LogoutAsync(Guid usuarioId);
    Task<ServiceResult<bool>> CambiarPasswordAsync(Guid usuarioId, CambiarPasswordRequest request);
    Task<ServiceResult<bool>> ResetPasswordAsync(Guid usuarioId, ResetPasswordRequest request);
}
