using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

/// <summary>
/// Cálculos de una compra, sin base de datos: montos de cada línea, costo por
/// unidad y nuevo costo del repuesto. La web repite los montos solo para mostrar
/// la vista previa (src/web/src/utils/compras.ts); los que valen son estos.
/// </summary>
public static class CalculoCompra
{
    public record MontosLinea(decimal Subtotal, decimal MontoIgv, decimal Total);

    public static decimal Redondear(decimal monto) =>
        Math.Round(monto, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Sin IGV: subtotal = cantidad × precio e IGV encima. Con IGV: el importe ya
    /// es el total y el subtotal sale de dividirlo. Exonerado e inafecto no llevan IGV.
    /// </summary>
    public static MontosLinea CalcularLinea(
        int cantidad,
        decimal precioUnitario,
        TipoAfectacionIgv afectacion,
        decimal porcentajeIgv,
        bool preciosIncluyenIgv)
    {
        var importe = Redondear(cantidad * precioUnitario);

        if (afectacion != TipoAfectacionIgv.Gravado || porcentajeIgv <= 0)
        {
            return new MontosLinea(importe, 0m, importe);
        }

        if (preciosIncluyenIgv)
        {
            var subtotal = Redondear(importe / (1 + porcentajeIgv / 100m));
            return new MontosLinea(subtotal, importe - subtotal, importe);
        }

        var igv = Redondear(importe * porcentajeIgv / 100m);
        return new MontosLinea(importe, igv, importe + igv);
    }

    /// <summary>
    /// Costo de una unidad en soles. Con factura el IGV es crédito fiscal y no es
    /// costo; con cualquier otro comprobante no se recupera y sí lo es.
    /// </summary>
    public static decimal CostoUnitarioSoles(
        MontosLinea montos,
        int cantidad,
        TipoComprobanteCompra tipoComprobante,
        decimal tipoCambio)
    {
        var baseCosto = tipoComprobante == TipoComprobanteCompra.Factura ? montos.Subtotal : montos.Total;
        return Math.Round(baseCosto * tipoCambio / cantidad, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// El costo del repuesto después de la compra. Un costo 0 se toma como
    /// desconocido, y una línea con precio 0 (una bonificación) nunca lo deja en 0:
    /// en el promedio solo baja el costo de lo que ya había en stock.
    /// </summary>
    public static decimal NuevoCosto(
        MetodoCosteo metodo,
        int stockAnterior,
        decimal costoAnterior,
        int cantidad,
        decimal costoCompra)
    {
        var sinHistoria = stockAnterior <= 0 || costoAnterior <= 0;

        if (metodo == MetodoCosteo.UltimoCosto || sinHistoria)
        {
            return costoCompra > 0 ? Redondear(costoCompra) : costoAnterior;
        }

        var valorTotal = stockAnterior * costoAnterior + cantidad * costoCompra;
        return Redondear(valorTotal / (stockAnterior + cantidad));
    }

    /// <summary>
    /// Número del comprobante sin ceros a la izquierda cuando es solo dígitos, para
    /// que «00000123» y «123» se reconozcan como el mismo comprobante.
    /// </summary>
    public static string NormalizarNumero(string numero)
    {
        var limpio = numero.Trim().ToUpperInvariant();
        if (limpio.Length > 0 && limpio.All(char.IsAsciiDigit))
        {
            limpio = limpio.TrimStart('0');
            return limpio.Length == 0 ? "0" : limpio;
        }

        return limpio;
    }
}
