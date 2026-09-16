using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface IOrdenServicioService
{
    Task<List<OrdenServicioResponse>> GetAllAsync(Guid? vehiculoId, EstadoOrdenServicio? estado);
    Task<ServiceResult<OrdenServicioDetalleResponse>> GetByIdAsync(Guid id);
    Task<ServiceResult<OrdenServicioResponse>> CreateAperturaAsync(AperturaOrdenServicioRequest request);
    Task<ServiceResult<OrdenServicioResponse>> RegistrarDiagnosticoAsync(Guid id, RegistrarDiagnosticoRequest request);
    Task<ServiceResult<DetalleServicioResponse>> AgregarDetalleAsync(Guid ordenServicioId, AgregarDetalleServicioRequest request);
    Task<ServiceResult<bool>> EliminarDetalleAsync(Guid ordenServicioId, Guid detalleId);
    Task<ServiceResult<OrdenServicioResponse>> CambiarEstadoAsync(Guid ordenServicioId, CambiarEstadoOrdenServicioRequest request);
}
