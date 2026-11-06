using CRMTeamBenavides.Api.Features.Motos;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

/// <summary>Una moto lista para guardar: la serie normalizada y los demás datos limpios.</summary>
public sealed record MotoNormalizada(
    string NumeroSerieVIN, string? NumeroMotor, int? Anio, string? Color, string? Dua, string? ItemDua);

/// <summary>
/// Las reglas de las motos del inventario que comparten las compras, las ventas y el
/// ingreso inicial. Cada moto tiene su serie única; entra en stock y sale vendida, y al
/// venderla se crea la unidad del cliente con la misma serie. No se separan con
/// adelanto: una cotización no aparta la moto.
/// </summary>
public static class MotosInventario
{
    public const string MensajeStockManual =
        "El stock de las motos lo mueven las compras y las ventas, con la serie de cada una. Para el inventario inicial usa «Ingresar moto».";

    /// <summary>Valida y limpia las motos de una línea: una por cada unidad comprada.</summary>
    public static (List<MotoNormalizada> Motos, string? Error) ValidarIngreso(
        IReadOnlyList<UnidadMotoRequest>? motos, int cantidad, string donde)
    {
        if (motos is null || motos.Count != cantidad)
            return ([], $"{donde}: escribe la serie de cada moto. Son {cantidad} y hay {motos?.Count ?? 0}.");

        var limpias = new List<MotoNormalizada>(motos.Count);
        var maximoAnio = HoraPeru.Hoy().Year + 1;
        foreach (var moto in motos)
        {
            var serie = VehiculoService.NormalizarSerie(moto.NumeroSerieVIN);
            if (serie is null)
                return ([], $"{donde}: cada moto necesita su serie (VIN o chasis).");
            if (serie.Length > 50)
                return ([], $"{donde}: la serie {serie} pasa de 50 caracteres.");
            if (moto.Anio is { } anio && (anio < 1950 || anio > maximoAnio))
                return ([], $"{donde}: el año de la moto {serie} no es válido.");

            var motor = Mayusculas(moto.NumeroMotor);
            var color = Texto(moto.Color);
            var dua = Mayusculas(moto.Dua);
            var item = Texto(moto.ItemDua);
            if (motor?.Length > 50 || color?.Length > 50 || dua?.Length > 50 || item?.Length > 20)
                return ([], $"{donde}: un dato de la moto {serie} es demasiado largo.");

            limpias.Add(new MotoNormalizada(serie, motor, moto.Anio, color, dua, item));
        }

        var repetida = limpias.GroupBy(m => m.NumeroSerieVIN).FirstOrDefault(g => g.Count() > 1);
        if (repetida is not null)
            return ([], $"{donde}: la serie {repetida.Key} está repetida.");

        return (limpias, null);
    }

    /// <summary>Por qué alguna de esas series ya no se puede usar, o null si todas están libres.</summary>
    public static async Task<string?> SerieOcupadaAsync(ApplicationDbContext contexto, IReadOnlyCollection<string> series, CancellationToken ct = default)
    {
        if (series.Count == 0)
            return null;

        var enInventario = await contexto.UnidadesInventario
            .Where(u => u.Activo && series.Contains(u.NumeroSerieVIN))
            .Select(u => u.NumeroSerieVIN)
            .FirstOrDefaultAsync(ct);
        if (enInventario is not null)
            return $"La moto con serie {enInventario} ya está registrada en el inventario.";

        var deCliente = await contexto.Vehiculos
            .Where(v => v.Activo && v.NumeroSerieVIN != null && series.Contains(v.NumeroSerieVIN))
            .Select(v => v.NumeroSerieVIN)
            .FirstOrDefaultAsync(ct);
        if (deCliente is not null)
            return $"La serie {deCliente} ya es la unidad de un cliente: revisa Unidades.";

        return null;
    }

    public static UnidadInventario NuevaUnidad(Guid productoId, MotoNormalizada moto, decimal costo, Guid? usuarioId) => new()
    {
        ProductoId = productoId,
        NumeroSerieVIN = moto.NumeroSerieVIN,
        NumeroMotor = moto.NumeroMotor,
        Anio = moto.Anio,
        Color = moto.Color,
        Dua = moto.Dua,
        ItemDua = moto.ItemDua,
        CostoUnitario = Math.Round(costo, 2, MidpointRounding.AwayFromZero),
        Estado = EstadoUnidadInventario.EnStock,
        CreadoPorId = usuarioId,
        FechaCreacion = DateTime.UtcNow,
        Activo = true,
    };

    /// <summary>
    /// Las motos que se eligieron para una línea de venta, sin apartarlas: una por cada
    /// unidad y todas del mismo modelo y en stock. Un repuesto no lleva motos.
    /// </summary>
    public static async Task<(List<Guid> Ids, string? Error)> ValidarSeleccionAsync(
        ApplicationDbContext contexto, Producto producto, int cantidad, IReadOnlyCollection<Guid>? elegidas)
    {
        var ids = (elegidas ?? []).Distinct().ToList();
        if (producto.Tipo != TipoProducto.Vehiculo)
        {
            return ids.Count == 0 ? ([], null) : ([], $"«{producto.Nombre}» no es una moto: no lleva serie.");
        }

        if (ids.Count != cantidad)
            return ([], $"Elige la serie de cada moto «{producto.Nombre}»: son {cantidad} y elegiste {ids.Count}.");

        var motos = await contexto.UnidadesInventario.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToListAsync();
        var error = ErrorDeSeleccion(motos, ids, producto);
        return error is null ? (ids, null) : ([], error);
    }

