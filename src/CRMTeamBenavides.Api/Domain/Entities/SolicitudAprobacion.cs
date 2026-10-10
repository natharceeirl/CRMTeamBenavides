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

    /// <summary>Descripción del cambio para leerla en pantalla; no lleva datos internos.</summary>
    public string DetalleCambio { get; set; } = string.Empty;

    /// <summary>Ítem de la orden o del pedido al que apunta el cambio de precio, si es por ítem.</summary>
    public Guid? DetalleId { get; set; }

    /// <summary>
    /// Qué decide la solicitud: "detalle_{id}" para un ítem, o "entidad_venta",
    /// "entidad_ordenservicio" y "entidad_pedidolima" para la operación completa.
    /// Una solicitud nueva con la misma clave reemplaza a la pendiente anterior.
    /// Null solo en las solicitudes anteriores a esta columna.
    /// </summary>
    public string? ClaveObjetivo { get; set; }

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
