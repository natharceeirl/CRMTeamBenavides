namespace CRMTeamBenavides.Domain.Entities;

/// <summary>
/// Campos comunes de auditoría para todas las entidades del sistema.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaModificacion { get; set; }
    public Guid? CreadoPorId { get; set; }
    public Guid? ModificadoPorId { get; set; }

    /// <summary>Soft delete: nunca borrar físicamente registros operativos.</summary>
    public bool Activo { get; set; } = true;
}
