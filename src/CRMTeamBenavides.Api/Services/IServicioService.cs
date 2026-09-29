using CRMTeamBenavides.Api.Features.Servicios;

namespace CRMTeamBenavides.Api.Services;

public interface IServicioService
{
    Task<IReadOnlyList<ServicioResponse>> GetAllAsync(bool soloActivos = true, CancellationToken ct = default);
    Task<ServicioResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<ServicioResponse>> CreateAsync(CrearServicioRequest request, CancellationToken ct = default);
    Task<ServiceResult<ServicioResponse>> UpdateAsync(Guid id, ActualizarServicioRequest request, CancellationToken ct = default);
    Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}
