using CRMTeamBenavides.Api.Features.Clientes;

namespace CRMTeamBenavides.Api.Services;

public interface IClienteService
{
    Task<List<ClienteResponse>> GetAllAsync(Guid? soloClienteId = null);
    Task<ServiceResult<ClienteResponse>> GetByIdAsync(Guid id, Guid? soloClienteId = null);
    Task<ServiceResult<ClienteResponse>> CreateAsync(CreateClienteRequest request);
    Task<ServiceResult<ClienteResponse>> UpdateAsync(Guid id, UpdateClienteRequest request);
    Task<ServiceResult<bool>> DeleteAsync(Guid id);
}
