using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Notificaciones;

/// <summary>El token de Firebase de esta instalación de la app.</summary>
public record RegistrarDispositivoRequest(string Token, PlataformaDispositivo Plataforma);

public record DarDeBajaDispositivoRequest(string Token);

/// <summary>Entidad y EntidadId dicen a qué pantalla lleva: «OrdenServicio», «Cita», «PedidoLima» o «Aprobaciones».</summary>
public record NotificacionResponse(
    Guid Id,
    TipoNotificacion Tipo,
    string Titulo,
    string Mensaje,
    string? Entidad,
    Guid? EntidadId,
    int Cantidad,
    DateTime FechaCreacion,
    DateTime? FechaLeida);

public record ConteoNotificacionesResponse(int NoLeidas);
