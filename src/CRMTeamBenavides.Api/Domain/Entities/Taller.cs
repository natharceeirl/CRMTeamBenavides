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

public enum TipoAtencion
{
    MantenimientoPreventivo,
    MantenimientoCorrectivo,
    ReclamoGarantia,
    Gratuito
}

public enum ModalidadAtencion
{
    EnTaller,
    EnSitio
}

public enum TipoFalla
{
    Menor,
    Mayor
}

public class OrdenServicio : BaseEntity
{
    public Guid VehiculoId { get; set; }
    public Vehiculo Vehiculo { get; set; } = null!;

    public Guid ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public Guid? TecnicoAsignadoId { get; set; }
    public Usuario? TecnicoAsignado { get; set; }

    /// <summary>Código o correlativo visible (ej. OS-000001).</summary>
    public string? NumeroOrden { get; set; }

    public EstadoOrdenServicio Estado { get; set; } = EstadoOrdenServicio.Abierta;

    // Fechas y horarios
    public DateTime FechaApertura { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCierre { get; set; }
    public DateTime FechaIngreso { get; set; } = DateTime.UtcNow;
    public DateTime? FechaSalida { get; set; }
    public DateTime? FechaEstimadaEntrega { get; set; }

    // Falla y diagnóstico
    public string? MotivoFalla { get; set; }
    public string? Diagnostico { get; set; }
    public string? Solucion { get; set; }
    public string? Observaciones { get; set; }

    // Clasificación del servicio
    public TipoAtencion TipoAtencion { get; set; } = TipoAtencion.MantenimientoPreventivo;
    public ModalidadAtencion ModalidadAtencion { get; set; } = ModalidadAtencion.EnTaller;
    public TipoFalla? TipoFalla { get; set; }

    // Medición al ingresar
    public int? KilometrajeIngreso { get; set; }
    public decimal? HorasUsoIngreso { get; set; }

    public ICollection<DetalleServicio> Detalles { get; set; } = new List<DetalleServicio>();
    public ICollection<HistorialEstadoOrden> HistorialEstados { get; set; } = new List<HistorialEstadoOrden>();
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}

/// <summary>Historial o timeline de transiciones de estado de una orden de servicio.</summary>
public class HistorialEstadoOrden : BaseEntity
{
    public Guid OrdenServicioId { get; set; }
    public OrdenServicio OrdenServicio { get; set; } = null!;

    public EstadoOrdenServicio? EstadoAnterior { get; set; }
    public EstadoOrdenServicio EstadoNuevo { get; set; }

    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public DateTime FechaCambio { get; set; } = DateTime.UtcNow;
    public string? Observaciones { get; set; }
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
