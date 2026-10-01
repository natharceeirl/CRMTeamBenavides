using System.Text.Json.Serialization;

namespace CRMTeamBenavides.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoPedidoLima
{
    Pendiente,
    Confirmado,
    EnPreparacion,
    EnTransito,
    Recibido,
    Entregado,
    Cancelado
}

/// <summary>
/// Pedido especial de repuestos o piezas traídas desde Lima u otro origen central.
/// </summary>
public class PedidoLima : BaseEntity
{
    /// <summary>Código correlativo visible único (ej. PL-000001).</summary>
    public string? NumeroPedido { get; set; }

    public Guid ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public EstadoPedidoLima Estado { get; set; } = EstadoPedidoLima.Pendiente;

    /// <summary>Empresa de courier o transporte de carga (ej. Shalom, Marvisur, Olva Courier).</summary>
    public string? EmpresaTransporte { get; set; }

    /// <summary>Número de tracking, remito o guía de remisión del transportista.</summary>
    public string? NumeroGuia { get; set; }

    /// <summary>Fecha estimada de arribo a la agencia o taller.</summary>
    public DateTime? FechaEstimadaLlegada { get; set; }

    /// <summary>Fecha real en que el pedido fue recibido en el taller.</summary>
    public DateTime? FechaLlegada { get; set; }

    /// <summary>Fecha en que los repuestos fueron despachados/entregados al cliente.</summary>
    public DateTime? FechaEntrega { get; set; }

    // Desglose financiero tributario histórico
    public decimal SubtotalGravado { get; set; }
    public decimal SubtotalExonerado { get; set; }
    public decimal SubtotalInafecto { get; set; }
    public decimal PorcentajeIgv { get; set; }
    public decimal MontoIgv { get; set; }
    public decimal Total { get; set; }

    public string? Observaciones { get; set; }
    public string? MotivoCancelacion { get; set; }

    /// <summary>Indica si el stock ya fue deducido del inventario (al despachar/entregar).</summary>
    public bool StockDeducido { get; set; }

    public ICollection<DetallePedidoLima> Detalles { get; set; } = new List<DetallePedidoLima>();
    public ICollection<HistorialEstadoPedidoLima> HistorialEstados { get; set; } = new List<HistorialEstadoPedidoLima>();
}

/// <summary>
/// Línea o ítem que compone un pedido especial desde Lima.
/// </summary>
public class DetallePedidoLima : BaseEntity
{
    public Guid PedidoLimaId { get; set; }
    public PedidoLima PedidoLima { get; set; } = null!;

    public Guid ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int Cantidad { get; set; }

    /// <summary>Precio unitario pactado con el cliente al registrar el pedido.</summary>
    public decimal PrecioUnitario { get; set; }

    /// <summary>Costo unitario histórico de adquisición al momento de crear el pedido.</summary>
    public decimal CostoUnitarioHistorico { get; set; }

    public TipoAfectacionIgv TipoAfectacionIgv { get; set; } = TipoAfectacionIgv.Gravado;

    public decimal SubtotalGravado { get; set; }
    public decimal PorcentajeIgvAplicado { get; set; }
    public decimal MontoIgv { get; set; }
    public decimal Total { get; set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;
}

/// <summary>
/// Bitácora de transiciones de estado de un pedido especial de Lima.
/// </summary>
public class HistorialEstadoPedidoLima : BaseEntity
{
    public Guid PedidoLimaId { get; set; }
    public PedidoLima PedidoLima { get; set; } = null!;

    public EstadoPedidoLima? EstadoAnterior { get; set; }
    public EstadoPedidoLima EstadoNuevo { get; set; }

    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Observacion { get; set; }
}
