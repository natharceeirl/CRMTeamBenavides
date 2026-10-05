using CRMTeamBenavides.Api.Features.Portal;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class PortalService : IPortalService
{
    private readonly ApplicationDbContext _context;

    public PortalService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<PortalResumenResponse>> GetResumenAsync(Guid clienteId)
    {
        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clienteId && c.Activo);

        if (cliente is null)
        {
            return ServiceResult<PortalResumenResponse>.NotFound();
        }

        var cantidadUnidades = await _context.Vehiculos
            .CountAsync(v => v.ClienteId == clienteId && v.Activo);

        var cantidadOrdenesActivas = await _context.OrdenesServicio
            .CountAsync(o => (o.ClienteId == clienteId || o.Vehiculo.ClienteId == clienteId)
                          && o.Activo
                          && o.Estado != EstadoOrdenServicio.Entregada
                          && o.Estado != EstadoOrdenServicio.Cancelada);

        // Por responder: el presupuesto ya existe (diagnóstico hecho) y sigue sin respuesta.
        // Una orden que avanzó de Diagnóstico ya no espera nada del cliente.
        var cantidadPresupuestosPendientes = await _context.OrdenesServicio
            .CountAsync(o => (o.ClienteId == clienteId || o.Vehiculo.ClienteId == clienteId)
                          && o.Activo
                          && o.Estado == EstadoOrdenServicio.Diagnostico
                          && o.EstadoPresupuestoCliente == EstadoPresupuestoCliente.Pendiente);

        // El saldo es lo que el cliente ya debe: el trabajo comprometido (presupuesto
        // aprobado, o la orden ya pasó de Diagnóstico) y las ventas confirmadas. Un
        // presupuesto que todavía no responde no es deuda.
        var ordenes = await _context.OrdenesServicio
            .Include(o => o.Pagos)
            .Where(o => (o.ClienteId == clienteId || o.Vehiculo.ClienteId == clienteId)
                        && o.Activo
                        && o.Estado != EstadoOrdenServicio.Cancelada
                        && (o.EstadoPresupuestoCliente == EstadoPresupuestoCliente.Aprobado
                            || (o.Estado != EstadoOrdenServicio.Abierta && o.Estado != EstadoOrdenServicio.Diagnostico)))
            .ToListAsync();

        decimal saldoOrdenes = 0m;
        foreach (var o in ordenes)
        {
            var pagado = o.Pagos.Where(p => p.Activo).Sum(p => p.Monto);
            var saldo = Math.Max(0m, o.Total - pagado);
            saldoOrdenes += saldo;
        }

        var ventasDirectas = await _context.Ventas
            .Include(v => v.Pagos)
            .Where(v => v.ClienteId == clienteId
                        && v.OrdenServicioId == null
                        && v.Activo
                        && v.Estado != EstadoVenta.Anulada)
            .ToListAsync();

        decimal saldoVentas = 0m;
        foreach (var v in ventasDirectas)
        {
            var pagado = v.Pagos.Where(p => p.Activo).Sum(p => p.Monto);
            var saldo = Math.Max(0m, v.Total - pagado);
            saldoVentas += saldo;
        }

        var saldoPendienteTotal = saldoOrdenes + saldoVentas;

        var resumen = new PortalResumenResponse(
            ClienteId: cliente.Id,
            ClienteNombre: cliente.NombreCompleto,
            CantidadUnidades: cantidadUnidades,
            CantidadOrdenesActivas: cantidadOrdenesActivas,
            CantidadPresupuestosPendientes: cantidadPresupuestosPendientes,
            SaldoPendienteTotal: saldoPendienteTotal);

        return ServiceResult<PortalResumenResponse>.Success(resumen);
    }

    public async Task<ServiceResult<List<PortalComprobanteResponse>>> GetComprobantesAsync(Guid clienteId)
    {
        var comprobantes = await _context.Comprobantes
            .Include(c => c.Venta)
            .Include(c => c.OrdenServicio)
            .Where(c => c.Activo && c.Venta != null && c.Venta.ClienteId == clienteId && c.Venta.Activo)
            .OrderByDescending(c => c.FechaCreacion)
            .ToListAsync();

        var responses = comprobantes.Select(c => new PortalComprobanteResponse(
            Id: c.Id,
            VentaId: c.VentaId,
            OrdenServicioId: c.OrdenServicioId ?? c.Venta?.OrdenServicioId,
            NumeroOrden: c.OrdenServicio?.NumeroOrden,
            Tipo: c.Tipo.ToString(),
            Serie: c.Serie,
            Numero: c.Numero,
            Fecha: c.FechaCreacion,
            SubtotalGravado: c.SubtotalGravado,
            SubtotalExonerado: c.SubtotalExonerado,
            SubtotalInafecto: c.SubtotalInafecto,
            PorcentajeIgv: c.PorcentajeIgv,
            MontoIgv: c.MontoIgv,
            Total: c.Total,
            Estado: c.Estado.ToString(),
            MetodoPagoPrincipal: c.MetodoPagoPrincipal,
            Observaciones: c.Observaciones)).ToList();

        return ServiceResult<List<PortalComprobanteResponse>>.Success(responses);
    }

    public async Task<ServiceResult<List<AtencionServicioUnidadResponse>>> GetHistorialServicioUnidadAsync(
        Guid vehiculoId, Guid? soloClienteId = null)
    {
        var vehiculo = await _context.Vehiculos
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == vehiculoId && v.Activo);

        if (vehiculo is null)
        {
            return ServiceResult<List<AtencionServicioUnidadResponse>>.NotFound();
        }

        if (soloClienteId.HasValue && vehiculo.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<List<AtencionServicioUnidadResponse>>.NotFound();
        }

        var ordenes = await _context.OrdenesServicio
            .Include(o => o.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Servicio)
            .Include(o => o.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Producto)
            .Where(o => o.VehiculoId == vehiculoId && o.Activo && o.Estado != EstadoOrdenServicio.Cancelada)
            .OrderByDescending(o => o.FechaIngreso)
            .ToListAsync();

        var resultado = ordenes.Select(o =>
        {
            var items = o.Detalles.Select(d => new HistorialServicioItemResponse(
                Id: d.Id,
                Descripcion: d.Descripcion,
                ServicioNombre: d.Servicio?.Nombre,
                ProductoCodigo: d.Producto?.Codigo,
                TipoItem: d.TipoItem,
                TipoItemNombre: d.TipoItem.ToString(),
                Cantidad: d.Cantidad,
                PrecioUnitario: d.PrecioUnitario,
                Total: d.Total)).ToList();

            return new AtencionServicioUnidadResponse(
                OrdenServicioId: o.Id,
                NumeroOrden: o.NumeroOrden,
                FechaIngreso: o.FechaIngreso,
                FechaSalida: o.FechaSalida ?? o.FechaCierre,
                Estado: o.Estado.ToString(),
                EstadoId: (int)o.Estado,
                KilometrajeIngreso: o.KilometrajeIngreso,
                HorasUsoIngreso: o.HorasUsoIngreso,
                LecturaMedidorIngreso: o.LecturaMedidorIngreso,
                TipoMedidor: vehiculo.TipoMedidor.ToString(),
                MotivoFalla: o.MotivoFalla,
                Solucion: o.Solucion,
                ObservacionesCliente: o.Observaciones,
                Total: o.Total,
                Items: items);
        }).ToList();

        return ServiceResult<List<AtencionServicioUnidadResponse>>.Success(resultado);
    }
}
