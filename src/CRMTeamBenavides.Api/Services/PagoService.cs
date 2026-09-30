using CRMTeamBenavides.Api.Features.Ventas;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class PagoService : IPagoService
{
    private readonly ApplicationDbContext _context;

    public PagoService(ApplicationDbContext context)
    {
        _context = context;
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
        await _context.SaveChangesAsync();

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

        var metodo = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.Id == request.MetodoPagoId && m.Activo);

        if (metodo is null)
        {
            return ServiceResult<PagoResponse>.Invalid("El método de pago indicado no existe o está inactivo.");
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
        await _context.SaveChangesAsync();

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
}
