using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

/// <summary>
/// Una orden puede llevar varios tipos de atención (por ejemplo, mantenimiento completo y
/// correctivo). La orden guarda la lista y además un tipo principal, que es el que usan
/// Yamaha, los reportes y el Excel. Regla confirmada el 04/11/2026: si hay completo o
/// preventivo, el principal es mantenimiento (Yamaha «MP»); si no, garantía, luego
/// gratuito y al final correctivo.
/// </summary>
public static class TiposAtencionOrden
{
    /// <summary>De mayor a menor prioridad: el principal es el primero de la lista.</summary>
    private static readonly TipoAtencion[] Prioridad =
    [
        TipoAtencion.MantenimientoCompleto,
        TipoAtencion.MantenimientoPreventivo,
        TipoAtencion.ReclamoGarantia,
        TipoAtencion.Gratuito,
        TipoAtencion.MantenimientoCorrectivo,
    ];

    /// <summary>Sin repetidos y en el orden de prioridad, así se muestran igual en todas partes.</summary>
    public static List<TipoAtencion> Normalizar(IEnumerable<TipoAtencion> tipos) =>
        tipos.Distinct().OrderBy(tipo => Array.IndexOf(Prioridad, tipo)).ToList();

    /// <summary>Los tipos que pidió quien abre o edita la orden; si no mandó la lista, el tipo único.</summary>
    public static List<TipoAtencion> Pedidos(IReadOnlyCollection<TipoAtencion>? lista, TipoAtencion unico) =>
        lista is { Count: > 0 } ? Normalizar(lista) : [unico];

    /// <summary>Por qué la lista no sirve, o null si sirve.</summary>
    public static string? Validar(IReadOnlyCollection<TipoAtencion> tipos)
    {
        if (tipos.Count == 0)
            return "Elige al menos un tipo de atención.";
        if (tipos.Any(tipo => !Enum.IsDefined(tipo)))
            return "Uno de los tipos de atención no es válido.";
        return null;
    }

    /// <summary>Guarda la lista y su tipo principal.</summary>
    public static void Asignar(OrdenServicio orden, IEnumerable<TipoAtencion> tipos)
    {
        var lista = Normalizar(tipos);
        orden.TiposAtencion = lista;
        orden.TipoAtencion = lista.Count > 0 ? lista[0] : TipoAtencion.MantenimientoPreventivo;
    }

    /// <summary>La lista de la orden. Una orden anterior a la lista tiene solo su tipo principal.</summary>
    public static List<TipoAtencion> DeLaOrden(OrdenServicio orden) =>
        orden.TiposAtencion is { Count: > 0 } ? Normalizar(orden.TiposAtencion) : [orden.TipoAtencion];

    /// <summary>Cómo se lee en el formato de atención impreso.</summary>
    public static string Nombre(TipoAtencion tipo) => tipo switch
    {
        TipoAtencion.MantenimientoPreventivo => "Mantenimiento preventivo",
        TipoAtencion.MantenimientoCorrectivo => "Mantenimiento correctivo",
        TipoAtencion.ReclamoGarantia => "Reclamo de garantía",
        TipoAtencion.Gratuito => "Gratuito",
        TipoAtencion.MantenimientoCompleto => "Mantenimiento completo",
        _ => tipo.ToString(),
    };
}
