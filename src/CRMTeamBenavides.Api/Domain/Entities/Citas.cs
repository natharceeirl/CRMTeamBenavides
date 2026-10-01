using System.Text.Json.Serialization;

namespace CRMTeamBenavides.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoCita
{
    Pendiente,
    Confirmada,
    EnTaller,
    Completada,
    Cancelada,
    NoAsistio
}

/// <summary>
/// Cita programada en la agenda del taller para recepción y atención de una unidad.
/// </summary>
public class Cita : BaseEntity
{
    /// <summary>Código correlativo visible único (ej. CIT-000001).</summary>
    public string? NumeroCita { get; set; }

    public Guid ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public Guid VehiculoId { get; set; }
    public Vehiculo Vehiculo { get; set; } = null!;

    /// <summary>Fecha y hora pactada para la cita en UTC.</summary>
    public DateTime FechaHoraProgramada { get; set; }

    /// <summary>Duración estimada en minutos de la cita (por defecto 60 min).</summary>
    public int DuracionMinutos { get; set; } = 60;

    /// <summary>Motivo de la cita o servicio solicitado por el cliente.</summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Observaciones adicionales o requerimientos del cliente.</summary>
    public string? Observaciones { get; set; }

    /// <summary>Estado del flujo de la cita.</summary>
    public EstadoCita Estado { get; set; } = EstadoCita.Pendiente;

    /// <summary>Motivo especificado en caso de cancelación de la cita.</summary>
    public string? MotivoCancelacion { get; set; }

    /// <summary>Si la cita se materializó en una orden de servicio abierta.</summary>
    public Guid? OrdenServicioId { get; set; }
    public OrdenServicio? OrdenServicio { get; set; }

    public ICollection<HistorialEstadoCita> HistorialEstados { get; set; } = new List<HistorialEstadoCita>();
}

/// <summary>
/// Bitácora de transiciones de estado de una cita.
/// </summary>
public class HistorialEstadoCita : BaseEntity
{
    public Guid CitaId { get; set; }
    public Cita Cita { get; set; } = null!;

    public EstadoCita? EstadoAnterior { get; set; }
    public EstadoCita EstadoNuevo { get; set; }

    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Observacion { get; set; }
}
