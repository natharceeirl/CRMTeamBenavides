using CRMTeamBenavides.Api.Features.Citas;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface ICitaService
{
    Task<List<CitaListResponse>> GetAllAsync(
        Guid? soloClienteId = null,
        Guid? clienteId = null,
        Guid? vehiculoId = null,
        EstadoCita? estado = null,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default);

    Task<ServiceResult<CitaDetalleResponse>> GetByIdAsync(
        Guid id,
        Guid? soloClienteId = null,
        CancellationToken ct = default);

    Task<ServiceResult<CitaDetalleResponse>> CrearAsync(
        CrearCitaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default);

    Task<ServiceResult<CitaDetalleResponse>> ActualizarAsync(
        Guid id,
        ActualizarCitaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default);

    Task<ServiceResult<CitaDetalleResponse>> ReprogramarAsync(
        Guid id,
        ReprogramarCitaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default);

    Task<ServiceResult<CitaDetalleResponse>> CambiarEstadoAsync(
        Guid id,
        CambiarEstadoCitaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default);

    Task<ServiceResult<CitaDetalleResponse>> CancelarAsync(
        Guid id,
        CancelarCitaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default);

    Task<byte[]> ExportarExcelAsync(
        Guid? soloClienteId = null,
        Guid? clienteId = null,
        Guid? vehiculoId = null,
        EstadoCita? estado = null,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default);
}
