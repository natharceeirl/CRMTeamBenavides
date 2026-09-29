using System.Text.Json.Serialization;

namespace CRMTeamBenavides.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoComprobante
{
    Boleta,
    Factura
}

public enum EstadoVenta
{
    Cotizacion,
    Confirmada,
    Anulada
}

/// <summary>
/// Catálogo tipado de métodos de pago aceptados en mostrador y taller.
/// </summary>
public class MetodoPago : BaseEntity
{
    public string Codigo { get; set; } = null!; // EFECTIVO, TARJETA, TRANSFERENCIA, YAPE_PLIN
    public string Nombre { get; set; } = null!; // Efectivo, Tarjeta, etc.
}

/// <summary>
/// Registro individual de pago o anticipo asociado a una Venta o a una Orden de Servicio.
/// </summary>
public class Pago : BaseEntity
{
    public decimal Monto { get; set; }

    public Guid MetodoPagoId { get; set; }
    public MetodoPago MetodoPago { get; set; } = null!;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Referencia { get; set; }

    public Guid? VentaId { get; set; }
    public Venta? Venta { get; set; }

    public Guid? OrdenServicioId { get; set; }
    public OrdenServicio? OrdenServicio { get; set; }

    /// <summary>Indica si el pago fue registrado como anticipo/adelanto.</summary>
    public bool EsAnticipo { get; set; }

    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string? Observaciones { get; set; }
}

public class Venta : BaseEntity
{
    public Guid ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    /// <summary>Nulo si la venta es de mostrador, sin orden de servicio asociada.</summary>
    public Guid? OrdenServicioId { get; set; }
    public OrdenServicio? OrdenServicio { get; set; }

    public EstadoVenta Estado { get; set; } = EstadoVenta.Cotizacion;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    // Desglose financiero tributario
    public decimal SubtotalGravado { get; set; }
    public decimal SubtotalExonerado { get; set; }
    public decimal SubtotalInafecto { get; set; }
    public decimal MontoIgv { get; set; }
    public decimal Total { get; set; }

    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public Comprobante? Comprobante { get; set; }
}

public class DetalleVenta : BaseEntity
{
    public Guid VentaId { get; set; }
    public Venta Venta { get; set; } = null!;

    public Guid? ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public TipoItemServicio TipoItem { get; set; } = TipoItemServicio.Repuesto;
    public Guid? ServicioId { get; set; }
    public Servicio? Servicio { get; set; }

    /// <summary>Referencia al DetalleServicio si proviene de una liquidación de Orden de Servicio.</summary>
    public Guid? DetalleServicioOrigenId { get; set; }

    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal CostoUnitarioHistorico { get; set; }
    public TipoAfectacionIgv TipoAfectacionIgv { get; set; } = TipoAfectacionIgv.Gravado;
    public decimal SubtotalGravado { get; set; }
    public decimal PorcentajeIgvAplicado { get; set; }
    public decimal MontoIgv { get; set; }
    public decimal Total { get; set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;
}

public class Comprobante : BaseEntity
{
    public Guid VentaId { get; set; }
    public Venta Venta { get; set; } = null!;

    public string Tipo { get; set; } = "Boleta"; // Boleta, Factura
    public string? Serie { get; set; }
    public string? Numero { get; set; }
    public string Estado { get; set; } = "Emitido";

    public decimal SubtotalGravado { get; set; }
    public decimal SubtotalExonerado { get; set; }
    public decimal SubtotalInafecto { get; set; }
    public decimal PorcentajeIgv { get; set; }
    public decimal MontoIgv { get; set; }
    public decimal Total { get; set; }

    public string? MetodoPagoPrincipal { get; set; }
    public string? Observaciones { get; set; }

    public Guid? OrdenServicioId { get; set; }
    public OrdenServicio? OrdenServicio { get; set; }
}
