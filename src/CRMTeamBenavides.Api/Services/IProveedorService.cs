using CRMTeamBenavides.Api.Features.Compras;

namespace CRMTeamBenavides.Api.Services;

public interface IProveedorService
{
    Task<List<ProveedorResponse>> ListarAsync(string? busqueda, CancellationToken ct = default);
    Task<ServiceResult<ProveedorResponse>> CrearAsync(GuardarProveedorRequest request, Guid? usuarioId, CancellationToken ct = default);
    Task<ServiceResult<ProveedorResponse>> ActualizarAsync(Guid id, GuardarProveedorRequest request, Guid? usuarioId, CancellationToken ct = default);
    Task<ServiceResult<bool>> EliminarAsync(Guid id, Guid? usuarioId, CancellationToken ct = default);
}
