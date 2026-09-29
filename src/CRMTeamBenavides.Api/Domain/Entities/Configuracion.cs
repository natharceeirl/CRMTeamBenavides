namespace CRMTeamBenavides.Domain.Entities;

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
}
