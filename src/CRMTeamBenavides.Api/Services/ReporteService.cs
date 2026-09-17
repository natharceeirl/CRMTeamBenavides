using CRMTeamBenavides.Api.Features.Reportes;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class ReporteService : IReporteService
{
    private readonly ApplicationDbContext _context;

    public ReporteService(ApplicationDbContext context)
    {
        _context = context;
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
                o.FechaCierre
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
                : null))
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
            .Where(p => p.Activo && p.StockActual <= p.StockMinimo)
            .OrderBy(p => p.StockActual - p.StockMinimo) // los más críticos primero
            .Select(p => new StockBajoResponse(
                p.Id,
                p.Codigo,
                p.Nombre,
                p.CategoriaId,
                p.Categoria.Nombre,
                p.StockActual,
                p.StockMinimo,
                p.StockMinimo - p.StockActual, // Diferencia: cuánto falta para llegar al mínimo
                p.PrecioVenta))
            .ToListAsync();
    }
}
