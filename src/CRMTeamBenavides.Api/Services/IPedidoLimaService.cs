using CRMTeamBenavides.Api.Features.PedidosLima;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface IPedidoLimaService
{
    Task<List<PedidoLimaResponse>> GetAllAsync(
        Guid? soloClienteId = null,
        Guid? clienteId = null,
        EstadoPedidoLima? estado = null,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        string? guia = null,
        CancellationToken ct = default);

    Task<ServiceResult<PedidoLimaResponse>> GetByIdAsync(
        Guid id,
        Guid? soloClienteId = null,
        CancellationToken ct = default);

    Task<ServiceResult<PedidoLimaResponse>> CrearAsync(
        CrearPedidoLimaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        bool puedeModificarPrecios = false,
        CancellationToken ct = default);

    Task<ServiceResult<PedidoLimaResponse>> ActualizarAsync(
        Guid id,
        ActualizarPedidoLimaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default);

    Task<ServiceResult<PedidoLimaResponse>> CambiarEstadoAsync(
        Guid id,
        CambiarEstadoPedidoLimaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        bool puedeDespachar = false,
        CancellationToken ct = default);

    Task<ServiceResult<PedidoLimaResponse>> CancelarAsync(
        Guid id,
        CancelarPedidoLimaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default);

    Task<byte[]> ExportarExcelAsync(
        Guid? soloClienteId = null,
        Guid? clienteId = null,
        EstadoPedidoLima? estado = null,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default);

    Task<ServiceResult<PedidoLimaResponse>> AprobacionGerenciaAsync(
        Guid id,
        AprobacionGerenciaPedidoLimaRequest request,
        Guid? usuarioId = null,
        CancellationToken ct = default);
}
