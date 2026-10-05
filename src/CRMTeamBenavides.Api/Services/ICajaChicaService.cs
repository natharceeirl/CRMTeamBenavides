using CRMTeamBenavides.Api.Features.CajaChica;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface ICajaChicaService
{
    Task<EstadoCajaActualResponse> ObtenerCajaActualAsync(CancellationToken ct = default);
    Task<CajaChicaDetalleResponse?> ObtenerCajaPorIdAsync(Guid id, CancellationToken ct = default);
    Task<List<CajaChicaResponse>> ObtenerHistorialCajasAsync(CancellationToken ct = default);
    Task<List<MovimientoCajaResponse>> ObtenerMovimientosAsync(
        Guid? cajaChicaId = null,
        TipoMovimientoCaja? tipo = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        CancellationToken ct = default);

    Task<ServiceResult<CajaChicaDetalleResponse>> AperturarCajaAsync(
        AperturaCajaRequest request,
        Guid? usuarioId,
        CancellationToken ct = default);

    Task<ServiceResult<CajaChicaDetalleResponse>> CerrarCajaAsync(
        CierreCajaRequest request,
        Guid? usuarioId,
        CancellationToken ct = default);

    Task<ServiceResult<MovimientoCajaResponse>> RegistrarMovimientoAsync(
        RegistrarMovimientoCajaRequest request,
        Guid? usuarioId,
        CancellationToken ct = default);

    Task<ResumenMetodosPagoCajaResponse> ObtenerResumenMetodosAsync(
        Guid? cajaChicaId = null,
        CancellationToken ct = default);
}
