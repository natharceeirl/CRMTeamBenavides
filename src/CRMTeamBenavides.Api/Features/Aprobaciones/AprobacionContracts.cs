using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Aprobaciones;

public record SolicitudAprobacionResponse(
    Guid Id,
    string Tipo,
    string Entidad,
    string EntidadId,
    Guid? UsuarioSolicitanteId,
    string? UsuarioSolicitanteNombre,
    DateTime FechaSolicitud,
    int EstadoId,
    string Estado,
    string DetalleCambio,
    decimal? ValorAnterior,
    decimal? ValorSolicitado,
    string? Motivo,
    Guid? UsuarioAprobadorId,
    string? UsuarioAprobadorNombre,
    DateTime? FechaRespuesta,
    string? ObservacionesRespuesta
);

public record RegistrarSolicitudAprobacionRequest(
    string Tipo,
    string Entidad,
    string EntidadId,
    string DetalleCambio,
    decimal? ValorAnterior,
    decimal? ValorSolicitado,
    string? Motivo
);

public record ResolverSolicitudAprobacionRequest(
    EstadoAprobacionGerencia Estado,
    string? Observaciones
);
