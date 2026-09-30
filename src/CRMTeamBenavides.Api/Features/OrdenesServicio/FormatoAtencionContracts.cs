using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.OrdenesServicio;

public record FormatoAtencionTallerDto(
    string NombreTaller,
    string RazonSocial,
    string? Ruc,
    string? Direccion,
    string? Telefono,
    string? Email);

public record FormatoAtencionOrdenDto(
    Guid Id,
    string NumeroOrden,
    int EstadoId,
    string EstadoNombre,
    DateTime FechaIngreso,
    DateTime? FechaEstimadaEntrega,
    DateTime? FechaSalida,
    string TipoAtencion,
    string ModalidadAtencion,
    string? TipoFalla);

public record FormatoAtencionClienteDto(
    Guid Id,
    string NombreCompleto,
    string? RazonSocial,
    string? TipoDocumento,
    string? NumeroDocumento,
    string? Telefono,
    string? Email,
    string? Direccion);

public record FormatoAtencionUnidadDto(
    Guid Id,
    string TipoUnidad,
    string Marca,
    string Modelo,
    int? Anio,
    string? Placa,
    string? NumeroSerieVIN,
    string? NumeroMotor,
    string? Color,
    string TipoMedidor,
    decimal? LecturaIngreso,
    decimal? LecturaActualSalida);

public record FormatoAtencionTrabajoDto(
    string? MotivoFalla,
    string? Diagnostico,
    string? Solucion,
    string? Observaciones,
    string? TecnicoResponsable,
    string? TecnicoEmail);

public record FormatoAtencionItemDto(
    Guid Id,
    TipoItemServicio TipoItem,
    string TipoItemNombre,
    string Descripcion,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Total,
    string AfectacionIgv);

public record FormatoAtencionFinancieroDto(
    decimal SubtotalGravado,
    decimal SubtotalExonerado,
    decimal SubtotalInafecto,
    decimal MontoIgv,
    decimal Total,
    decimal TotalPagado,
    decimal SaldoPendiente,
    decimal PorcentajeIgv,
    string Moneda);

public record FormatoAtencionResponse(
    FormatoAtencionTallerDto Empresa,
    FormatoAtencionOrdenDto Orden,
    FormatoAtencionClienteDto Cliente,
    FormatoAtencionUnidadDto Unidad,
    FormatoAtencionTrabajoDto Trabajo,
    IReadOnlyList<FormatoAtencionItemDto> Items,
    FormatoAtencionFinancieroDto Financiero,
    DateTime FechaEmision);
