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

public enum TipoItemServicio
{
    Repuesto,
    Servicio,
    ManoDeObra,
    Terceros
}

public enum TipoAfectacionIgv
{
    Gravado,
    Exonerado,
    Inafecto
}

public enum EstadoPresupuestoCliente
{
    Pendiente,
    Aprobado,
    Rechazado
}

public enum EstadoAprobacionGerencia
{
    NoAplica,
    Pendiente,
    Aprobado,
    Rechazado
}

/// <summary>Catálogo de servicios ofrecidos por el taller (sin código obligatorio).</summary>
public class Servicio : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public decimal PrecioSugerido { get; set; }
    public TipoAfectacionIgv TipoAfectacionIgv { get; set; } = TipoAfectacionIgv.Gravado;
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
    public decimal? LecturaMedidorIngreso { get; set; }

    // Totales financieros calculados e históricos
    public decimal SubtotalGravado { get; set; }
    public decimal SubtotalExonerado { get; set; }
    public decimal SubtotalInafecto { get; set; }
    public decimal MontoIgv { get; set; }
    public decimal Total { get; set; }

    // Aprobaciones y autorizaciones
    public EstadoPresupuestoCliente EstadoPresupuestoCliente { get; set; } = EstadoPresupuestoCliente.Pendiente;
    public EstadoAprobacionGerencia EstadoAprobacionGerencia { get; set; } = EstadoAprobacionGerencia.NoAplica;
    public DateTime? FechaRespuestaCliente { get; set; }
    public string? ObservacionesPresupuestoCliente { get; set; }
    public DateTime? FechaAprobacionGerencia { get; set; }
    public Guid? UsuarioAprobacionGerenciaId { get; set; }
    public Usuario? UsuarioAprobacionGerencia { get; set; }
    public string? ObservacionesAprobacionGerencia { get; set; }

    public ICollection<DetalleServicio> Detalles { get; set; } = new List<DetalleServicio>();
    public ICollection<HistorialEstadoOrden> HistorialEstados { get; set; } = new List<HistorialEstadoOrden>();
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Comprobante> Comprobantes { get; set; } = new List<Comprobante>();
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

/// <summary>Línea tipada de una orden: repuesto, servicio, mano de obra o terceros con afectación tributaria e IGV.</summary>
public class DetalleServicio : BaseEntity
{
    public Guid OrdenServicioId { get; set; }
    public OrdenServicio OrdenServicio { get; set; } = null!;

    public TipoItemServicio TipoItem { get; set; } = TipoItemServicio.Repuesto;

    /// <summary>Nulo si no es un repuesto del inventario.</summary>
    public Guid? ProductoId { get; set; }
    public Producto? Producto { get; set; }

    /// <summary>Nulo si no proviene del catálogo de servicios.</summary>
    public Guid? ServicioId { get; set; }
    public Servicio? Servicio { get; set; }

    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }

    /// <summary>Costo unitario vigente al momento de agregar el ítem (inmutable).</summary>
    public decimal CostoUnitarioHistorico { get; set; }

    /// <summary>Afectación tributaria: Gravado, Exonerado o Inafecto.</summary>
    public TipoAfectacionIgv TipoAfectacionIgv { get; set; } = TipoAfectacionIgv.Gravado;

    /// <summary>Subtotal base imponible gravado.</summary>
    public decimal SubtotalGravado { get; set; }

    /// <summary>Porcentaje de IGV aplicado históricamente (ej. 18.00).</summary>
    public decimal PorcentajeIgvAplicado { get; set; }

    /// <summary>Monto de IGV calculado para la línea.</summary>
    public decimal MontoIgv { get; set; }

    /// <summary>Total de la línea.</summary>
    public decimal Total { get; set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;
}
