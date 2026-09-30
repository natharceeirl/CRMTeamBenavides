using CRMTeamBenavides.Api.Features.Portal;

namespace CRMTeamBenavides.Api.Services;

public interface IPortalService
{
    Task<ServiceResult<PortalResumenResponse>> GetResumenAsync(Guid clienteId);
    Task<ServiceResult<List<PortalComprobanteResponse>>> GetComprobantesAsync(Guid clienteId);
    Task<ServiceResult<List<AtencionServicioUnidadResponse>>> GetHistorialServicioUnidadAsync(Guid vehiculoId, Guid? soloClienteId = null);
}
