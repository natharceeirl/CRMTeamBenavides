using CRMTeamBenavides.Api.Features.Ventas;

namespace CRMTeamBenavides.Api.Services;

public interface IPagoService
{
    Task<List<MetodoPagoResponse>> GetMetodosPagoAsync();
    Task<ServiceResult<PagoResponse>> RegistrarPagoVentaAsync(Guid ventaId, RegistrarPagoRequest request, Guid? usuarioId);
    Task<ServiceResult<PagoResponse>> RegistrarPagoOrdenServicioAsync(Guid ordenServicioId, RegistrarPagoRequest request, Guid? usuarioId);
    Task<List<PagoResponse>> GetPagosByVentaIdAsync(Guid ventaId);
    Task<List<PagoResponse>> GetPagosByOrdenServicioIdAsync(Guid ordenServicioId);
}
