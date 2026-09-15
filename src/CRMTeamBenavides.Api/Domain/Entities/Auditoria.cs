namespace CRMTeamBenavides.Domain.Entities;

/// <summary>
/// Registro de auditoría de acciones relevantes: usuario, fecha, operación y entidad afectada.
/// No hereda de BaseEntity a propósito: un log de auditoría no debería tener su propio
/// registro de auditoría ni soft delete.
/// </summary>
public class EventoAuditoria
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UsuarioId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Accion { get; set; } = string.Empty;   // "Crear", "Editar", "Eliminar"
    public string Entidad { get; set; } = string.Empty;  // "OrdenServicio", "Venta", etc.
    public string EntidadId { get; set; } = string.Empty;
    public string? Detalle { get; set; }                 // JSON con cambios, opcional
}
