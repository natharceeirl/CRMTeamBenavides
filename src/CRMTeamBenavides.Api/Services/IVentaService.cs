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
        DateTime? fechaHasta,
        Guid? soloClienteId = null);

    Task<ServiceResult<VentaDetalleResponse>> GetByIdAsync(Guid id, Guid? soloClienteId = null);
    Task<ServiceResult<VentaDetalleResponse>> CreateAsync(
        CreateVentaRequest request,
        bool puedeModificarPrecios = false,
        bool puedeAplicarDescuentos = false,
        Guid? soloClienteId = null,
        Guid? usuarioId = null);

    Task<ServiceResult<VentaDetalleResponse>> ConfirmarCotizacionAsync(Guid id, Guid? soloClienteId = null);
    Task<ServiceResult<VentaDetalleResponse>> AnularAsync(Guid id, Guid? usuarioId = null);
    Task<ServiceResult<ComprobanteResponse>> GetComprobanteAsync(Guid ventaId, Guid? soloClienteId = null);
    Task<ServiceResult<ComprobanteResponse>> RegistrarComprobanteAsync(Guid ventaId, RegistrarComprobanteRequest request, Guid? usuarioId = null);
    Task<ServiceResult<ComprobanteResponse>> AnularComprobanteAsync(Guid ventaId, Guid? usuarioId = null);
}
