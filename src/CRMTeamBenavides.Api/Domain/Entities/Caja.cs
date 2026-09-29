namespace CRMTeamBenavides.Domain.Entities;

public enum EstadoCajaChica
{
    Abierta = 0,
    Cerrada = 1
}

public enum TipoMovimientoCaja
{
    Ingreso = 0,
    Egreso = 1
}

/// <summary>
/// Turno de caja chica operativa para el taller / mostrador.
/// Maneja aperturas, cierres y control estricto de saldos en backend.
/// </summary>
public class CajaChica : BaseEntity
{
    public decimal MontoApertura { get; set; }
    public decimal? MontoCierre { get; set; }
    public decimal SaldoCalculado { get; set; }
    public DateTime FechaApertura { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCierre { get; set; }
    public EstadoCajaChica Estado { get; set; } = EstadoCajaChica.Abierta;
    public string? ObservacionesApertura { get; set; }
    public string? ObservacionesCierre { get; set; }

    public Guid? UsuarioAperturaId { get; set; }
    public Usuario? UsuarioApertura { get; set; }

    public Guid? UsuarioCierreId { get; set; }
    public Usuario? UsuarioCierre { get; set; }

    public ICollection<MovimientoCajaChica> Movimientos { get; set; } = new List<MovimientoCajaChica>();
}

/// <summary>
/// Movimiento individual de ingreso o egreso de caja chica.
/// </summary>
public class MovimientoCajaChica : BaseEntity
{
    public Guid CajaChicaId { get; set; }
    public CajaChica CajaChica { get; set; } = null!;

    public TipoMovimientoCaja Tipo { get; set; }
    public decimal Monto { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}
