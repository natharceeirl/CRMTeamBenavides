using CRMTeamBenavides.Api.Features.Ventas;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface IVentaService
{
    Task<List<VentaResponse>> GetAllAsync(
        Guid? clienteId,
        EstadoVenta? estado,
        Guid? ordenServicioId,
        DateTime? fechaDesde,
        DateTime? fechaHasta);

    Task<ServiceResult<VentaDetalleResponse>> GetByIdAsync(Guid id);
    Task<ServiceResult<VentaDetalleResponse>> CreateAsync(CreateVentaRequest request);
    Task<ServiceResult<VentaDetalleResponse>> ConfirmarCotizacionAsync(Guid id);
    Task<ServiceResult<VentaDetalleResponse>> AnularAsync(Guid id);
}
