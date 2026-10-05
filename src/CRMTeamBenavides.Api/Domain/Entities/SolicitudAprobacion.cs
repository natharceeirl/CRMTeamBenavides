namespace CRMTeamBenavides.Domain.Entities;

/// <summary>
/// Solicitud de aprobación centralizada dirigida a Gerencia (cambios de precio, descuentos o excepciones comerciales).
/// </summary>
public class SolicitudAprobacion : BaseEntity
{
    /// <summary>Tipo de solicitud: "CambioPrecio", "Descuento", "ExcepcionComercial".</summary>
    public string Tipo { get; set; } = "CambioPrecio";

    /// <summary>Entidad origen: "OrdenServicio", "Venta", "PedidoLima".</summary>
    public string Entidad { get; set; } = string.Empty;

    /// <summary>Identificador de la entidad origen (ID como string).</summary>
    public string EntidadId { get; set; } = string.Empty;

    public Guid? UsuarioSolicitanteId { get; set; }
    public Usuario? UsuarioSolicitante { get; set; }
    public string? UsuarioSolicitanteNombre { get; set; }

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public EstadoAprobacionGerencia Estado { get; set; } = EstadoAprobacionGerencia.Pendiente;

    /// <summary>Descripción detallada del cambio propuesto.</summary>
    public string DetalleCambio { get; set; } = string.Empty;

    /// <summary>Precio base o valor original antes de la modificación.</summary>
    public decimal? ValorAnterior { get; set; }

    /// <summary>Precio o valor propuesto sujeto a aprobación.</summary>
    public decimal? ValorSolicitado { get; set; }

    /// <summary>Motivo o justificación comercial expresada por el solicitante.</summary>
    public string? Motivo { get; set; }

    public Guid? UsuarioAprobadorId { get; set; }
    public Usuario? UsuarioAprobador { get; set; }
    public string? UsuarioAprobadorNombre { get; set; }

    public DateTime? FechaRespuesta { get; set; }
    public string? ObservacionesRespuesta { get; set; }
}
