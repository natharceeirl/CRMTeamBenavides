namespace CRMTeamBenavides.Domain.Entities;

/// <summary>Configuración general de la empresa y parámetros fiscales (IGV, etc.).</summary>
public class ConfiguracionEmpresa : BaseEntity
{
    public string NombreEmpresa { get; set; } = "Team Benavides";
    public string? Ruc { get; set; }
    public decimal PorcentajeIgv { get; set; } = 18.00m;
}
