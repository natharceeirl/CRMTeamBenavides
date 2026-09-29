using CRMTeamBenavides.Api.Features.Configuracion;

namespace CRMTeamBenavides.Api.Services;

public interface IConfiguracionService
{
    Task<ConfiguracionEmpresaResponse> ObtenerConfiguracionEmpresaAsync(CancellationToken ct = default);
    Task<decimal> ObtenerPorcentajeIgvVigenteAsync(CancellationToken ct = default);
    Task<ServiceResult<ConfiguracionEmpresaResponse>> ActualizarConfiguracionEmpresaAsync(
        ActualizarConfiguracionEmpresaRequest request, CancellationToken ct = default);
}
