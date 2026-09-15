namespace CRMTeamBenavides.Domain.Entities;

public enum EstadoOrdenServicio
{
    Abierta,
    Diagnostico,
    Aprobada,
    EnProceso,
    Lista,
    Entregada,
    Cancelada
}

public class OrdenServicio : BaseEntity
{
    public Guid VehiculoId { get; set; }
    public Vehiculo Vehiculo { get; set; } = null!;

    public Guid? TecnicoAsignadoId { get; set; }
    public Usuario? TecnicoAsignado { get; set; }

    public string? Diagnostico { get; set; }
    public EstadoOrdenServicio Estado { get; set; } = EstadoOrdenServicio.Abierta;
    public DateTime FechaApertura { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCierre { get; set; }
    public string? Observaciones { get; set; }

    public ICollection<DetalleServicio> Detalles { get; set; } = new List<DetalleServicio>();
}

/// <summary>Cada línea de una orden: puede ser mano de obra o un repuesto usado.</summary>
public class DetalleServicio : BaseEntity
{
    public Guid OrdenServicioId { get; set; }
    public OrdenServicio OrdenServicio { get; set; } = null!;

    /// <summary>Nulo si es solo mano de obra, sin repuesto asociado.</summary>
    public Guid? ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public string Descripcion { get; set; } = string.Empty; // ej: "Mano de obra - cambio de aceite"
    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;
}
