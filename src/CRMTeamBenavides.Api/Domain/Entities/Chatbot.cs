namespace CRMTeamBenavides.Domain.Entities;

public enum EstadoAtencionConsulta
{
    Pendiente = 0,
    EnAtencion = 1,
    Resuelto = 2,
    Descartado = 3
}

public class FaqItem : BaseEntity
{
    public string Categoria { get; set; } = string.Empty;
    public string Pregunta { get; set; } = string.Empty;
    public string Respuesta { get; set; } = string.Empty;
    public string? PalabrasClave { get; set; }
    public int Orden { get; set; } = 0;
    public int VecesConsultada { get; set; } = 0;
}

public class ConsultaChatbot : BaseEntity
{
    public string Canal { get; set; } = "Web";

    public Guid? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string? NombreContacto { get; set; }
    public string? TelefonoContacto { get; set; }

    public string MensajeConsulta { get; set; } = string.Empty;

    public Guid? FaqItemId { get; set; }
    public FaqItem? FaqItem { get; set; }

    public bool RequiereAtencionAgente { get; set; } = false;
    public EstadoAtencionConsulta EstadoAtencion { get; set; } = EstadoAtencionConsulta.Pendiente;

    public Guid? AgenteAsignadoId { get; set; }
    public Usuario? AgenteAsignado { get; set; }

    public string? NotasAgente { get; set; }
    public DateTime? FechaDerivacion { get; set; }
    public DateTime? FechaResolucion { get; set; }
}
