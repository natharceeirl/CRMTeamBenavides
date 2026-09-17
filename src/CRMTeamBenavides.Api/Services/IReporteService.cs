using CRMTeamBenavides.Api.Features.Reportes;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface IReporteService
{
    Task<List<OrdenServicioReporteResponse>> GetOrdenesServicioAsync(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        EstadoOrdenServicio? estado,
        Guid? tecnicoId);

    Task<List<VentaReporteResponse>> GetVentasAsync(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        EstadoVenta? estado,
        Guid? clienteId);

    Task<List<StockBajoResponse>> GetStockBajoAsync();
}
