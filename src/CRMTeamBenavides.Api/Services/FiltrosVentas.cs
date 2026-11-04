using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

/// <summary>Situación del comprobante por la que se filtra el registro de ventas.</summary>
public enum FiltroComprobanteVenta
{
    /// <summary>Venta confirmada que todavía no tiene comprobante.</summary>
    Pendiente,
    Emitido,
    Anulado,
}

/// <summary>
/// Los filtros del registro de ventas. La lista y su Excel usan los mismos, para que el
/// Excel exporte exactamente lo que se ve en pantalla.
/// </summary>
public sealed record FiltrosVentas(
    Guid? ClienteId = null,
    EstadoVenta? Estado = null,
    Guid? OrdenServicioId = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    FiltroComprobanteVenta? Comprobante = null,
    string? Busqueda = null)
{
    public IQueryable<Venta> Aplicar(IQueryable<Venta> query)
    {
        if (ClienteId.HasValue)
            query = query.Where(v => v.ClienteId == ClienteId.Value);

        if (Estado.HasValue)
            query = query.Where(v => v.Estado == Estado.Value);

        if (OrdenServicioId.HasValue)
            query = query.Where(v => v.OrdenServicioId == OrdenServicioId.Value);

        // Los días son los del taller: una venta de las 20:00 en Arequipa ya es del día
        // siguiente en UTC, y con el día UTC se contaba en otra fecha. «Hasta» incluye el día.
        if (FechaDesde.HasValue)
        {
            var desde = HoraPeru.InicioDelDiaUtc(FechaDesde.Value);
            query = query.Where(v => v.Fecha >= desde);
        }

        if (FechaHasta.HasValue)
        {
            var hastaExclusivo = HoraPeru.InicioDelDiaUtc(FechaHasta.Value.Date.AddDays(1));
            query = query.Where(v => v.Fecha < hastaExclusivo);
        }

        // El comprobante anulado sigue ligado a la venta: no vuelve a «Pendiente».
        query = Comprobante switch
        {
            FiltroComprobanteVenta.Pendiente =>
                query.Where(v => v.Estado == EstadoVenta.Confirmada && v.Comprobante == null),
            FiltroComprobanteVenta.Emitido =>
                query.Where(v => v.Comprobante != null && v.Comprobante.Estado == "Emitido"),
            FiltroComprobanteVenta.Anulado =>
                query.Where(v => v.Comprobante != null && v.Comprobante.Estado == "Anulado"),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(Busqueda))
        {
            var texto = Busqueda.Trim();
            var patron = $"%{texto}%";
            // La lista muestra la venta como «#1A2B3C4D»: el inicio de su id.
            var inicioId = $"{texto.TrimStart('#')}%";

            query = query.Where(v =>
                EF.Functions.ILike(v.Cliente.NombreCompleto, patron) ||
                EF.Functions.ILike(v.Cliente.NumeroDocumento!, patron) ||
                EF.Functions.ILike(v.Cliente.DocumentoIdentidad!, patron) ||
                EF.Functions.ILike(v.OrdenServicio!.NumeroOrden!, patron) ||
                EF.Functions.ILike(v.Comprobante!.Serie + "-" + v.Comprobante.Numero, patron) ||
                EF.Functions.ILike(v.Id.ToString(), inicioId));
        }

        return query;
    }
}
