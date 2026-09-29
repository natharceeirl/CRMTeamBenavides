using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface IOrdenServicioService
{
    Task<List<OrdenServicioResponse>> GetAllAsync(
        Guid? vehiculoId = null,
        EstadoOrdenServicio? estado = null,
        Guid? clienteId = null,
        Guid? soloTecnicoId = null,
        Guid? soloClienteId = null,
        string? busqueda = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        Guid? tecnicoId = null);

    Task<ServiceResult<OrdenServicioDetalleResponse>> GetByIdAsync(
        Guid id,
        Guid? soloTecnicoId = null,
        Guid? soloClienteId = null);

    Task<ServiceResult<OrdenServicioResponse>> CreateAperturaAsync(
        AperturaOrdenServicioRequest request,
        Guid? usuarioId = null);

    Task<ServiceResult<OrdenServicioResponse>> RegistrarDiagnosticoAsync(
        Guid id,
        RegistrarDiagnosticoRequest request,
        Guid? soloTecnicoId = null,
        Guid? usuarioId = null);

    Task<ServiceResult<OrdenServicioResponse>> ActualizarAsync(
        Guid id,
        ActualizarOrdenServicioRequest request,
        Guid? soloTecnicoId = null,
        Guid? usuarioId = null);

    Task<ServiceResult<List<HistorialEstadoOrdenResponse>>> GetHistorialAsync(
        Guid id,
        Guid? soloTecnicoId = null,
        Guid? soloClienteId = null);

    Task<ServiceResult<DetalleServicioResponse>> AgregarDetalleAsync(
        Guid ordenServicioId,
        AgregarDetalleServicioRequest request,
        bool puedeModificarPrecios,
        Guid? soloTecnicoId = null);

    Task<ServiceResult<bool>> EliminarDetalleAsync(Guid ordenServicioId, Guid detalleId);

    Task<ServiceResult<OrdenServicioResponse>> CambiarEstadoAsync(
        Guid ordenServicioId,
        CambiarEstadoOrdenServicioRequest request,
        bool esTecnico,
        Guid? usuarioId = null);

    Task<ServiceResult<OrdenServicioResponse>> AsignarTecnicoAsync(
        Guid id,
        Guid tecnicoId,
        Guid? usuarioId = null);

    Task<ServiceResult<OrdenServicioResponse>> ResponderPresupuestoClienteAsync(
        Guid id,
        ResponderPresupuestoClienteRequest request,
        Guid? soloClienteId = null,
        Guid? usuarioId = null);

    Task<ServiceResult<OrdenServicioResponse>> AprobacionGerenciaAsync(
        Guid id,
        AprobacionGerenciaRequest request,
        Guid? usuarioId = null);
}
