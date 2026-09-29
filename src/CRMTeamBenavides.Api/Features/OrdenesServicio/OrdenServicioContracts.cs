using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.OrdenesServicio;

public record AperturaOrdenServicioRequest(
    Guid VehiculoId,
    Guid? TecnicoAsignadoId,
    string? Observaciones,
    string? MotivoFalla = null,
    DateTime? FechaEstimadaEntrega = null,
    TipoAtencion TipoAtencion = TipoAtencion.MantenimientoPreventivo,
    ModalidadAtencion ModalidadAtencion = ModalidadAtencion.EnTaller,
    TipoFalla? TipoFalla = null,
    int? KilometrajeIngreso = null,
    decimal? HorasUsoIngreso = null,
    decimal? LecturaMedidorIngreso = null);

public record RegistrarDiagnosticoRequest(
    string Diagnostico,
    Guid? TecnicoAsignadoId,
    string? Observaciones,
    string? Solucion = null,
    DateTime? FechaEstimadaEntrega = null,
    TipoFalla? TipoFalla = null);

public record ActualizarOrdenServicioRequest(
    string? MotivoFalla = null,
    string? Diagnostico = null,
    string? Solucion = null,
    string? Observaciones = null,
    DateTime? FechaEstimadaEntrega = null,
    TipoAtencion? TipoAtencion = null,
    ModalidadAtencion? ModalidadAtencion = null,
    TipoFalla? TipoFalla = null,
    int? KilometrajeIngreso = null,
    decimal? HorasUsoIngreso = null,
    decimal? LecturaMedidorIngreso = null,
    Guid? TecnicoAsignadoId = null);

public record AgregarDetalleServicioRequest(
    Guid? ProductoId = null,
    string? Descripcion = null,
    int Cantidad = 1,
    decimal? PrecioUnitario = null,
    Guid? ServicioId = null,
    TipoItemServicio? TipoItem = null,
    TipoAfectacionIgv? TipoAfectacionIgv = null);

public record AsignarTecnicoRequest(
    Guid? TecnicoId = null,
    Guid? TecnicoAsignadoId = null,
    string? Observaciones = null)
{
    public Guid TecnicoEfectivoId => TecnicoId ?? TecnicoAsignadoId ?? Guid.Empty;
}

public record CambiarEstadoOrdenServicioRequest(
    EstadoOrdenServicio NuevoEstado,
    string? Observaciones);

public record ResponderPresupuestoClienteRequest(
    EstadoPresupuestoCliente Estado,
    string? Observaciones = null);

public record AprobacionGerenciaRequest(
    EstadoAprobacionGerencia Estado,
    string? Observaciones = null);

public record DetalleServicioResponse(
    Guid Id,
    Guid? ProductoId,
    string? ProductoCodigo,
    string Descripcion,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal,
    bool EsRepuesto,
    Guid? ServicioId = null,
    string? ServicioNombre = null,
    TipoItemServicio TipoItem = TipoItemServicio.Repuesto,
    string? TipoItemNombre = null,
    decimal CostoUnitarioHistorico = 0m,
    TipoAfectacionIgv TipoAfectacionIgv = TipoAfectacionIgv.Gravado,
    string? TipoAfectacionIgvNombre = null,
    decimal SubtotalGravado = 0m,
    decimal PorcentajeIgvAplicado = 18.00m,
    decimal MontoIgv = 0m,
    decimal Total = 0m);

public record HistorialEstadoOrdenResponse(
    Guid Id,
    Guid OrdenServicioId,
    string? EstadoAnterior,
    int? EstadoAnteriorId,
    string EstadoNuevo,
    int EstadoNuevoId,
    Guid? UsuarioId,
    string? UsuarioNombre,
    DateTime FechaCambio,
    string? Observaciones);

public record OrdenServicioResponse(
    Guid Id,
    Guid VehiculoId,
    string? VehiculoPlaca,
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
    bool Activo,
    string? NumeroOrden = null,
    DateTime? FechaIngreso = null,
    DateTime? FechaEstimadaEntrega = null,
    DateTime? FechaSalida = null,
    string? MotivoFalla = null,
    string? Solucion = null,
    string? TipoAtencion = null,
    int? TipoAtencionId = null,
    string? ModalidadAtencion = null,
    int? ModalidadAtencionId = null,
    string? TipoFalla = null,
    int? TipoFallaId = null,
    int? KilometrajeIngreso = null,
    decimal? HorasUsoIngreso = null,
    decimal? LecturaMedidorIngreso = null,
    decimal SubtotalGravado = 0m,
    decimal SubtotalExonerado = 0m,
    decimal SubtotalInafecto = 0m,
    decimal MontoIgv = 0m,
    decimal Total = 0m,
    int EstadoPresupuestoClienteId = 0,
    string EstadoPresupuestoCliente = "Pendiente",
    DateTime? FechaRespuestaCliente = null,
    string? ObservacionesPresupuestoCliente = null,
    int EstadoAprobacionGerenciaId = 0,
    string EstadoAprobacionGerencia = "NoAplica",
    DateTime? FechaAprobacionGerencia = null,
    Guid? UsuarioAprobacionGerenciaId = null,
    string? UsuarioAprobacionGerenciaNombre = null,
    string? ObservacionesAprobacionGerencia = null,
    Guid? VentaId = null,
    string? ComprobanteSerieNumero = null);

public record OrdenServicioDetalleResponse(
    Guid Id,
    Guid VehiculoId,
    string? VehiculoPlaca,
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
    decimal Total,
    string? NumeroOrden = null,
    DateTime? FechaIngreso = null,
    DateTime? FechaEstimadaEntrega = null,
    DateTime? FechaSalida = null,
    string? MotivoFalla = null,
    string? Solucion = null,
    string? TipoAtencion = null,
    int? TipoAtencionId = null,
    string? ModalidadAtencion = null,
    int? ModalidadAtencionId = null,
    string? TipoFalla = null,
    int? TipoFallaId = null,
    int? KilometrajeIngreso = null,
    decimal? HorasUsoIngreso = null,
    decimal? LecturaMedidorIngreso = null,
    decimal SubtotalGravado = 0m,
    decimal SubtotalExonerado = 0m,
    decimal SubtotalInafecto = 0m,
    decimal MontoIgv = 0m,
    int EstadoPresupuestoClienteId = 0,
    string EstadoPresupuestoCliente = "Pendiente",
    DateTime? FechaRespuestaCliente = null,
    string? ObservacionesPresupuestoCliente = null,
    int EstadoAprobacionGerenciaId = 0,
    string EstadoAprobacionGerencia = "NoAplica",
    DateTime? FechaAprobacionGerencia = null,
    Guid? UsuarioAprobacionGerenciaId = null,
    string? UsuarioAprobacionGerenciaNombre = null,
    string? ObservacionesAprobacionGerencia = null,
    string? TipoUnidad = null,
    string? NumeroSerieVIN = null,
    string? NumeroMotor = null,
    List<HistorialEstadoOrdenResponse>? Historial = null,
    Guid? VentaId = null,
    string? ComprobanteSerieNumero = null);
