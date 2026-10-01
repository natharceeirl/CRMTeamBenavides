using System.ComponentModel.DataAnnotations;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Citas;

public record CrearCitaRequest(
    Guid? ClienteId,
    [Required] Guid VehiculoId,
    [Required] DateTime FechaHoraProgramada,
    int? DuracionMinutos,
    [Required] string Motivo,
    string? Observaciones);

public record ReprogramarCitaRequest(
    [Required] DateTime NuevaFechaHoraProgramada,
    int? NuevaDuracionMinutos,
    string? MotivoReprogramacion);

public record ActualizarCitaRequest(
    [Required] Guid VehiculoId,
    [Required] DateTime FechaHoraProgramada,
    int? DuracionMinutos,
    [Required] string Motivo,
    string? Observaciones);

public record CambiarEstadoCitaRequest(
    [Required] EstadoCita NuevoEstado,
    string? Observacion);

public record CancelarCitaRequest(
    [Required] string MotivoCancelacion);

public record CitaListResponse(
    Guid Id,
    string NumeroCita,
    Guid ClienteId,
    string ClienteNombre,
    Guid VehiculoId,
    string? VehiculoPlaca,
    string VehiculoModelo,
    DateTime FechaHoraProgramada,
    int DuracionMinutos,
    string Motivo,
    EstadoCita Estado,
    string EstadoDescripcion,
    Guid? OrdenServicioId,
    string? NumeroOrdenServicio);

public record HistorialEstadoCitaResponse(
    Guid Id,
    EstadoCita? EstadoAnterior,
    string? EstadoAnteriorDescripcion,
    EstadoCita EstadoNuevo,
    string EstadoNuevoDescripcion,
    Guid? UsuarioId,
    string? UsuarioNombre,
    DateTime Fecha,
    string? Observacion);

public record CitaDetalleResponse(
    Guid Id,
    string NumeroCita,
    Guid ClienteId,
    string ClienteNombre,
    string? ClienteDocumento,
    string? ClienteTelefono,
    string? ClienteEmail,
    Guid VehiculoId,
    string VehiculoMarca,
    string VehiculoModelo,
    string? VehiculoPlaca,
    int? VehiculoAnio,
    DateTime FechaHoraProgramada,
    int DuracionMinutos,
    DateTime FechaHoraFinEstimada,
    string Motivo,
    string? Observaciones,
    EstadoCita Estado,
    string EstadoDescripcion,
    string? MotivoCancelacion,
    Guid? OrdenServicioId,
    string? NumeroOrdenServicio,
    List<HistorialEstadoCitaResponse> Historial,
    DateTime FechaCreacion);
