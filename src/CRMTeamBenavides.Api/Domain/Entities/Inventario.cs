namespace CRMTeamBenavides.Domain.Entities;

public class CategoriaProducto : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Stock mínimo por defecto aplicado a productos de esta categoría si el producto no tiene uno propio.</summary>
    public int? StockMinimoDefault { get; set; }

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

    /// <summary>Precio de venta al público sin IGV o con afectación correspondiente.</summary>
    public decimal PrecioVenta { get; set; }

    /// <summary>Costo de adquisición o reposición del producto/repuesto.</summary>
    public decimal Costo { get; set; }

    public int StockActual { get; set; }

    /// <summary>Stock mínimo específico de este producto. Si es NULL, se resuelve mediante Categoria.StockMinimoDefault o 4.</summary>
    public int? StockMinimo { get; set; }

    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();

    /// <summary>
    /// Resuelve el stock mínimo efectivo aplicando la cascada:
    /// 1. Producto.StockMinimo
    /// 2. Categoria.StockMinimoDefault (o parámetro suministrado)
    /// 3. 4 (mínimo general por defecto)
    /// </summary>
    public int ObtenerStockMinimoEfectivo(int? stockMinimoCategoria = null)
    {
        if (StockMinimo.HasValue) return StockMinimo.Value;
        if (stockMinimoCategoria.HasValue) return stockMinimoCategoria.Value;
        if (Categoria?.StockMinimoDefault.HasValue == true) return Categoria.StockMinimoDefault.Value;
        return 4;
    }

    /// <summary>Indica si el producto se encuentra en alerta de stock bajo.</summary>
    public bool EsBajoStock => StockActual <= ObtenerStockMinimoEfectivo();
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

    /// <summary>Costo unitario de la operación de inventario para valorización y rentabilidad.</summary>
    public decimal? CostoUnitario { get; set; }

    public string? Motivo { get; set; } // "Venta #123", "Orden de servicio #45", "Ajuste manual"

    // Trazabilidad: de dónde vino el movimiento, sin crear dependencia circular fuerte
    public Guid? OrdenServicioId { get; set; }
    public Guid? VentaId { get; set; }
    public Guid? PedidoLimaId { get; set; }
}
