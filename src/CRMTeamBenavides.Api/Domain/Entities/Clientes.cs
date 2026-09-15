namespace CRMTeamBenavides.Domain.Entities;

public class Cliente : BaseEntity
{
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Si el cliente es una empresa (ej. flota).</summary>
    public string? RazonSocial { get; set; }
    public string? DocumentoIdentidad { get; set; } // DNI / RUC
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Observaciones { get; set; }

    public ICollection<Vehiculo> Vehiculos { get; set; } = new List<Vehiculo>();
}

public class Vehiculo : BaseEntity
{
    public Guid ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public string Placa { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int? Anio { get; set; }
    public int? Kilometraje { get; set; }
    public string? Color { get; set; }
    public string? Observaciones { get; set; }

    public ICollection<OrdenServicio> OrdenesServicio { get; set; } = new List<OrdenServicio>();
}
