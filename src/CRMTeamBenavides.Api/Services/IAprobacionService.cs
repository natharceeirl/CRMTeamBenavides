using CRMTeamBenavides.Api.Features.Aprobaciones;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface IAprobacionService
{
    Task<List<SolicitudAprobacionResponse>> ObtenerPendientesAsync(CancellationToken ct = default);

    Task<List<SolicitudAprobacionResponse>> ObtenerHistorialAsync(
        string? entidad = null,
        string? tipo = null,
        EstadoAprobacionGerencia? estado = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        CancellationToken ct = default);

    Task<ServiceResult<SolicitudAprobacionResponse>> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<SolicitudAprobacionResponse>> CrearSolicitudAsync(
        RegistrarSolicitudAprobacionRequest request,
        Guid? usuarioId,
        CancellationToken ct = default);

    Task<ServiceResult<SolicitudAprobacionResponse>> ResolverSolicitudAsync(
        Guid id,
        ResolverSolicitudAprobacionRequest request,
        Guid? usuarioId,
        CancellationToken ct = default);
}
