namespace CRMTeamBenavides.Domain.Entities;

/// <summary>Cómo una compra actualiza el costo del repuesto.</summary>
public enum MetodoCosteo
{
    PromedioPonderado,
    UltimoCosto
}

/// <summary>Configuración general de la empresa y parámetros fiscales (IGV, etc.).</summary>
public class ConfiguracionEmpresa : BaseEntity
{
    public string NombreEmpresa { get; set; } = "Team Benavides";
    public string RazonSocial { get; set; } = "Team Benavides S.R.L.";
    public string? Ruc { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public decimal PorcentajeIgv { get; set; } = 18.00m;
    public string MonedaBase { get; set; } = "PEN";
    public decimal? TipoCambioVigente { get; set; }
    public DateTime? FechaActualizacionTipoCambio { get; set; }
    public MetodoCosteo MetodoCosteo { get; set; } = MetodoCosteo.PromedioPonderado;
}
