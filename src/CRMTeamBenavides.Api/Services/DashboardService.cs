using CRMTeamBenavides.Api.Features.Dashboard;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardResumenResponse> GetResumenAsync(DateTime? fechaDesde, DateTime? fechaHasta)
    {
        // Normalizar fechas a UTC para PostgreSQL.
        // fechaDesde: inclusiva desde el inicio exacto del día proporcionado.
        // fechaHasta: inclusiva para todo el día — se convierte al inicio del día SIGUIENTE
        //             y se usa < (exclusivo) en los filtros. Así, "fechaHasta=2026-09-17"
        //             incluye todos los registros hasta 2026-09-17T23:59:59.999Z.
        var desdeUtc = fechaDesde.HasValue
            ? DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc)
            : (DateTime?)null;

        // Límite superior exclusivo: inicio del día siguiente al fechaHasta proporcionado.
        var hastaExclusivo = fechaHasta.HasValue
            ? DateTime.SpecifyKind(fechaHasta.Value.Date.AddDays(1), DateTimeKind.Utc)
            : (DateTime?)null;

        // ---------------------------------------------------------------
        // 1. Órdenes de Servicio — agregaciones independientes por estado
        //    Aplicamos filtro de período solo si se proporcionan fechas.
        //    Sin filtro de fecha: cuenta el estado actual del taller completo.
        // ---------------------------------------------------------------
        var osQuery = _context.OrdenesServicio.Where(o => o.Activo);

        if (desdeUtc.HasValue)
            osQuery = osQuery.Where(o => o.FechaApertura >= desdeUtc.Value);
        if (hastaExclusivo.HasValue)
            osQuery = osQuery.Where(o => o.FechaApertura < hastaExclusivo.Value);

        // Una sola consulta SQL que agrupa por estado: eficiente, sin N+1
        var osConteosPorEstado = await osQuery
            .GroupBy(o => o.Estado)
            .Select(g => new { Estado = g.Key, Conteo = g.Count() })
            .ToListAsync();

        int GetOsConteo(EstadoOrdenServicio estado) =>
            osConteosPorEstado.FirstOrDefault(x => x.Estado == estado)?.Conteo ?? 0;

        var ordenesResumen = new OrdenesServicioResumenResponse(
            Abierta:     GetOsConteo(EstadoOrdenServicio.Abierta),
            Diagnostico: GetOsConteo(EstadoOrdenServicio.Diagnostico),
            Aprobada:    GetOsConteo(EstadoOrdenServicio.Aprobada),
            EnProceso:   GetOsConteo(EstadoOrdenServicio.EnProceso),
            Lista:       GetOsConteo(EstadoOrdenServicio.Lista),
            Entregada:   GetOsConteo(EstadoOrdenServicio.Entregada),
            Cancelada:   GetOsConteo(EstadoOrdenServicio.Cancelada),
            Total:       osConteosPorEstado.Sum(x => x.Conteo));

        // ---------------------------------------------------------------
        // 2. Ventas — conteos y montos por estado
        // ---------------------------------------------------------------
        var ventasQuery = _context.Ventas.Where(v => v.Activo);

        if (desdeUtc.HasValue)
            ventasQuery = ventasQuery.Where(v => v.Fecha >= desdeUtc.Value);
        if (hastaExclusivo.HasValue)
            ventasQuery = ventasQuery.Where(v => v.Fecha < hastaExclusivo.Value);

        // Una sola consulta SQL que agrupa por estado
        var ventasStats = await ventasQuery
            .GroupBy(v => v.Estado)
            .Select(g => new
            {
                Estado = g.Key,
                Conteo = g.Count(),
                Monto  = g.Sum(v => v.Total)
            })
            .ToListAsync();

        var confirmadas     = ventasStats.FirstOrDefault(x => x.Estado == EstadoVenta.Confirmada);
        var montoConfirmado = confirmadas?.Monto ?? 0m;
        var countConfirmado = confirmadas?.Conteo ?? 0;

        var ventasResumen = new VentasResumenResponse(
            Confirmadas:    countConfirmado,
            Cotizaciones:   ventasStats.FirstOrDefault(x => x.Estado == EstadoVenta.Cotizacion)?.Conteo ?? 0,
            Anuladas:       ventasStats.FirstOrDefault(x => x.Estado == EstadoVenta.Anulada)?.Conteo ?? 0,
            MontoConfirmadas: montoConfirmado,
            TicketPromedio: countConfirmado > 0
                ? Math.Round(montoConfirmado / countConfirmado, 2)
                : 0m);

        // ---------------------------------------------------------------
        // 3. Inventario — consultas agregadas directas, sin filtro de fecha
        //    (el stock es estado actual, no histórico en el período)
        // ---------------------------------------------------------------
        var inventarioStats = await _context.Productos
            .Where(p => p.Activo)
            .GroupBy(_ => 1) // agregación total en una sola consulta SQL
            .Select(g => new
            {
                ConStockBajo = g.Count(p => p.StockActual <= p.StockMinimo),
                SinStock     = g.Count(p => p.StockActual == 0),
                ValorTotal   = g.Sum(p => (decimal)p.StockActual * p.PrecioVenta)
            })
            .FirstOrDefaultAsync();

        var inventarioResumen = new InventarioResumenResponse(
            ProductosConStockBajo:      inventarioStats?.ConStockBajo     ?? 0,
            ProductosSinStock:          inventarioStats?.SinStock         ?? 0,
            ValorEstimadoInventario:    inventarioStats?.ValorTotal        ?? 0m);

        // ---------------------------------------------------------------
        // 4. CRM — clientes y vehículos activos (estado siempre actual)
        // ---------------------------------------------------------------
        var clientesActivos  = await _context.Clientes.CountAsync(c => c.Activo);
        var vehiculosActivos = await _context.Vehiculos.CountAsync(v => v.Activo);

        var crmResumen = new ClientesVehiculosResumenResponse(
            ClientesActivos:  clientesActivos,
            VehiculosActivos: vehiculosActivos);

        return new DashboardResumenResponse(
            FechaDesde:      fechaDesde,
            FechaHasta:      fechaHasta,
            OrdenesServicio: ordenesResumen,
            Ventas:          ventasResumen,
            Inventario:      inventarioResumen,
            CrmActivos:      crmResumen);
    }
}
