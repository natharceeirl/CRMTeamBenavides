using CRMTeamBenavides.Api.Features.Ventas;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class PagoService : IPagoService
{
    private const string MensajeEfectivoSinCaja =
        "No se puede registrar un cobro en efectivo porque no hay ninguna Caja Chica abierta en este momento.";

    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public PagoService(ApplicationDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<MetodoPagoResponse>> GetMetodosPagoAsync()
    {
        return await _context.MetodosPago
            .Where(m => m.Activo)
            .OrderBy(m => m.Nombre)
            .Select(m => new MetodoPagoResponse(m.Id, m.Codigo, m.Nombre, m.Activo))
            .ToListAsync();
    }

    public async Task<ServiceResult<PagoResponse>> RegistrarPagoVentaAsync(
        Guid ventaId, RegistrarPagoRequest request, Guid? usuarioId)
    {
        if (request.Monto <= 0)
        {
            return ServiceResult<PagoResponse>.Invalid("El monto del pago debe ser mayor a 0.");
        }

        var metodo = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.Id == request.MetodoPagoId && m.Activo);

        if (metodo is null)
        {
            return ServiceResult<PagoResponse>.Invalid("El método de pago indicado no existe o está inactivo.");
        }

        if (CajaChicaService.EsCodigoEfectivo(metodo.Codigo))
        {
            var cajaAbierta = await _context.CajasChicas
                .AnyAsync(c => c.Estado == EstadoCajaChica.Abierta);
            if (!cajaAbierta)
            {
                return ServiceResult<PagoResponse>.Invalid(MensajeEfectivoSinCaja);
            }
        }

        Pago pago;
        await using var transaccion = await _context.Database.BeginTransactionAsync();
        try
        {
            // Bloqueo pesimista a nivel de fila sobre la venta para serializar operaciones concurrentes
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"Ventas\" WHERE \"Id\" = {ventaId} FOR UPDATE");

            var venta = await _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.OrdenServicio)
                .Include(v => v.Comprobante)
                .FirstOrDefaultAsync(v => v.Id == ventaId && v.Activo);

            if (venta is null)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.NotFound();
            }

            if (venta.Estado != EstadoVenta.Confirmada)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("Solo se pueden registrar pagos en ventas en estado Confirmada.");
            }

            if (venta.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Pendiente)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("No se pueden registrar pagos en una venta con aprobación de Gerencia pendiente.");
            }

            if (venta.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Rechazado)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("No se pueden registrar pagos en una venta con aprobación de Gerencia rechazada.");
            }

            var totalPagado = await _context.Pagos
                .Where(p => p.VentaId == venta.Id && p.Activo)
                .SumAsync(p => (decimal?)p.Monto) ?? 0m;
            var saldo = Math.Max(0m, venta.Total - totalPagado);

            if (saldo <= 0)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("La venta ya se encuentra pagada en su totalidad.");
            }

            if (request.Monto > saldo)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid(
                    $"El monto del pago (S/ {request.Monto:F2}) no puede exceder el saldo pendiente (S/ {saldo:F2}).");
            }

            pago = new Pago
            {
                Monto           = request.Monto,
                MetodoPagoId    = request.MetodoPagoId,
                MetodoPago      = metodo,
                Fecha           = DateTime.UtcNow,
                Referencia      = string.IsNullOrWhiteSpace(request.Referencia) ? null : request.Referencia.Trim(),
                VentaId         = venta.Id,
                OrdenServicioId = venta.OrdenServicioId,
                EsAnticipo      = request.EsAnticipo,
                UsuarioId       = usuarioId,
                Observaciones   = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
                Activo          = true,
                FechaCreacion   = DateTime.UtcNow
            };

            _context.Pagos.Add(pago);

            string refVenta;
            if (venta.OrdenServicio != null && !string.IsNullOrWhiteSpace(venta.OrdenServicio.NumeroOrden))
            {
                refVenta = $"OS #{venta.OrdenServicio.NumeroOrden}";
            }
            else if (venta.Comprobante != null && !string.IsNullOrWhiteSpace(venta.Comprobante.Numero))
            {
                refVenta = $"Comprobante {venta.Comprobante.Serie}-{venta.Comprobante.Numero}";
            }
            else if (venta.Cliente != null)
            {
                refVenta = $"Venta mostrador ({venta.Cliente.NombreCompleto})";
            }
            else
            {
                refVenta = "Venta mostrador";
            }

            if (!await RegistrarMovimientoCajaChicaAutomaticoAsync(pago, metodo, refVenta, usuarioId))
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid(MensajeEfectivoSinCaja);
            }

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }

        await _auditoriaService.RegistrarEventoAsync(
            usuarioId,
            "Crear",
            "Pago",
            pago.Id.ToString(),
            new
            {
                pago.Monto,
                pago.MetodoPagoId,
                MetodoPago = metodo.Nombre,
                pago.VentaId,
                pago.EsAnticipo,
                pago.Referencia
            });

        string? usuarioNombre = null;
        if (usuarioId.HasValue)
        {
            usuarioNombre = await _context.Usuarios
                .Where(u => u.Id == usuarioId.Value)
                .Select(u => u.NombreCompleto)
                .FirstOrDefaultAsync();
        }

        return ServiceResult<PagoResponse>.Success(new PagoResponse(
            pago.Id,
            pago.Monto,
            pago.MetodoPagoId,
            metodo.Nombre,
            metodo.Codigo,
            pago.Fecha,
            pago.Referencia,
            pago.EsAnticipo,
            pago.VentaId,
            pago.OrdenServicioId,
            pago.UsuarioId,
            usuarioNombre,
            pago.Observaciones,
            pago.Activo));
    }

    public async Task<ServiceResult<PagoResponse>> RegistrarPagoOrdenServicioAsync(
        Guid ordenServicioId, RegistrarPagoRequest request, Guid? usuarioId)
    {
        if (request.Monto <= 0)
        {
            return ServiceResult<PagoResponse>.Invalid("El monto del pago debe ser mayor a 0.");
        }

        var metodo = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.Id == request.MetodoPagoId && m.Activo);

        if (metodo is null)
        {
            return ServiceResult<PagoResponse>.Invalid("El método de pago indicado no existe o está inactivo.");
        }

        if (CajaChicaService.EsCodigoEfectivo(metodo.Codigo))
        {
            var cajaAbierta = await _context.CajasChicas
                .AnyAsync(c => c.Estado == EstadoCajaChica.Abierta);
            if (!cajaAbierta)
            {
                return ServiceResult<PagoResponse>.Invalid(MensajeEfectivoSinCaja);
            }
        }

        Pago pago;
        await using var transaccion = await _context.Database.BeginTransactionAsync();
        try
        {
            // Bloqueo pesimista a nivel de fila sobre la orden de servicio para serializar operaciones concurrentes
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"OrdenesServicio\" WHERE \"Id\" = {ordenServicioId} FOR UPDATE");

            var orden = await _context.OrdenesServicio
                .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo);

            if (orden is null)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.NotFound();
            }

            if (orden.Estado == EstadoOrdenServicio.Cancelada)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("No se pueden registrar pagos en una orden de servicio cancelada.");
            }

            // Un adelanto después de liquidar no llegaría al saldo de la venta.
            var liquidada = await _context.Ventas.AnyAsync(v =>
                v.OrdenServicioId == ordenServicioId && v.Activo && v.Estado != EstadoVenta.Anulada);
            if (liquidada)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("La orden ya está liquidada: registra el pago en su venta.");
            }

            if (orden.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Pendiente)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("No se pueden registrar pagos en una orden de servicio con aprobación de Gerencia pendiente.");
            }

            if (orden.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Rechazado)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("No se pueden registrar pagos en una orden de servicio con aprobación de Gerencia rechazada.");
            }

            var totalPagado = await _context.Pagos
                .Where(p => p.OrdenServicioId == orden.Id && p.Activo)
                .SumAsync(p => (decimal?)p.Monto) ?? 0m;
            var saldo = Math.Max(0m, orden.Total - totalPagado);

            if (saldo <= 0)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("La orden de servicio ya se encuentra pagada en su totalidad.");
            }

            if (request.Monto > saldo)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid(
                    $"El monto del pago (S/ {request.Monto:F2}) no puede exceder el saldo pendiente (S/ {saldo:F2}).");
            }

            pago = new Pago
            {
                Monto           = request.Monto,
                MetodoPagoId    = request.MetodoPagoId,
                MetodoPago      = metodo,
                Fecha           = DateTime.UtcNow,
                Referencia      = string.IsNullOrWhiteSpace(request.Referencia) ? null : request.Referencia.Trim(),
                OrdenServicioId = orden.Id,
                EsAnticipo      = request.EsAnticipo,
                UsuarioId       = usuarioId,
                Observaciones   = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
                Activo          = true,
                FechaCreacion   = DateTime.UtcNow
            };

            _context.Pagos.Add(pago);
            var refOrden = !string.IsNullOrWhiteSpace(orden.NumeroOrden) ? $"OS #{orden.NumeroOrden}" : "OS taller";

            if (!await RegistrarMovimientoCajaChicaAutomaticoAsync(pago, metodo, refOrden, usuarioId))
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid(MensajeEfectivoSinCaja);
            }

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }

        await _auditoriaService.RegistrarEventoAsync(
            usuarioId,
            "Crear",
            "Pago",
            pago.Id.ToString(),
            new
            {
                pago.Monto,
                pago.MetodoPagoId,
                MetodoPago = metodo.Nombre,
                pago.OrdenServicioId,
                pago.EsAnticipo,
                pago.Referencia
            });

        string? usuarioNombre = null;
        if (usuarioId.HasValue)
        {
            usuarioNombre = await _context.Usuarios
                .Where(u => u.Id == usuarioId.Value)
                .Select(u => u.NombreCompleto)
                .FirstOrDefaultAsync();
        }

        return ServiceResult<PagoResponse>.Success(new PagoResponse(
            pago.Id,
            pago.Monto,
            pago.MetodoPagoId,
            metodo.Nombre,
            metodo.Codigo,
            pago.Fecha,
            pago.Referencia,
            pago.EsAnticipo,
            pago.VentaId,
            pago.OrdenServicioId,
            pago.UsuarioId,
            usuarioNombre,
            pago.Observaciones,
            pago.Activo));
    }

    public async Task<ServiceResult<List<PagoResponse>>> GetPagosByVentaIdAsync(Guid ventaId, Guid? soloClienteId = null)
    {
        var venta = await _context.Ventas
            .FirstOrDefaultAsync(v => v.Id == ventaId && v.Activo);

        if (venta is null)
        {
            return ServiceResult<List<PagoResponse>>.NotFound();
        }

        if (soloClienteId.HasValue && venta.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<List<PagoResponse>>.NotFound();
        }

        var pagos = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Include(p => p.Usuario)
            .Where(p => p.VentaId == ventaId && p.Activo)
            .OrderBy(p => p.Fecha)
            .Select(p => new PagoResponse(
                p.Id,
                p.Monto,
                p.MetodoPagoId,
                p.MetodoPago.Nombre,
                p.MetodoPago.Codigo,
                p.Fecha,
                p.Referencia,
                p.EsAnticipo,
                p.VentaId,
                p.OrdenServicioId,
                p.UsuarioId,
                p.Usuario != null ? p.Usuario.NombreCompleto : null,
                p.Observaciones,
                p.Activo))
            .ToListAsync();

        return ServiceResult<List<PagoResponse>>.Success(pagos);
    }

    public async Task<ServiceResult<List<PagoResponse>>> GetPagosByOrdenServicioIdAsync(Guid ordenServicioId, Guid? soloClienteId = null)
    {
        var orden = await _context.OrdenesServicio
            .Include(o => o.Vehiculo)
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo);

        if (orden is null)
        {
            return ServiceResult<List<PagoResponse>>.NotFound();
        }

        var clienteAsociadoId = orden.ClienteId != Guid.Empty ? orden.ClienteId : orden.Vehiculo?.ClienteId;
        if (soloClienteId.HasValue && clienteAsociadoId != soloClienteId.Value)
        {
            return ServiceResult<List<PagoResponse>>.NotFound();
        }

        var pagos = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Include(p => p.Usuario)
            .Where(p => p.OrdenServicioId == ordenServicioId && p.Activo)
            .OrderBy(p => p.Fecha)
            .Select(p => new PagoResponse(
                p.Id,
                p.Monto,
                p.MetodoPagoId,
                p.MetodoPago.Nombre,
                p.MetodoPago.Codigo,
                p.Fecha,
                p.Referencia,
                p.EsAnticipo,
                p.VentaId,
                p.OrdenServicioId,
                p.UsuarioId,
                p.Usuario != null ? p.Usuario.NombreCompleto : null,
                p.Observaciones,
                p.Activo))
            .ToListAsync();

        return ServiceResult<List<PagoResponse>>.Success(pagos);
    }

    public async Task<ServiceResult<PagoResponse>> RegistrarPagoPedidoLimaAsync(
        Guid pedidoLimaId, RegistrarPagoRequest request, Guid? usuarioId)
    {
        if (request.Monto <= 0)
        {
            return ServiceResult<PagoResponse>.Invalid("El monto del pago debe ser mayor a 0.");
        }

        var metodo = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.Id == request.MetodoPagoId && m.Activo);

        if (metodo is null)
        {
            return ServiceResult<PagoResponse>.Invalid("El método de pago indicado no existe o está inactivo.");
        }

        if (CajaChicaService.EsCodigoEfectivo(metodo.Codigo))
        {
            var cajaAbierta = await _context.CajasChicas
                .AnyAsync(c => c.Estado == EstadoCajaChica.Abierta);
            if (!cajaAbierta)
            {
                return ServiceResult<PagoResponse>.Invalid(MensajeEfectivoSinCaja);
            }
        }

        Pago pago;
        await using var transaccion = await _context.Database.BeginTransactionAsync();
        try
        {
            // Bloqueo pesimista a nivel de fila sobre el pedido de Lima para serializar operaciones concurrentes
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"PedidosLima\" WHERE \"Id\" = {pedidoLimaId} FOR UPDATE");

            var pedido = await _context.PedidosLima
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == pedidoLimaId && p.Activo);

            if (pedido is null)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.NotFound();
            }

            if (pedido.Estado == EstadoPedidoLima.Cancelado)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("No se pueden registrar pagos en un pedido de Lima cancelado.");
            }

            if (pedido.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Pendiente)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("No se pueden registrar pagos en un pedido a Lima con aprobación de Gerencia pendiente.");
            }

            if (pedido.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Rechazado)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("No se pueden registrar pagos en un pedido a Lima con aprobación de Gerencia rechazada.");
            }

            var totalPagado = await _context.Pagos
                .Where(p => p.PedidoLimaId == pedido.Id && p.Activo)
                .SumAsync(p => (decimal?)p.Monto) ?? 0m;
            var saldo = Math.Max(0m, pedido.Total - totalPagado);

            if (saldo <= 0)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid("El pedido de Lima ya se encuentra pagada en su totalidad.");
            }

            if (request.Monto > saldo)
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid(
                    $"El monto del pago (S/ {request.Monto:F2}) no puede exceder el saldo pendiente (S/ {saldo:F2}).");
            }

            pago = new Pago
            {
                Monto           = request.Monto,
                MetodoPagoId    = request.MetodoPagoId,
                MetodoPago      = metodo,
                Fecha           = DateTime.UtcNow,
                Referencia      = string.IsNullOrWhiteSpace(request.Referencia) ? null : request.Referencia.Trim(),
                PedidoLimaId    = pedido.Id,
                EsAnticipo      = request.EsAnticipo || saldo > request.Monto,
                UsuarioId       = usuarioId,
                Observaciones   = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
                Activo          = true,
                FechaCreacion   = DateTime.UtcNow
            };

            _context.Pagos.Add(pago);
            var refPedido = !string.IsNullOrWhiteSpace(pedido.NumeroPedido) ? $"Pedido Lima #{pedido.NumeroPedido}" : $"Pedido Lima ({pedido.Cliente?.NombreCompleto ?? "Cliente"})";

            if (!await RegistrarMovimientoCajaChicaAutomaticoAsync(pago, metodo, refPedido, usuarioId))
            {
                await transaccion.RollbackAsync();
                return ServiceResult<PagoResponse>.Invalid(MensajeEfectivoSinCaja);
            }

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }

        await _auditoriaService.RegistrarEventoAsync(
            usuarioId,
            "Crear",
            "Pago",
            pago.Id.ToString(),
            new
            {
                pago.Monto,
                pago.MetodoPagoId,
                MetodoPago = metodo.Nombre,
                pago.PedidoLimaId,
                pago.EsAnticipo,
                pago.Referencia
            });

        string? usuarioNombre = null;
        if (usuarioId.HasValue)
        {
            usuarioNombre = await _context.Usuarios
                .Where(u => u.Id == usuarioId.Value)
                .Select(u => u.NombreCompleto)
                .FirstOrDefaultAsync();
        }

        return ServiceResult<PagoResponse>.Success(new PagoResponse(
            pago.Id,
            pago.Monto,
            pago.MetodoPagoId,
            metodo.Nombre,
            metodo.Codigo,
            pago.Fecha,
            pago.Referencia,
            pago.EsAnticipo,
            pago.VentaId,
            pago.OrdenServicioId,
            pago.UsuarioId,
            usuarioNombre,
            pago.Observaciones,
            pago.Activo));
    }

    public async Task<ServiceResult<List<PagoResponse>>> GetPagosByPedidoLimaIdAsync(
        Guid pedidoLimaId, Guid? soloClienteId = null)
    {
        var pedido = await _context.PedidosLima
            .Include(p => p.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .Include(p => p.Pagos)
                .ThenInclude(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == pedidoLimaId && p.Activo);

        if (pedido is null)
        {
            return ServiceResult<List<PagoResponse>>.NotFound();
        }

        if (soloClienteId.HasValue && pedido.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<List<PagoResponse>>.NotFound();
        }

        var pagos = pedido.Pagos
            .Where(p => p.Activo)
            .OrderBy(p => p.Fecha)
            .Select(p => new PagoResponse(
                p.Id,
                p.Monto,
                p.MetodoPagoId,
                p.MetodoPago.Nombre,
                p.MetodoPago.Codigo,
                p.Fecha,
                p.Referencia,
                p.EsAnticipo,
                p.VentaId,
                p.OrdenServicioId,
                p.UsuarioId,
                p.Usuario != null ? p.Usuario.NombreCompleto : null,
                p.Observaciones,
                p.Activo))
            .ToList();

        return ServiceResult<List<PagoResponse>>.Success(pagos);
    }

    /// <summary>
    /// Todo cobro entra como ingreso a la caja abierta; solo el efectivo suma al
    /// saldo. Se llama dentro de la transacción del pago: la caja se bloquea con
    /// FOR UPDATE, como en los movimientos manuales y el cierre, para que dos cobros
    /// simultáneos no pisen el saldo ni un cobro entre a una caja que se está cerrando.
    /// Devuelve false si el cobro es en efectivo y no hay caja abierta.
    /// </summary>
    private async Task<bool> RegistrarMovimientoCajaChicaAutomaticoAsync(
        Pago pago, MetodoPago metodo, string referenciaTexto, Guid? usuarioId)
    {
        var esEfectivo = CajaChicaService.EsCodigoEfectivo(metodo.Codigo);

        var cajaAbiertaId = await _context.CajasChicas
            .Where(c => c.Estado == EstadoCajaChica.Abierta)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        CajaChica? cajaAbierta = null;
        if (cajaAbiertaId != null)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"CajasChicas\" WHERE \"Id\" = {cajaAbiertaId.Value} FOR UPDATE");
            cajaAbierta = await _context.CajasChicas.FirstAsync(c => c.Id == cajaAbiertaId.Value);
            // Puede venir del rastreo de antes del bloqueo: se relee el saldo y el estado vigentes.
            await _context.Entry(cajaAbierta).ReloadAsync();
            if (cajaAbierta.Estado != EstadoCajaChica.Abierta)
            {
                cajaAbierta = null;
            }
        }

        if (cajaAbierta == null)
        {
            // El efectivo necesita una caja donde quedar; los demás métodos se
            // registran igual en el pago, sin movimiento de caja.
            return !esEfectivo;
        }

        _context.MovimientosCajaChica.Add(new MovimientoCajaChica
        {
            Id = Guid.NewGuid(),
            CajaChicaId = cajaAbierta.Id,
            Tipo = TipoMovimientoCaja.Ingreso,
            Monto = pago.Monto,
            Concepto = $"Cobro {metodo.Nombre} - {referenciaTexto}",
            Referencia = pago.Referencia,
            Fecha = DateTime.UtcNow,
            UsuarioId = usuarioId,
            PagoId = pago.Id,
            MetodoPagoId = metodo.Id,
            MetodoPagoNombre = metodo.Nombre,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        });

        if (esEfectivo)
        {
            cajaAbierta.SaldoCalculado += pago.Monto;
            cajaAbierta.FechaModificacion = DateTime.UtcNow;
        }

        return true;
    }
}
