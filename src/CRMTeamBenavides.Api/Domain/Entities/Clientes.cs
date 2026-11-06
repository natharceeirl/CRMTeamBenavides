namespace CRMTeamBenavides.Domain.Entities;

public enum TipoDocumentoCliente
{
    DNI,
    RUC,
    Otro
}

public class Cliente : BaseEntity
{
    public string NombreCompleto { get; set; } = string.Empty;

    // De una persona, como en su DNI: el nombre completo los une en este orden. Una
    // empresa o un cliente registrado antes los tiene vacíos.
    public string? Nombres { get; set; }
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }

    /// <summary>Si el cliente es una empresa (ej. flota).</summary>
    public string? RazonSocial { get; set; }

    /// <summary>Tipo de documento oficial de identidad (DNI, RUC, Otro).</summary>
    public TipoDocumentoCliente? TipoDocumento { get; set; }

    /// <summary>Número de documento de identidad validado.</summary>
    public string? NumeroDocumento { get; set; }

    /// <summary>DNI / RUC histórico (mantenido por retrocompatibilidad de consultas).</summary>
    public string? DocumentoIdentidad { get; set; }

    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Observaciones { get; set; }

    /// <summary>Vínculo con el usuario para acceso al Portal del Cliente.</summary>
    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>
    /// Código con el que el cliente activa su cuenta en la app, junto con su DNI. Lo
    /// genera el personal en la ficha; se guarda solo su hash y sirve una vez.
    /// </summary>
    public string? CodigoActivacionHash { get; set; }
    public DateTime? CodigoActivacionVence { get; set; }
    public int CodigoActivacionIntentos { get; set; }

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

    /// <summary>Lectura actual del medidor de la unidad (kilometraje u horas según TipoMedidor).</summary>
    public decimal? LecturaMedidorActual { get; set; }

    /// <summary>Valor comercial o estimado de la unidad si aplica.</summary>
    public decimal? ValorEstimado { get; set; }

    public string? Observaciones { get; set; }

    // Datos de la moto que vendió el taller. La tarjeta de propiedad y la placa llegan
    // después de la venta: mientras falten, la unidad figura con «Tarjeta pendiente».
    public string? Dua { get; set; }
    public string? ItemDua { get; set; }
    public string? NumeroTarjetaPropiedad { get; set; }

    /// <summary>Inicio de la garantía: por defecto el día de la venta; se corrige si la entrega fue otro día.</summary>
    public DateOnly? FechaInicioGarantia { get; set; }

    public ICollection<OrdenServicio> OrdenesServicio { get; set; } = new List<OrdenServicio>();
}