    /// <summary>Si las líneas de una venta eligen la misma moto dos veces.</summary>
    public static string? MotoRepetida(IEnumerable<IReadOnlyCollection<Guid>?> lineas)
    {
        var todas = lineas.SelectMany(ids => ids ?? []).ToList();
        return todas.Count == todas.Distinct().Count() ? null : "La misma moto está elegida en dos líneas de la venta.";
    }

    /// <summary>
    /// Al confirmar la venta: bloquea las motos, comprueba que sigan en stock, las marca
    /// vendidas y crea la unidad de cada una para el cliente, con la garantía desde hoy.
    /// </summary>
    public static async Task<string?> VenderAsync(ApplicationDbContext contexto, Venta venta, DetalleVenta detalle, Producto producto)
    {
        if (producto.Tipo != TipoProducto.Vehiculo)
            return null;

        var ids = detalle.UnidadesInventarioIds.Distinct().ToList();
        if (ids.Count != detalle.Cantidad)
            return $"Elige la serie de cada moto «{producto.Nombre}»: son {detalle.Cantidad} y hay {ids.Count}.";

        if (contexto.Database.IsRelational())
        {
            var arreglo = ids.ToArray();
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"UnidadesInventario\" WHERE \"Id\" = ANY({arreglo}) ORDER BY \"Id\" FOR UPDATE");
        }

        var motos = await contexto.UnidadesInventario.Where(u => ids.Contains(u.Id)).ToListAsync();
        var error = ErrorDeSeleccion(motos, ids, producto);
        if (error is not null)
            return error;

        var series = motos.Select(m => m.NumeroSerieVIN).ToList();
        var deCliente = await contexto.Vehiculos
            .Where(v => v.Activo && v.NumeroSerieVIN != null && series.Contains(v.NumeroSerieVIN))
            .Select(v => v.NumeroSerieVIN)
            .FirstOrDefaultAsync();
        if (deCliente is not null)
            return $"La serie {deCliente} ya es la unidad de un cliente: revisa Unidades antes de venderla.";

        var ahora = DateTime.UtcNow;
        foreach (var moto in motos)
        {
            var unidad = new Vehiculo
            {
                ClienteId = venta.ClienteId,
                TipoUnidad = TipoUnidad.Motocicleta,
                Marca = string.IsNullOrWhiteSpace(producto.Marca) ? "Yamaha" : producto.Marca.Trim(),
                Modelo = producto.Nombre.Trim(),
                Anio = moto.Anio,
                Color = moto.Color,
                NumeroSerieVIN = moto.NumeroSerieVIN,
                NumeroMotor = moto.NumeroMotor,
                Dua = moto.Dua,
                ItemDua = moto.ItemDua,
                FechaInicioGarantia = HoraPeru.Hoy(),
                TipoMedidor = TipoMedidor.Kilometraje,
                Kilometraje = 0,
                LecturaMedidorActual = 0,
                Observaciones = "Vendida por el taller.",
                FechaCreacion = ahora,
                Activo = true,
            };
            contexto.Vehiculos.Add(unidad);

            moto.Estado = EstadoUnidadInventario.Vendida;
            moto.VentaId = venta.Id;
            moto.DetalleVentaId = detalle.Id;
            moto.FechaVenta = ahora;
            moto.VehiculoId = unidad.Id;
            moto.FechaModificacion = ahora;
        }

        // La ganancia de la línea usa el costo real de las motos vendidas.
        detalle.CostoUnitarioHistorico = Math.Round(motos.Average(m => m.CostoUnitario), 2, MidpointRounding.AwayFromZero);
        return null;
    }

    /// <summary>
    /// Al anular la venta: las motos vuelven al stock. La unidad del cliente se da de baja
    /// si todavía no pasó por el taller; si ya tiene órdenes, se conserva su historial.
    /// </summary>
    public static async Task RevertirVentaAsync(ApplicationDbContext contexto, Guid ventaId)
    {
        var motos = await contexto.UnidadesInventario
            .Where(u => u.Activo && u.VentaId == ventaId)
            .ToListAsync();
        if (motos.Count == 0)
            return;

        var unidadIds = motos.Where(m => m.VehiculoId.HasValue).Select(m => m.VehiculoId!.Value).ToList();
        var conOrdenes = await contexto.OrdenesServicio
            .Where(o => unidadIds.Contains(o.VehiculoId))
            .Select(o => o.VehiculoId)
            .Distinct()
            .ToListAsync();
        var unidades = await contexto.Vehiculos.Where(v => unidadIds.Contains(v.Id)).ToListAsync();

        var ahora = DateTime.UtcNow;
        foreach (var unidad in unidades.Where(u => !conOrdenes.Contains(u.Id)))
        {
            unidad.Activo = false;
            unidad.FechaModificacion = ahora;
        }

        foreach (var moto in motos)
        {
            moto.Estado = EstadoUnidadInventario.EnStock;
            moto.VentaId = null;
            moto.DetalleVentaId = null;
            moto.FechaVenta = null;
            moto.VehiculoId = null;
            moto.FechaModificacion = ahora;
        }
    }

    private static string? ErrorDeSeleccion(List<UnidadInventario> motos, List<Guid> ids, Producto producto)
    {
        if (motos.Count != ids.Count || motos.Any(m => !m.Activo))
            return "Una de las motos elegidas ya no está en el inventario.";

        var deOtroModelo = motos.FirstOrDefault(m => m.ProductoId != producto.Id);
        if (deOtroModelo is not null)
            return $"La moto {deOtroModelo.NumeroSerieVIN} no es del modelo «{producto.Nombre}».";

        var vendida = motos.FirstOrDefault(m => m.Estado != EstadoUnidadInventario.EnStock);
        return vendida is null ? null : $"La moto {vendida.NumeroSerieVIN} ya se vendió: elige otra.";
    }

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? Mayusculas(string? valor) => Texto(valor)?.ToUpperInvariant();
}
