namespace CRMTeamBenavides.Domain.Entities;

/// <summary>
/// Repuesto o vehículo. Un producto de tipo Vehículo es el modelo de moto (con su precio
/// y su stock); cada moto física es una <see cref="UnidadInventario"/> con su serie.
/// </summary>
public enum TipoProducto
{
    Repuesto,
    Vehiculo
}

/// <summary>
/// Una moto no se separa con adelanto (decidido con el cliente el 05/11/2026): está en
/// stock hasta que se confirma su venta.
/// </summary>
public enum EstadoUnidadInventario
{
    EnStock,
    Vendida
}

/// <summary>
/// Cada moto del inventario, identificada por su serie. Entra por una compra (o por un
/// ingreso de inventario inicial) y sale por una venta, que crea la unidad del cliente
/// con la misma serie para que el taller la reconozca.
/// </summary>
public class UnidadInventario : BaseEntity
{
    public Guid ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    /// <summary>VIN o número de chasis, sin espacios y en mayúsculas. Único entre las activas.</summary>
    public string NumeroSerieVIN { get; set; } = string.Empty;
    public string? NumeroMotor { get; set; }
    public int? Anio { get; set; }
    public string? Color { get; set; }
    public string? Dua { get; set; }
    public string? ItemDua { get; set; }

    /// <summary>Costo de esta moto en soles, sin IGV, como lo dejó su compra.</summary>
    public decimal CostoUnitario { get; set; }

    public EstadoUnidadInventario Estado { get; set; } = EstadoUnidadInventario.EnStock;

    public Guid? CompraId { get; set; }
    public Compra? Compra { get; set; }
    public Guid? DetalleCompraId { get; set; }

    public Guid? VentaId { get; set; }
    public Venta? Venta { get; set; }
    public Guid? DetalleVentaId { get; set; }
    public DateTime? FechaVenta { get; set; }

    /// <summary>La unidad del cliente que se creó al venderla.</summary>
    public Guid? VehiculoId { get; set; }
    public Vehiculo? Vehiculo { get; set; }
}
