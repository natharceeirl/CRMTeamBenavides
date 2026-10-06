using CRMTeamBenavides.Api.Features.Ventas;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class PagoService : IPagoService
{
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

        var venta = await _context.Ventas
            .Include(v => v.Pagos)
            .Include(v => v.Cliente)
            .Include(v => v.OrdenServicio)
            .Include(v => v.Comprobante)
            .FirstOrDefaultAsync(v => v.Id == ventaId && v.Activo);

        if (venta is null)
        {
            return ServiceResult<PagoResponse>.NotFound();
        }

        if (venta.Estado != EstadoVenta.Confirmada)
        {
            return ServiceResult<PagoResponse>.Invalid("Solo se pueden registrar pagos en ventas en estado Confirmada.");
        }

        var metodo = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.Id == request.MetodoPagoId && m.Activo);

        if (metodo is null)
        {
            return ServiceResult<PagoResponse>.Invalid("El método de pago indicado no existe o está inactivo.");
        }

        if (metodo.Codigo.Equals("EFECTIVO", StringComparison.OrdinalIgnoreCase))
        {
            var cajaAbierta = await _context.CajasChicas
                .AnyAsync(c => c.Estado == EstadoCajaChica.Abierta);
            if (!cajaAbierta)
            {
                return ServiceResult<PagoResponse>.Invalid(
                    "No se puede registrar un cobro en efectivo porque no hay ninguna Caja Chica abierta en este momento.");
            }
        }

        var totalPagado = venta.Pagos.Where(p => p.Activo).Sum(p => p.Monto);
        var saldo = Math.Max(0m, venta.Total - totalPagado);

        if (saldo <= 0)
        {
            return ServiceResult<PagoResponse>.Invalid("La venta ya se encuentra pagada en su totalidad.");
        }

        if (request.Monto > saldo)
        {
            return ServiceResult<PagoResponse>.Invalid(
                $"El monto del pago (S/ {request.Monto:F2}) no puede exceder el saldo pendiente (S/ {saldo:F2}).");
        }

        var pago = new Pago
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

        await RegistrarMovimientoCajaChicaAutomaticoAsync(pago, metodo, refVenta, usuarioId);
        await _context.SaveChangesAsync();

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

        var orden = await _context.OrdenesServicio
            .Include(o => o.Pagos)
            .FirstOrDefaultAsync(o => o.Id == ordenServicioId && o.Activo);

        if (orden is null)
        {
            return ServiceResult<PagoResponse>.NotFound();
        }

        if (orden.Estado == EstadoOrdenServicio.Cancelada)
        {
            return ServiceResult<PagoResponse>.Invalid("No se pueden registrar pagos en una orden de servicio cancelada.");
        }

        // Un adelanto después de liquidar no llegaría al saldo de la venta.
        var liquidada = await _context.Ventas.AnyAsync(v =>
            v.OrdenServicioId == ordenServicioId && v.Activo && v.Estado != EstadoVenta.Anulada);
        if (liquidada)
        {
            return ServiceResult<PagoResponse>.Invalid("La orden ya está liquidada: registra el pago en su venta.");
        }

        var metodo = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.Id == request.MetodoPagoId && m.Activo);

        if (metodo is null)
        {
            return ServiceResult<PagoResponse>.Invalid("El método de pago indicado no existe o está inactivo.");
        }

        if (metodo.Codigo.Equals("EFECTIVO", StringComparison.OrdinalIgnoreCase))
        {
            var cajaAbierta = await _context.CajasChicas
                .AnyAsync(c => c.Estado == EstadoCajaChica.Abierta);
            if (!cajaAbierta)
            {
                return ServiceResult<PagoResponse>.Invalid(
                    "No se puede registrar un cobro en efectivo porque no hay ninguna Caja Chica abierta en este momento.");
            }
        }

        var totalPagado = orden.Pagos.Where(p => p.Activo).Sum(p => p.Monto);
        var saldo = Math.Max(0m, orden.Total - totalPagado);

        if (saldo <= 0)
        {
            return ServiceResult<PagoResponse>.Invalid("La orden de servicio ya se encuentra pagada en su totalidad.");
        }

        if (request.Monto > saldo)
        {
            return ServiceResult<PagoResponse>.Invalid(
                $"El monto del pago (S/ {request.Monto:F2}) no puede exceder el saldo pendiente (S/ {saldo:F2}).");
        }

        var pago = new Pago
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
        await RegistrarMovimientoCajaChicaAutomaticoAsync(pago, metodo, refOrden, usuarioId);
        await _context.SaveChangesAsync();

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

        var pedido = await _context.PedidosLima
            .Include(p => p.Pagos)
            .Include(p => p.Cliente)
            .FirstOrDefaultAsync(p => p.Id == pedidoLimaId && p.Activo);

        if (pedido is null)
        {
            return ServiceResult<PagoResponse>.NotFound();
        }

        if (pedido.Estado == EstadoPedidoLima.Cancelado)
        {
            return ServiceResult<PagoResponse>.Invalid("No se pueden registrar pagos en un pedido de Lima cancelado.");
        }

        var metodo = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.Id == request.MetodoPagoId && m.Activo);

        if (metodo is null)
        {
            return ServiceResult<PagoResponse>.Invalid("El método de pago indicado no existe o está inactivo.");
        }

        if (metodo.Codigo.Equals("EFECTIVO", StringComparison.OrdinalIgnoreCase))
        {
            var cajaAbierta = await _context.CajasChicas
                .AnyAsync(c => c.Estado == EstadoCajaChica.Abierta);
            if (!cajaAbierta)
            {
                return ServiceResult<PagoResponse>.Invalid(
                    "No se puede registrar un cobro en efectivo porque no hay ninguna Caja Chica abierta en este momento.");
            }
        }

        var totalPagado = pedido.Pagos.Where(p => p.Activo).Sum(p => p.Monto);
        var saldo = Math.Max(0m, pedido.Total - totalPagado);

        if (saldo <= 0)
        {
            return ServiceResult<PagoResponse>.Invalid("El pedido de Lima ya se encuentra pagada en su totalidad.");
        }

        if (request.Monto > saldo)
        {
            return ServiceResult<PagoResponse>.Invalid(
                $"El monto del pago (S/ {request.Monto:F2}) no puede exceder el saldo pendiente (S/ {saldo:F2}).");
        }

        var pago = new Pago
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
        await RegistrarMovimientoCajaChicaAutomaticoAsync(pago, metodo, refPedido, usuarioId);
        await _context.SaveChangesAsync();

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

    private async Task RegistrarMovimientoCajaChicaAutomaticoAsync(
        Pago pago, MetodoPago metodo, string referenciaTexto, Guid? usuarioId)
    {
        var cajaAbierta = await _context.CajasChicas
            .FirstOrDefaultAsync(c => c.Estado == EstadoCajaChica.Abierta);

        if (cajaAbierta != null)
        {
            var movCaja = new MovimientoCajaChica
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
            };

            if (metodo.Codigo.Equals("EFECTIVO", StringComparison.OrdinalIgnoreCase))
            {
                cajaAbierta.SaldoCalculado += pago.Monto;
                cajaAbierta.FechaModificacion = DateTime.UtcNow;
            }

            _context.MovimientosCajaChica.Add(movCaja);
        }
    }
}
