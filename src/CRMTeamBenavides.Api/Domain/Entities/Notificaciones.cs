namespace CRMTeamBenavides.Domain.Entities;

public enum PlataformaDispositivo
{
    Android,
    IOS
}

/// <summary>Qué pasó. Cada tipo sabe a qué pantalla lleva al tocar la notificación.</summary>
public enum TipoNotificacion
{
    SolicitudAprobacion,
    PresupuestoEnviado,
    OrdenLista,
    PedidoLimaRecibido,
    CitaConfirmada,
    OrdenAsignada
}

public enum EstadoEnvioNotificacion
{
    /// <summary>En la cola: el envío en segundo plano todavía no la manda al teléfono.</summary>
    Pendiente,
    Enviada,
    /// <summary>El usuario no tiene teléfonos registrados: queda solo en su bandeja.</summary>
    SinDispositivo,
    /// <summary>Se agotaron los reintentos: queda en la bandeja con el último error.</summary>
    Fallida
}

/// <summary>Teléfono donde un usuario recibe notificaciones (el token de Firebase de esa instalación).</summary>
public class DispositivoNotificacion : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Token { get; set; } = string.Empty;
    public PlataformaDispositivo Plataforma { get; set; }

    /// <summary>La última vez que la app lo registró: al iniciar sesión o al renovar el token.</summary>
    public DateTime UltimoRegistro { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Aviso para un usuario. Es su bandeja (leída o no) y también la cola de envío
/// al teléfono: se crea en la misma transacción que el evento y un proceso en
/// segundo plano la envía después, con reintentos.
/// </summary>
public class Notificacion : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public TipoNotificacion Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>A dónde lleva al tocarla: «OrdenServicio», «Cita», «PedidoLima» o «Aprobaciones».</summary>
    public string? Entidad { get; set; }
    public Guid? EntidadId { get; set; }

    /// <summary>
    /// Avisos del mismo asunto que se juntan mientras siguen en la cola, como
    /// varias solicitudes de precio de una misma orden («aprobacion:OrdenServicio:{id}»).
    /// </summary>
    public string? ClaveAgrupacion { get; set; }

    /// <summary>Cuántos avisos junta. Uno, salvo los agrupados.</summary>
    public int Cantidad { get; set; } = 1;

    public DateTime? FechaLeida { get; set; }

    public EstadoEnvioNotificacion EstadoEnvio { get; set; } = EstadoEnvioNotificacion.Pendiente;
    public int Intentos { get; set; }
    public DateTime? ProximoIntento { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public string? UltimoError { get; set; }
}
