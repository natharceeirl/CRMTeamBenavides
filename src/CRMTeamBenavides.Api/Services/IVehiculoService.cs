using CRMTeamBenavides.Api.Features.Vehiculos;

namespace CRMTeamBenavides.Api.Services;

public interface IVehiculoService
{
    Task<List<VehiculoResponse>> GetAllAsync(Guid? clienteId, Guid? soloClienteId = null);
    Task<ServiceResult<VehiculoResponse>> GetByIdAsync(Guid id, Guid? soloClienteId = null);
    Task<ServiceResult<VehiculoResponse>> CreateAsync(CreateVehiculoRequest request);
    Task<ServiceResult<VehiculoResponse>> UpdateAsync(Guid id, UpdateVehiculoRequest request);
    Task<ServiceResult<bool>> DeleteAsync(Guid id);
}
