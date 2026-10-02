namespace CRMTeamBenavides.Api.Services;

/// <summary>
/// Servicio centralizado para el registro consistente de eventos de auditoría del sistema.
/// </summary>
public interface IAuditoriaService
{
    Task RegistrarEventoAsync(
        Guid? usuarioId,
        string accion,
        string entidad,
        string entidadId,
        object? detalle = null,
        CancellationToken ct = default);
}
