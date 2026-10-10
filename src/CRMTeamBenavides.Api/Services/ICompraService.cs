using CRMTeamBenavides.Api.Features.Compras;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

/// <summary>Filtros de la lista de compras. EstadoPago acepta los valores de <see cref="EstadosPagoCompra"/>.</summary>
public record FiltrosCompras(
    Guid? ProveedorId = null,
    EstadoCompra? Estado = null,
    string? EstadoPago = null,
    DateOnly? FechaDesde = null,
    DateOnly? FechaHasta = null,
    string? Busqueda = null);

public interface ICompraService
{
    Task<List<CompraResumenResponse>> ListarAsync(FiltrosCompras filtros, CancellationToken ct = default);
    Task<byte[]> ExportarExcelAsync(FiltrosCompras filtros, CancellationToken ct = default);
    Task<ServiceResult<CompraResponse>> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<CompraResponse>> RegistrarAsync(CrearCompraRequest request, Guid? usuarioId, CancellationToken ct = default);
    Task<ServiceResult<CompraResponse>> ActualizarAsync(Guid id, ActualizarCompraRequest request, Guid? usuarioId, CancellationToken ct = default);
    Task<ServiceResult<CompraResponse>> RegistrarPagoAsync(Guid id, RegistrarPagoCompraRequest request, Guid? usuarioId, CancellationToken ct = default);
    Task<ServiceResult<CompraResponse>> AnularPagoAsync(Guid id, Guid pagoId, AnularRequest request, Guid? usuarioId, CancellationToken ct = default);
    Task<ServiceResult<CompraResponse>> AnularAsync(Guid id, AnularRequest request, Guid? usuarioId, CancellationToken ct = default);
}
