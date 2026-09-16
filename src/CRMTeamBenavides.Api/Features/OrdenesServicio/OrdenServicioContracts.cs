using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.OrdenesServicio;

public record AperturaOrdenServicioRequest(
    Guid VehiculoId,
    Guid? TecnicoAsignadoId,
    string? Observaciones);

public record RegistrarDiagnosticoRequest(
    string Diagnostico,
    Guid? TecnicoAsignadoId,
    string? Observaciones);

public record AgregarDetalleServicioRequest(
    Guid? ProductoId,
    string? Descripcion,
    int Cantidad,
    decimal? PrecioUnitario);

public record CambiarEstadoOrdenServicioRequest(
    EstadoOrdenServicio NuevoEstado,
    string? Observaciones);

public record DetalleServicioResponse(
    Guid Id,
    Guid? ProductoId,
    string? ProductoCodigo,
    string Descripcion,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal,
    bool EsRepuesto);

public record OrdenServicioResponse(
    Guid Id,
    Guid VehiculoId,
    string VehiculoPlaca,
    string VehiculoMarca,
    string VehiculoModelo,
    Guid ClienteId,
    string ClienteNombre,
    Guid? TecnicoAsignadoId,
    string? TecnicoNombre,
    string Estado,
    int EstadoId,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    string? Diagnostico,
    string? Observaciones,
    bool Activo);

public record OrdenServicioDetalleResponse(
    Guid Id,
    Guid VehiculoId,
    string VehiculoPlaca,
    string VehiculoMarca,
    string VehiculoModelo,
    int? VehiculoAnio,
    int? VehiculoKilometraje,
    string? VehiculoColor,
    Guid ClienteId,
    string ClienteNombre,
    string? ClienteTelefono,
    string? ClienteDocumentoIdentidad,
    Guid? TecnicoAsignadoId,
    string? TecnicoNombre,
    string Estado,
    int EstadoId,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    string? Diagnostico,
    string? Observaciones,
    bool Activo,
    List<DetalleServicioResponse> Detalles,
    decimal Total);
