using CRMTeamBenavides.Api.Features.Reportes;
using CRMTeamBenavides.Api.Services.Exportacion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class ReporteService : IReporteService
{
    private readonly ApplicationDbContext _context;
    private readonly IExportacionExcelService _excelService;

    public ReporteService(ApplicationDbContext context, IExportacionExcelService excelService)
    {
        _context = context;
        _excelService = excelService;
    }

    // ------------------------------------------------------------------
    // REPORTE 1: Órdenes de Servicio
    // ------------------------------------------------------------------
    public async Task<List<OrdenServicioReporteResponse>> GetOrdenesServicioAsync(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        EstadoOrdenServicio? estado,
        Guid? tecnicoId)
    {
        var query = _context.OrdenesServicio.Where(o => o.Activo);

        if (fechaDesde.HasValue)
        {
            var desde = DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc);
            query = query.Where(o => o.FechaApertura >= desde);
        }

        if (fechaHasta.HasValue)
        {
            // Límite superior exclusivo: inicio del día siguiente para incluir todo fechaHasta.
            var hastaExclusivo = DateTime.SpecifyKind(fechaHasta.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(o => o.FechaApertura < hastaExclusivo);
        }

        if (estado.HasValue)
            query = query.Where(o => o.Estado == estado.Value);

        if (tecnicoId.HasValue)
            query = query.Where(o => o.TecnicoAsignadoId == tecnicoId.Value);

        // Proyección SQL: todos los campos en una sola consulta — sin N+1.
        // TiempoAtencionHoras se calcula en C# post-materialización porque
        // EF.Functions.DateDiffHour solo existe en el proveedor SQL Server (no Npgsql).
        var filas = await query
            .OrderByDescending(o => o.FechaApertura)
            .Select(o => new
            {
                o.Id,
                o.VehiculoId,
                VehiculoPlaca  = o.Vehiculo.Placa,
                VehiculoMarca  = o.Vehiculo.Marca,
                VehiculoModelo = o.Vehiculo.Modelo,
                ClienteId      = o.Vehiculo.ClienteId,
                ClienteNombre  = o.Vehiculo.Cliente.NombreCompleto,
                o.TecnicoAsignadoId,
                TecnicoNombre  = o.TecnicoAsignado != null ? o.TecnicoAsignado.NombreCompleto : null,
                o.Estado,
                o.FechaApertura,
                o.FechaCierre,
                o.NumeroOrden,
                VehiculoTipoMedidor = o.Vehiculo.TipoMedidor,
                VehiculoTipoUnidad  = o.Vehiculo.TipoUnidad
            })
            .ToListAsync();

        return filas.Select(o => new OrdenServicioReporteResponse(
            o.Id,
            o.VehiculoId,
            o.VehiculoPlaca,
            o.VehiculoMarca,
            o.VehiculoModelo,
            o.ClienteId,
            o.ClienteNombre,
            o.TecnicoAsignadoId,
            o.TecnicoNombre,
            o.Estado.ToString(),
            (int)o.Estado,
            o.FechaApertura,
            o.FechaCierre,
            // Horas de atención: solo cuando FechaCierre existe (OS cerrada)
            o.FechaCierre.HasValue
                ? Math.Round((o.FechaCierre.Value - o.FechaApertura).TotalHours, 1)
                : null,
            o.NumeroOrden,
            (o.VehiculoTipoMedidor == TipoMedidor.Horas || o.VehiculoTipoUnidad == TipoUnidad.MotoAcuatica || o.VehiculoTipoUnidad == TipoUnidad.Generador) ? "Horas" : "Km"))
        .ToList();
    }

    // ------------------------------------------------------------------
    // REPORTE 2: Ventas / Cotizaciones
    // ------------------------------------------------------------------
    public async Task<List<VentaReporteResponse>> GetVentasAsync(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        EstadoVenta? estado,
        Guid? clienteId)
    {
        var query = _context.Ventas.Where(v => v.Activo);

        if (fechaDesde.HasValue)
        {
            var desde = DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc);
            query = query.Where(v => v.Fecha >= desde);
        }

        if (fechaHasta.HasValue)
        {
            // Límite superior exclusivo: inicio del día siguiente para incluir todo fechaHasta.
            var hastaExclusivo = DateTime.SpecifyKind(fechaHasta.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(v => v.Fecha < hastaExclusivo);
        }

        if (estado.HasValue)
            query = query.Where(v => v.Estado == estado.Value);

        if (clienteId.HasValue)
            query = query.Where(v => v.ClienteId == clienteId.Value);

        // Proyección SQL única — sin N+1
        return await query
            .OrderByDescending(v => v.Fecha)
            .Select(v => new VentaReporteResponse(
                v.Id,
                v.ClienteId,
                v.Cliente.NombreCompleto,
                v.OrdenServicioId,
                v.Estado.ToString(),
                (int)v.Estado,
                v.Fecha,
                v.Total))
            .ToListAsync();
    }

    // ------------------------------------------------------------------
    // REPORTE 3: Stock Bajo
    // ------------------------------------------------------------------
    public async Task<List<StockBajoResponse>> GetStockBajoAsync()
    {
        return await _context.Productos
            .Where(p => p.Activo && p.StockActual <= (p.StockMinimo ?? (p.Categoria.StockMinimoDefault ?? 4)))
            .OrderBy(p => p.StockActual - (p.StockMinimo ?? (p.Categoria.StockMinimoDefault ?? 4))) // los más críticos primero
            .Select(p => new StockBajoResponse(
                p.Id,
                p.Codigo,
                p.Nombre,
                p.CategoriaId,
                p.Categoria.Nombre,
                p.StockActual,
                p.StockMinimo ?? (p.Categoria.StockMinimoDefault ?? 4),
                (p.StockMinimo ?? (p.Categoria.StockMinimoDefault ?? 4)) - p.StockActual, // Diferencia: cuánto falta para llegar al mínimo
                p.PrecioVenta))
            .ToListAsync();
    }

    // ------------------------------------------------------------------
    // REPORTE 4: Rentabilidad y Costos Históricos (D9)
    // ------------------------------------------------------------------
    public async Task<RentabilidadReporteResponse> GetRentabilidadAsync(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        Guid? productoId)
    {
        var query = _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Comprobante)
            .Include(v => v.OrdenServicio)
            .Include(v => v.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Producto)
            .Include(v => v.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Servicio)
            .Where(v => v.Activo && v.Estado == EstadoVenta.Confirmada);

        if (fechaDesde.HasValue)
        {
            var desde = DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc);
            query = query.Where(v => v.Fecha >= desde);
        }

        if (fechaHasta.HasValue)
        {
            var hastaExclusivo = DateTime.SpecifyKind(fechaHasta.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(v => v.Fecha < hastaExclusivo);
        }

        var ventas = await query.OrderByDescending(v => v.Fecha).ToListAsync();

        if (productoId.HasValue)
        {
            ventas = ventas.Where(v => v.Detalles.Any(d => d.ProductoId == productoId.Value)).ToList();
        }

        var todasLineas = ventas
            .SelectMany(v => v.Detalles
                .Where(d => !productoId.HasValue || d.ProductoId == productoId.Value)
                .Select(d => new
                {
                    Venta = v,
                    Detalle = d,
                    IngresoNeto = Math.Round(d.Cantidad * d.PrecioUnitario, 2),
                    CostoHistorico = Math.Round(d.Cantidad * d.CostoUnitarioHistorico, 2)
                }))
            .ToList();

        // 1. Resumen Global
        var ingresosTotalesSinIgv = Math.Round(todasLineas.Sum(l => l.IngresoNeto), 2);
        var costoTotalHistorico = Math.Round(todasLineas.Sum(l => l.CostoHistorico), 2);
        var utilidadBrutaTotal = Math.Round(ingresosTotalesSinIgv - costoTotalHistorico, 2);
        var margenGlobal = ingresosTotalesSinIgv > 0
            ? Math.Round((utilidadBrutaTotal / ingresosTotalesSinIgv) * 100m, 2)
            : 0m;

        var resumen = new RentabilidadResumenResponse(
            ingresosTotalesSinIgv,
            costoTotalHistorico,
            utilidadBrutaTotal,
            margenGlobal);

        // 2. Desglose por Tipo de Ítem (Repuesto, Servicio, ManoDeObra, Terceros)
        var tiposEnum = new[]
        {
            TipoItemServicio.Repuesto,
            TipoItemServicio.Servicio,
            TipoItemServicio.ManoDeObra,
            TipoItemServicio.Terceros
        };

        var lineasPorTipo = todasLineas.GroupBy(l => l.Detalle.TipoItem).ToDictionary(g => g.Key, g => g.ToList());

        var desglosePorTipo = tiposEnum.Select(tipo =>
        {
            if (lineasPorTipo.TryGetValue(tipo, out var lineas))
            {
                var ing = Math.Round(lineas.Sum(l => l.IngresoNeto), 2);
                var cos = Math.Round(lineas.Sum(l => l.CostoHistorico), 2);
                var uti = Math.Round(ing - cos, 2);
                var mar = ing > 0 ? Math.Round((uti / ing) * 100m, 2) : 0m;
                return new RentabilidadPorTipoItemResponse(
                    tipo.ToString(),
                    lineas.Sum(l => l.Detalle.Cantidad),
                    ing,
                    cos,
                    uti,
                    mar);
            }

            return new RentabilidadPorTipoItemResponse(tipo.ToString(), 0, 0m, 0m, 0m, 0m);
        }).ToList();

        // 3. Detalle por Operación (Ventas / OS)
        var detalleOperaciones = ventas.Select(v =>
        {
            var lineasVenta = todasLineas.Where(l => l.Venta.Id == v.Id).ToList();
            var ing = Math.Round(lineasVenta.Sum(l => l.IngresoNeto), 2);
            var cos = Math.Round(lineasVenta.Sum(l => l.CostoHistorico), 2);
            var uti = Math.Round(ing - cos, 2);
            var mar = ing > 0 ? Math.Round((uti / ing) * 100m, 2) : 0m;

            string docTipo = v.OrdenServicioId.HasValue ? "OrdenServicio" : "Venta";
            string docNum = v.Comprobante != null
                ? $"{v.Comprobante.Serie}-{v.Comprobante.Numero}"
                : (v.OrdenServicio?.NumeroOrden ?? $"VTA-{v.Id.ToString()[..8].ToUpperInvariant()}");

            return new RentabilidadOperacionDetalleResponse(
                docTipo,
                docNum,
                v.Id,
                v.Fecha,
                v.Cliente.NombreCompleto,
                ing,
                cos,
                uti,
                mar);
        }).Where(op => op.IngresoNeto > 0 || op.CostoHistoricoRegistrado > 0).ToList();

        // 4. Ranking de Repuestos
        var repuestosLineas = todasLineas
            .Where(l => l.Detalle.TipoItem == TipoItemServicio.Repuesto && l.Detalle.ProductoId.HasValue)
            .GroupBy(l => l.Detalle.ProductoId!.Value);

        var rankingRepuestos = repuestosLineas.Select(g =>
        {
            var primera = g.First();
            var ing = Math.Round(g.Sum(l => l.IngresoNeto), 2);
            var cos = Math.Round(g.Sum(l => l.CostoHistorico), 2);
            var uti = Math.Round(ing - cos, 2);
            var mar = ing > 0 ? Math.Round((uti / ing) * 100m, 2) : 0m;

            return new RentabilidadRepuestoRankingResponse(
                g.Key,
                primera.Detalle.Producto?.Codigo ?? string.Empty,
                primera.Detalle.Producto?.Nombre ?? primera.Detalle.Servicio?.Nombre ?? "Repuesto",
                g.Sum(l => l.Detalle.Cantidad),
                ing,
                cos,
                uti,
                mar);
        })
        .OrderByDescending(r => r.UtilidadBruta)
        .ToList();

        return new RentabilidadReporteResponse(
            fechaDesde,
            fechaHasta,
            resumen,
            desglosePorTipo,
            detalleOperaciones,
            rankingRepuestos);
    }

    // ------------------------------------------------------------------
    // EXPORTACIÓN EXCEL: Ventas
    // ------------------------------------------------------------------
    public async Task<byte[]> ExportarVentasExcelAsync(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        EstadoVenta? estado,
        Guid? clienteId,
        bool incluirFinanciero)
    {
        var query = _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Pagos.Where(p => p.Activo))
            .Include(v => v.Comprobante)
            .Include(v => v.Detalles.Where(d => d.Activo))
            .Where(v => v.Activo);

        if (fechaDesde.HasValue)
        {
            var desde = DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc);
            query = query.Where(v => v.Fecha >= desde);
        }

        if (fechaHasta.HasValue)
        {
            var hastaExclusivo = DateTime.SpecifyKind(fechaHasta.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(v => v.Fecha < hastaExclusivo);
        }

        if (estado.HasValue)
            query = query.Where(v => v.Estado == estado.Value);

        if (clienteId.HasValue)
            query = query.Where(v => v.ClienteId == clienteId.Value);

        var ventas = await query.OrderByDescending(v => v.Fecha).ToListAsync();

        var dtos = ventas.Select(v =>
        {
            var totalPagado = v.Pagos.Sum(p => p.Monto);
            var saldo = Math.Max(0m, v.Total - totalPagado);
            var estadoPago = saldo <= 0 ? "Pagado" : (totalPagado > 0 ? "Parcial" : "Pendiente");
            var estadoComprobante = v.Comprobante != null ? "Emitido" : "Sin comprobante";

            decimal? costoTotal = null;
            decimal? utilidad = null;
            decimal? margen = null;

            if (incluirFinanciero)
            {
                var ingresoSinIgv = v.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
                costoTotal = v.Detalles.Sum(d => d.Cantidad * d.CostoUnitarioHistorico);
                utilidad = ingresoSinIgv - costoTotal.Value;
                margen = ingresoSinIgv > 0 ? Math.Round((utilidad.Value / ingresoSinIgv) * 100m, 2) : 0m;
            }

            return new VentaExcelDto(
                Id: v.Id,
                Fecha: v.Fecha,
                ClienteNombre: v.Cliente?.NombreCompleto ?? "N/A",
                Estado: v.Estado.ToString(),
                EstadoPago: estadoPago,
                EstadoComprobante: estadoComprobante,
                CantidadItems: v.Detalles.Sum(d => d.Cantidad),
                Total: v.Total,
                TotalPagado: totalPagado,
                Saldo: saldo,
                CostoTotal: costoTotal,
                Utilidad: utilidad,
                MargenPorcentaje: margen
            );
        }).ToList();

        return _excelService.GenerarExcelVentas(dtos, incluirFinanciero);
    }

    // ------------------------------------------------------------------
    // EXPORTACIÓN EXCEL: Órdenes de Servicio
    // ------------------------------------------------------------------
    public async Task<byte[]> ExportarOrdenesServicioExcelAsync(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        EstadoOrdenServicio? estado,
        Guid? tecnicoId,
        bool incluirFinanciero)
    {
        var query = _context.OrdenesServicio
            .Include(o => o.Vehiculo)
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoAsignado)
            .Include(o => o.Detalles.Where(d => d.Activo))
            .Where(o => o.Activo);

        if (fechaDesde.HasValue)
        {
            var desde = DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc);
            query = query.Where(o => o.FechaApertura >= desde);
        }

        if (fechaHasta.HasValue)
        {
            var hastaExclusivo = DateTime.SpecifyKind(fechaHasta.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(o => o.FechaApertura < hastaExclusivo);
        }

        if (estado.HasValue)
            query = query.Where(o => o.Estado == estado.Value);

        if (tecnicoId.HasValue)
            query = query.Where(o => o.TecnicoAsignadoId == tecnicoId.Value);

        var ordenes = await query.OrderByDescending(o => o.FechaApertura).ToListAsync();

        var dtos = ordenes.Select(o =>
        {
            decimal? costoTotal = null;
            decimal? utilidad = null;
            decimal? margen = null;

            if (incluirFinanciero)
            {
                var ingresoSinIgv = o.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
                costoTotal = o.Detalles.Sum(d => d.Cantidad * d.CostoUnitarioHistorico);
                utilidad = ingresoSinIgv - costoTotal.Value;
                margen = ingresoSinIgv > 0 ? Math.Round((utilidad.Value / ingresoSinIgv) * 100m, 2) : 0m;
            }

            return new OrdenServicioExcelDto(
                Id: o.Id,
                NumeroOrden: o.NumeroOrden,
                FechaIngreso: o.FechaApertura,
                FechaSalida: o.FechaCierre,
                ClienteNombre: o.Cliente?.NombreCompleto ?? o.Vehiculo?.Cliente?.NombreCompleto ?? "N/A",
                Placa: o.Vehiculo?.Placa,
                Modelo: o.Vehiculo?.Modelo,
                Estado: o.Estado.ToString(),
                TecnicoNombre: o.TecnicoAsignado?.NombreCompleto,
                Total: o.Total,
                CostoTotal: costoTotal,
                Utilidad: utilidad,
                MargenPorcentaje: margen
            );
        }).ToList();

        return _excelService.GenerarExcelOrdenesServicio(dtos, incluirFinanciero);
    }
}
