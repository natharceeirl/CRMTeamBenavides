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

    /// <summary>
    /// Encola el evento de auditoría en la unidad de trabajo activa sin ejecutar <c>SaveChangesAsync</c>,
    /// permitiendo confirmarlo o revertirlo atómicamente junto con la transacción principal.
    /// </summary>
    void EncolarEventoEnTransaccion(
        Guid? usuarioId,
        string accion,
        string entidad,
        string entidadId,
        object? detalle = null,
        DateTime? fechaUtc = null)
    {
    }
}
