namespace CRMTeamBenavides.Domain.Entities;

/// <summary>
/// Reglas de la afectación del IGV que comparten la orden, la venta y la compra. Una
/// línea gratuita (retiro o bonificación) es gravada: su precio se escribe con o sin IGV
/// como cualquier gravada, pero no se cobra. Queda con base, IGV y total en 0, y su
/// valor de referencia es lo que se habría cobrado.
/// </summary>
public static class AfectacionIgv
{
    /// <summary>Las afectaciones que se pueden elegir en un pedido a Lima: las cobradas.</summary>
    public static readonly IReadOnlyList<TipoAfectacionIgv> Cobradas =
        [TipoAfectacionIgv.Gravado, TipoAfectacionIgv.Exonerado, TipoAfectacionIgv.Inafecto];

    public static bool EsGratuita(this TipoAfectacionIgv afectacion) =>
        afectacion is >= TipoAfectacionIgv.GravadoRetiroPorPremio
            and <= TipoAfectacionIgv.GravadoRetiroPorEntregaATrabajadores;

    /// <summary>Gravada, cobrada o gratuita: el precio puede escribirse con IGV incluido.</summary>
    public static bool EsGravada(this TipoAfectacionIgv afectacion) =>
        afectacion == TipoAfectacionIgv.Gravado || afectacion.EsGratuita();

    public static string Nombre(this TipoAfectacionIgv afectacion) => afectacion switch
    {
        TipoAfectacionIgv.Gravado => "Gravado – Operación onerosa",
        TipoAfectacionIgv.Exonerado => "Exonerado",
        TipoAfectacionIgv.Inafecto => "Inafecto",
        TipoAfectacionIgv.GravadoRetiroPorPremio => "Gravado – Retiro por premio",
        TipoAfectacionIgv.GravadoRetiroPorDonacion => "Gravado – Retiro por donación",
        TipoAfectacionIgv.GravadoRetiro => "Gravado – Retiro",
        TipoAfectacionIgv.GravadoRetiroPorPublicidad => "Gravado – Retiro por publicidad",
        TipoAfectacionIgv.GravadoBonificaciones => "Gravado – Bonificaciones",
        TipoAfectacionIgv.GravadoRetiroPorEntregaATrabajadores => "Gravado – Retiro por entrega a trabajadores",
        _ => afectacion.ToString()
    };

    /// <summary>
    /// Lo que se habría cobrado por una línea gratuita, con IGV: el total que tendría si
    /// fuera gravada. 0 si la línea se cobra.
    /// </summary>
    public static decimal ValorReferencial(
        TipoAfectacionIgv afectacion, int cantidad, decimal precioUnitario, decimal? precioUnitarioConIgv, decimal porcentajeIgv)
    {
        if (!afectacion.EsGratuita()) return 0m;

        if (precioUnitarioConIgv is decimal conIgv)
        {
            return decimal.Round(cantidad * conIgv, 2, MidpointRounding.AwayFromZero);
        }

        var baseImponible = decimal.Round(cantidad * precioUnitario, 2, MidpointRounding.AwayFromZero);
        var igv = decimal.Round(baseImponible * porcentajeIgv / 100m, 2, MidpointRounding.AwayFromZero);
        return baseImponible + igv;
    }

    /// <summary>Lo que la línea aporta a los ingresos sin IGV: nada si es gratuita.</summary>
    public static decimal IngresoSinIgv(TipoAfectacionIgv afectacion, int cantidad, decimal precioUnitario) =>
        afectacion.EsGratuita() ? 0m : cantidad * precioUnitario;
}
