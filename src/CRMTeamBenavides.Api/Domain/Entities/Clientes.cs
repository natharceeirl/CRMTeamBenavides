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

    /// <summary>Vínculo con el usuario para acceso al Portal del Cliente.</summary>
    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public ICollection<Vehiculo> Vehiculos { get; set; } = new List<Vehiculo>();
}

public enum TipoUnidad
{
    Motocicleta,
    Cuatrimoto,
    MotoAcuatica,
    Generador,
    Otro
}

public enum TipoMedidor
{
    Kilometraje,
    Horas
}

public class Vehiculo : BaseEntity
{
    public Guid ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public string? Placa { get; set; }
    public TipoUnidad TipoUnidad { get; set; } = TipoUnidad.Motocicleta;
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int? Anio { get; set; }
    public string? NumeroSerieVIN { get; set; }
    public string? NumeroMotor { get; set; }
    public string? Color { get; set; }

    public TipoMedidor TipoMedidor { get; set; } = TipoMedidor.Kilometraje;
    public int? Kilometraje { get; set; }
    public decimal? HorasUso { get; set; }

    /// <summary>Valor comercial o estimado de la unidad si aplica.</summary>
    public decimal? ValorEstimado { get; set; }

    public string? Observaciones { get; set; }

    public ICollection<OrdenServicio> OrdenesServicio { get; set; } = new List<OrdenServicio>();
}
