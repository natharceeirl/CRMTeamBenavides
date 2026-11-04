using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

/// <summary>El IGV vigente y el tipo de cambio del día, si se registró.</summary>
public sealed record ParametrosPrecio(decimal PorcentajeIgv, decimal? TipoCambio);

/// <summary>
/// El precio de lista de un repuesto en soles, hoy. <see cref="ConIgv"/> es lo que se
/// cobra cuando el repuesto se registró con IGV incluido; <see cref="SinIgv"/> es la base.
/// </summary>
public readonly record struct PrecioDeLista(decimal SinIgv, decimal? ConIgv)
{
    /// <summary>El precio como lo escribe el personal: con IGV si el repuesto lo incluye.</summary>
    public decimal Referencia => ConIgv ?? SinIgv;
}

/// <summary>
/// Traduce el precio registrado de un repuesto (moneda e «Incluye IGV») al precio en
/// soles que usan las órdenes, las ventas y los pedidos. Es el único lugar que lee
/// <see cref="Producto.PrecioVenta"/> como monto.
/// </summary>
public static class PrecioLista
{
    public const string MensajeSinTipoDeCambio =
        "El precio de este repuesto está en dólares: registra el tipo de cambio del día en Configuración.";

    public static async Task<ParametrosPrecio> ParametrosAsync(ApplicationDbContext contexto, CancellationToken ct = default)
    {
        var config = await contexto.ConfiguracionesEmpresa.AsNoTracking().FirstOrDefaultAsync(ct);
        var tipoCambio = config?.TipoCambioVigente is > 0 ? config.TipoCambioVigente : null;
        return new ParametrosPrecio(config?.PorcentajeIgv ?? 18.00m, tipoCambio);
    }

    /// <summary>
    /// Null si el repuesto está en dólares y no hay tipo de cambio. Los dólares se pasan
    /// a soles con la regla del conversor: hacia arriba al sol entero.
    /// </summary>
    public static PrecioDeLista? Calcular(Producto producto, decimal? tipoCambio, decimal porcentajeIgv)
    {
        decimal enSoles;
        if (producto.MonedaPrecio == MonedaPrecio.USD)
        {
            if (tipoCambio is not > 0) return null;
            enSoles = Math.Ceiling(producto.PrecioVenta * tipoCambio.Value);
        }
        else
        {
            enSoles = producto.PrecioVenta;
        }

        return producto.PrecioIncluyeIgv
            ? new PrecioDeLista(QuitarIgv(enSoles, porcentajeIgv), enSoles)
            : new PrecioDeLista(enSoles, null);
    }

    /// <summary>
    /// Un precio escrito para una línea, en el modo del repuesto: con IGV si lo incluye
    /// y la línea es gravada. Una línea exonerada o inafecta no lleva IGV.
    /// </summary>
    public static PrecioDeLista DeEscrito(decimal escrito, bool incluyeIgv, TipoAfectacionIgv afectacion, decimal porcentajeIgv) =>
        incluyeIgv && afectacion == TipoAfectacionIgv.Gravado
            ? new PrecioDeLista(QuitarIgv(escrito, porcentajeIgv), escrito)
            : new PrecioDeLista(escrito, null);

    public static decimal QuitarIgv(decimal conIgv, decimal porcentajeIgv) =>
        decimal.Round(conIgv / (1 + porcentajeIgv / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>El precio de la línea como se escribió: con IGV si se cobró así.</summary>
    public static decimal Referencia(decimal precioUnitario, decimal? precioUnitarioConIgv) =>
        precioUnitarioConIgv ?? precioUnitario;
}
