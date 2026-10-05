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

    Task<RentabilidadReporteResponse> GetRentabilidadAsync(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        Guid? productoId);

    Task<byte[]> ExportarVentasExcelAsync(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        EstadoVenta? estado,
        Guid? clienteId,
        bool incluirFinanciero);

    Task<byte[]> ExportarOrdenesServicioExcelAsync(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        EstadoOrdenServicio? estado,
        Guid? tecnicoId,
        bool incluirFinanciero);
}
