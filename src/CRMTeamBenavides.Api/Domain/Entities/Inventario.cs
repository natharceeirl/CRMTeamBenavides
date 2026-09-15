namespace CRMTeamBenavides.Domain.Entities;

public class CategoriaProducto : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

public class Producto : BaseEntity
{
    public Guid CategoriaId { get; set; }
    public CategoriaProducto Categoria { get; set; } = null!;

    public string Codigo { get; set; } = string.Empty; // SKU
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Unidad { get; set; } = "unidad"; // unidad, litro, etc.
    public decimal PrecioVenta { get; set; }
    public int StockActual { get; set; }
    public int StockMinimo { get; set; } // para alertas de stock bajo

    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}

public enum TipoMovimientoInventario
{
    Entrada,
    Salida,
    Ajuste
}

public class MovimientoInventario : BaseEntity
{
    public Guid ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public TipoMovimientoInventario Tipo { get; set; }
    public int Cantidad { get; set; }
    public string? Motivo { get; set; } // "Venta #123", "Orden de servicio #45", "Ajuste manual"

    // Trazabilidad: de dónde vino el movimiento, sin crear dependencia circular fuerte
    public Guid? OrdenServicioId { get; set; }
    public Guid? VentaId { get; set; }
}
