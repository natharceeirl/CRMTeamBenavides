namespace CRMTeamBenavides.Domain.Entities;

public enum EstadoVenta
{
    Cotizacion,
    Confirmada,
    Anulada
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
    public decimal Total { get; set; }

    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
    public Comprobante? Comprobante { get; set; }
}

public class DetalleVenta : BaseEntity
{
    public Guid VentaId { get; set; }
    public Venta Venta { get; set; } = null!;

    public Guid ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;
}

/// <summary>
/// Estructura mínima para el alcance aprobado (gestión de información para comprobantes).
/// Ajustar campos exactos cuando el Ingeniero cierre el alcance de facturación con el cliente.
/// </summary>
public class Comprobante : BaseEntity
{
    public Guid VentaId { get; set; }
    public Venta Venta { get; set; } = null!;

    public string Tipo { get; set; } = "Boleta"; // Boleta, Factura, etc.
    public string? Serie { get; set; }
    public string? Numero { get; set; }
    public string Estado { get; set; } = "Emitido";
}
