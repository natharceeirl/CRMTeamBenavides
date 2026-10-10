namespace CRMTeamBenavides.Domain.Entities;

/// <summary>Comprobante que respalda una compra. Solo la factura da crédito fiscal.</summary>
public enum TipoComprobanteCompra
{
    Factura,
    Boleta,
    Ticket,
    NotaVenta,
    ReciboHonorarios,
    Otro
}

public enum MonedaCompra
{
    PEN,
    USD
}

public enum EstadoCompra
{
    Registrada,
    Anulada
}

/// <summary>Empresa o persona a la que el taller le compra repuestos o servicios.</summary>
public class Proveedor : BaseEntity
{
    public TipoDocumentoCliente TipoDocumento { get; set; } = TipoDocumentoCliente.RUC;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }

    /// <summary>Persona con la que se trata: vendedor o asesor del proveedor.</summary>
    public string? Contacto { get; set; }
}

/// <summary>
/// Compra a un proveedor, registrada desde su comprobante. Al registrarse suma el
/// stock de sus repuestos y actualiza su costo; no se edita lo que ya movió stock,
/// se anula y se registra de nuevo.
/// </summary>
public class Compra : BaseEntity
{
    /// <summary>Correlativo interno visible (CO-000001).</summary>
    public string NumeroCompra { get; set; } = string.Empty;

    public Guid ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;

    public TipoComprobanteCompra TipoComprobante { get; set; } = TipoComprobanteCompra.Factura;
    public string Serie { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public DateOnly FechaEmision { get; set; }
    public DateOnly? FechaVencimiento { get; set; }

    public MonedaCompra Moneda { get; set; } = MonedaCompra.PEN;

    /// <summary>Soles por dólar de esta compra; 1 si la compra es en soles.</summary>
    public decimal TipoCambio { get; set; } = 1m;

    public decimal PorcentajeIgv { get; set; }

    /// <summary>Si los precios unitarios se escribieron con IGV, como vienen en algunos comprobantes.</summary>
    public bool PreciosIncluyenIgv { get; set; }

    // Montos en la moneda de la compra
    public decimal SubtotalGravado { get; set; }
    public decimal SubtotalExonerado { get; set; }
    public decimal SubtotalInafecto { get; set; }
    public decimal MontoIgv { get; set; }
    public decimal Total { get; set; }

    /// <summary>El total convertido a soles con el tipo de cambio de la compra.</summary>
    public decimal TotalSoles { get; set; }

    public EstadoCompra Estado { get; set; } = EstadoCompra.Registrada;

    /// <summary>
    /// Pedido a Lima al que corresponde la compra. Ese pedido suma el stock al
    /// marcarse «Recibido», así que una compra ligada no mueve stock ni costo.
    /// </summary>
    public Guid? PedidoLimaId { get; set; }
    public PedidoLima? PedidoLima { get; set; }

    public string? GuiaRemision { get; set; }
    public string? Observaciones { get; set; }

    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public DateTime? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }
    public Guid? UsuarioAnulacionId { get; set; }
    public Usuario? UsuarioAnulacion { get; set; }

    public ICollection<DetalleCompra> Detalles { get; set; } = new List<DetalleCompra>();
    public ICollection<PagoCompra> Pagos { get; set; } = new List<PagoCompra>();
}

/// <summary>Línea de una compra: un repuesto del catálogo o un concepto libre (flete, servicio externo).</summary>
public class DetalleCompra : BaseEntity
{
    public Guid CompraId { get; set; }
    public Compra Compra { get; set; } = null!;

    /// <summary>Posición en el comprobante. Al anular, los costos se deshacen en orden inverso.</summary>
    public int Orden { get; set; }

    /// <summary>Nulo en un concepto libre, que no mueve stock.</summary>
    public Guid? ProductoId { get; set; }
    public Producto? Producto { get; set; }

    /// <summary>Nombre del repuesto al registrar, o el texto del concepto.</summary>
    public string Descripcion { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    /// <summary>Precio unitario tal como se escribió, en la moneda de la compra.</summary>
    public decimal PrecioUnitario { get; set; }

    public TipoAfectacionIgv TipoAfectacionIgv { get; set; } = TipoAfectacionIgv.Gravado;

    /// <summary>Monto sin IGV de la línea.</summary>
    public decimal Subtotal { get; set; }
    public decimal MontoIgv { get; set; }
    public decimal Total { get; set; }

    /// <summary>
    /// Costo por unidad en soles: sin IGV con factura, con IGV con cualquier otro
    /// comprobante, porque ese IGV no se recupera.
    /// </summary>
    public decimal CostoUnitarioSoles { get; set; }

    /// <summary>Si la línea sumó stock y costo. Falso en conceptos libres y en compras de un pedido a Lima.</summary>
    public bool MueveStock { get; set; }

    /// <summary>Costo del repuesto antes y después de esta línea, para deshacerlo al anular.</summary>
    public decimal? CostoAnteriorProducto { get; set; }
    public decimal? CostoResultanteProducto { get; set; }
}

/// <summary>
/// Pago al proveedor. El efectivo sale de la caja abierta como egreso; los demás
/// métodos no tocan la caja. Un pago anulado se conserva con su motivo.
/// </summary>
public class PagoCompra : BaseEntity
{
    public Guid CompraId { get; set; }
    public Compra Compra { get; set; } = null!;

    /// <summary>En la moneda de la compra.</summary>
    public decimal Monto { get; set; }

    /// <summary>El monto en soles con el tipo de cambio de la compra: lo que sale de la caja si es efectivo.</summary>
    public decimal MontoSoles { get; set; }

    public Guid MetodoPagoId { get; set; }
    public MetodoPago MetodoPago { get; set; } = null!;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Referencia { get; set; }
    public string? Observaciones { get; set; }

    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public bool Anulado { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }
    public Guid? UsuarioAnulacionId { get; set; }
    public Usuario? UsuarioAnulacion { get; set; }
}
