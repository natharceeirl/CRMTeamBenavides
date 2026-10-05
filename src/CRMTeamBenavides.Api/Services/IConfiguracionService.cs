using CRMTeamBenavides.Api.Features.Configuracion;

namespace CRMTeamBenavides.Api.Services;

public interface IConfiguracionService
{
    Task<ConfiguracionEmpresaResponse> ObtenerConfiguracionEmpresaAsync(CancellationToken ct = default);
    Task<decimal> ObtenerPorcentajeIgvVigenteAsync(CancellationToken ct = default);
    Task<ServiceResult<ConfiguracionEmpresaResponse>> ActualizarConfiguracionEmpresaAsync(
        ActualizarConfiguracionEmpresaRequest request,
        Guid? usuarioId,
        CancellationToken ct = default);

    Task<TipoCambioResponse> ObtenerTipoCambioVigenteAsync(CancellationToken ct = default);
    Task<ServiceResult<TipoCambioResponse>> ActualizarTipoCambioAsync(
        ActualizarTipoCambioRequest request,
        Guid? usuarioId,
        CancellationToken ct = default);
    Task<List<HistorialTipoCambioResponse>> ObtenerHistorialTipoCambioAsync(CancellationToken ct = default);
    Task<ServiceResult<ConversionMonedaResponse>> ConvertirUsdAPenAsync(decimal montoUsd, decimal? tipoCambio = null, CancellationToken ct = default);
}
