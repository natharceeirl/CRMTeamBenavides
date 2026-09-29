namespace CRMTeamBenavides.Domain.Entities;

/// <summary>
/// Historial y registro de cambios en el tipo de cambio entre PEN y USD (u otras monedas).
/// Permite mantener trazabilidad histórica para operaciones y reportes contables.
/// </summary>
public class HistorialTipoCambio : BaseEntity
{
    public string MonedaOrigen { get; set; } = "USD";
    public string MonedaDestino { get; set; } = "PEN";
    public decimal ValorCompra { get; set; }
    public decimal ValorVenta { get; set; }
    public DateTime FechaVigencia { get; set; } = DateTime.UtcNow;
    public string? Observaciones { get; set; }

    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}
