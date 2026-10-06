using System.Globalization;
using System.Text.Json;
using CRMTeamBenavides.Api.Features.CajaChica;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class CajaChicaService : ICajaChicaService
{
    private readonly ApplicationDbContext _context;

    public CajaChicaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EstadoCajaActualResponse> ObtenerCajaActualAsync(CancellationToken ct = default)
    {
        var caja = await _context.CajasChicas
            .AsNoTracking()
            .Include(c => c.UsuarioApertura)
            .Include(c => c.UsuarioCierre)
            .Include(c => c.Movimientos.OrderByDescending(m => m.Fecha))
                .ThenInclude(m => m.Usuario)
            .FirstOrDefaultAsync(c => c.Estado == EstadoCajaChica.Abierta, ct);

        if (caja == null)
        {
            return new EstadoCajaActualResponse(false, null);
        }

        return new EstadoCajaActualResponse(true, MapearDetalle(caja));
    }

    public async Task<CajaChicaDetalleResponse?> ObtenerCajaPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var caja = await _context.CajasChicas
            .AsNoTracking()
            .Include(c => c.UsuarioApertura)
            .Include(c => c.UsuarioCierre)
            .Include(c => c.Movimientos.OrderByDescending(m => m.Fecha))
                .ThenInclude(m => m.Usuario)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        return caja == null ? null : MapearDetalle(caja);
    }

    public async Task<List<CajaChicaResponse>> ObtenerHistorialCajasAsync(CancellationToken ct = default)
    {
        var cajas = await _context.CajasChicas
            .AsNoTracking()
            .Include(c => c.UsuarioApertura)
            .Include(c => c.UsuarioCierre)
            .Include(c => c.Movimientos)
            .OrderByDescending(c => c.FechaApertura)
            .ToListAsync(ct);

        return cajas.Select(c =>
        {
            var ingresos = c.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Monto);
            var egresos = c.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Monto);

            return new CajaChicaResponse(
                c.Id,
                c.MontoApertura,
                c.MontoCierre,
                c.SaldoCalculado,
                c.FechaApertura,
                c.FechaCierre,
                c.Estado,
                c.Estado == EstadoCajaChica.Abierta ? "Abierta" : "Cerrada",
                c.ObservacionesApertura,
                c.ObservacionesCierre,
                c.UsuarioAperturaId,
                c.UsuarioApertura?.NombreCompleto,
                c.UsuarioCierreId,
                c.UsuarioCierre?.NombreCompleto,
                ingresos,
                egresos,
                c.Movimientos.Count,
                c.FechaCreacion
            );
        }).ToList();
    }

    public async Task<List<MovimientoCajaResponse>> ObtenerMovimientosAsync(
        Guid? cajaChicaId = null,
        TipoMovimientoCaja? tipo = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        CancellationToken ct = default)
    {
        var query = _context.MovimientosCajaChica
            .AsNoTracking()
            .Include(m => m.Usuario)
            .AsQueryable();

        if (cajaChicaId.HasValue)
            query = query.Where(m => m.CajaChicaId == cajaChicaId.Value);

        if (tipo.HasValue)
            query = query.Where(m => m.Tipo == tipo.Value);

        if (fechaDesde.HasValue)
            query = query.Where(m => m.Fecha >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(m => m.Fecha <= fechaHasta.Value);

        var list = await query
            .OrderByDescending(m => m.Fecha)
            .ToListAsync(ct);

        return list.Select(MapearMovimiento).ToList();
    }

    public async Task<ServiceResult<CajaChicaDetalleResponse>> AperturarCajaAsync(
        AperturaCajaRequest request,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        if (request.MontoApertura <= 0)
            return ServiceResult<CajaChicaDetalleResponse>.Invalid("El monto de apertura debe ser mayor a cero.");

        var existeAbierta = await _context.CajasChicas
            .AnyAsync(c => c.Estado == EstadoCajaChica.Abierta, ct);

        if (existeAbierta)
            return ServiceResult<CajaChicaDetalleResponse>.Conflict("Ya existe una caja chica abierta. Debe cerrarla antes de aperturar una nueva.");

        var caja = new CajaChica
        {
            Id = Guid.NewGuid(),
            MontoApertura = request.MontoApertura,
            SaldoCalculado = request.MontoApertura,
            FechaApertura = DateTime.UtcNow,
            Estado = EstadoCajaChica.Abierta,
            ObservacionesApertura = request.Observaciones?.Trim(),
            UsuarioAperturaId = usuarioId,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.CajasChicas.Add(caja);

        _context.EventosAuditoria.Add(new EventoAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Accion = "Apertura",
            Entidad = "CajaChica",
            EntidadId = caja.Id.ToString(),
            Detalle = JsonSerializer.Serialize(new
            {
                MontoApertura = caja.MontoApertura,
                Observaciones = caja.ObservacionesApertura
            })
        });

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<CajaChicaDetalleResponse>.Conflict("Ya existe una caja chica abierta. Debe cerrarla antes de aperturar una nueva.");
        }

        return ServiceResult<CajaChicaDetalleResponse>.Success(MapearDetalle(caja));
    }

    public async Task<ServiceResult<CajaChicaDetalleResponse>> CerrarCajaAsync(
        CierreCajaRequest request,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        var caja = await _context.CajasChicas
            .Include(c => c.UsuarioApertura)
            .Include(c => c.UsuarioCierre)
            .Include(c => c.Movimientos)
                .ThenInclude(m => m.Usuario)
            .FirstOrDefaultAsync(c => c.Estado == EstadoCajaChica.Abierta, ct);

        if (caja == null)
            return ServiceResult<CajaChicaDetalleResponse>.NotFound("No hay ninguna caja chica abierta para cerrar.");

        var totalIngresos = caja.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Monto);
        var totalEgresos = caja.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Monto);
        var saldoFinal = caja.MontoApertura + totalIngresos - totalEgresos;

        caja.SaldoCalculado = saldoFinal;
        caja.MontoCierre = saldoFinal;
        caja.Estado = EstadoCajaChica.Cerrada;
        caja.FechaCierre = DateTime.UtcNow;
        caja.UsuarioCierreId = usuarioId;
        caja.ObservacionesCierre = request.Observaciones?.Trim();
        caja.FechaModificacion = DateTime.UtcNow;

        _context.EventosAuditoria.Add(new EventoAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Accion = "Cierre",
            Entidad = "CajaChica",
            EntidadId = caja.Id.ToString(),
            Detalle = JsonSerializer.Serialize(new
            {
                MontoApertura = caja.MontoApertura,
                MontoCierre = saldoFinal,
                TotalIngresos = totalIngresos,
                TotalEgresos = totalEgresos,
                Observaciones = caja.ObservacionesCierre
            })
        });

        await _context.SaveChangesAsync(ct);

        return ServiceResult<CajaChicaDetalleResponse>.Success(MapearDetalle(caja));
    }

    public async Task<ServiceResult<MovimientoCajaResponse>> RegistrarMovimientoAsync(
        RegistrarMovimientoCajaRequest request,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        if (request.Monto <= 0)
            return ServiceResult<MovimientoCajaResponse>.Invalid("El monto del movimiento debe ser mayor a cero.");

        if (string.IsNullOrWhiteSpace(request.Concepto))
            return ServiceResult<MovimientoCajaResponse>.Invalid("El concepto del movimiento es obligatorio.");

        CajaChica? caja;

        if (request.CajaChicaId.HasValue)
        {
            caja = await _context.CajasChicas.FirstOrDefaultAsync(c => c.Id == request.CajaChicaId.Value, ct);
            if (caja == null)
                return ServiceResult<MovimientoCajaResponse>.NotFound("La caja chica especificada no existe.");

            if (caja.Estado == EstadoCajaChica.Cerrada)
                return ServiceResult<MovimientoCajaResponse>.Invalid("No se pueden registrar movimientos en una caja chica cerrada.");
        }
        else
        {
            caja = await _context.CajasChicas.FirstOrDefaultAsync(c => c.Estado == EstadoCajaChica.Abierta, ct);
            if (caja == null)
                return ServiceResult<MovimientoCajaResponse>.Invalid("No hay ninguna caja chica abierta para registrar movimientos.");
        }

        // La caja se bloquea y se relee: dos egresos simultáneos no deben pasar los
        // dos el control de saldo.
        await using var transaccion = await _context.Database.BeginTransactionAsync(ct);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM \"CajasChicas\" WHERE \"Id\" = {caja.Id} FOR UPDATE", ct);
        await _context.Entry(caja).ReloadAsync(ct);

        if (caja.Estado == EstadoCajaChica.Cerrada)
            return ServiceResult<MovimientoCajaResponse>.Invalid("No se pueden registrar movimientos en una caja chica cerrada.");

        if (request.Tipo == TipoMovimientoCaja.Egreso && request.Monto > caja.SaldoCalculado)
            return ServiceResult<MovimientoCajaResponse>.Invalid(
                $"El egreso (S/ {request.Monto.ToString("0.00", CultureInfo.InvariantCulture)}) supera el saldo de la caja (S/ {caja.SaldoCalculado.ToString("0.00", CultureInfo.InvariantCulture)}).");

        if (request.Tipo == TipoMovimientoCaja.Ingreso)
        {
            caja.SaldoCalculado += request.Monto;
        }
        else if (request.Tipo == TipoMovimientoCaja.Egreso)
        {
            caja.SaldoCalculado -= request.Monto;
        }

        caja.FechaModificacion = DateTime.UtcNow;

        var mov = new MovimientoCajaChica
        {
            Id = Guid.NewGuid(),
            CajaChicaId = caja.Id,
            Tipo = request.Tipo,
            Monto = request.Monto,
            Concepto = request.Concepto.Trim(),
            Referencia = request.Referencia?.Trim(),
            Fecha = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.MovimientosCajaChica.Add(mov);

        _context.EventosAuditoria.Add(new EventoAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Accion = request.Tipo == TipoMovimientoCaja.Ingreso ? "IngresoCaja" : "EgresoCaja",
            Entidad = "MovimientoCajaChica",
            EntidadId = mov.Id.ToString(),
            Detalle = JsonSerializer.Serialize(new
            {
                CajaChicaId = caja.Id,
                Tipo = request.Tipo.ToString(),
                Monto = request.Monto,
                Concepto = mov.Concepto,
                Referencia = mov.Referencia,
                SaldoCalculadoResultante = caja.SaldoCalculado
            })
        });

        await _context.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        Usuario? usuario = null;
        if (usuarioId.HasValue)
        {
            usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId.Value, ct);
        }

        mov.Usuario = usuario;

        return ServiceResult<MovimientoCajaResponse>.Success(MapearMovimiento(mov));
    }

    public async Task<ResumenMetodosPagoCajaResponse> ObtenerResumenMetodosAsync(
        Guid? cajaChicaId = null,
        CancellationToken ct = default)
    {
        CajaChica? caja = null;
        if (cajaChicaId.HasValue)
        {
            caja = await _context.CajasChicas.FirstOrDefaultAsync(c => c.Id == cajaChicaId.Value, ct);
        }
        else
        {
            caja = await _context.CajasChicas.FirstOrDefaultAsync(c => c.Estado == EstadoCajaChica.Abierta, ct);
        }

        var cajaId = caja?.Id;

        var query = _context.MovimientosCajaChica
            .AsNoTracking()
            .Include(m => m.MetodoPago)
            .Where(m => m.Tipo == TipoMovimientoCaja.Ingreso && m.Activo);

        if (cajaId.HasValue)
        {
            query = query.Where(m => m.CajaChicaId == cajaId.Value);
        }
        else
        {
            var hoyPeru = HoraPeru.DesdeUtc(DateTime.UtcNow).Date;
            var inicioHoyUtc = HoraPeru.InicioDelDiaUtc(hoyPeru);
            var finHoyUtc = inicioHoyUtc.AddDays(1);
            query = query.Where(m => m.Fecha >= inicioHoyUtc && m.Fecha < finHoyUtc);
        }

        var movimientos = await query.ToListAsync(ct);

        decimal totalEfectivo = 0m;
        decimal totalYapePlin = 0m;
        decimal totalTarjeta = 0m;
        decimal totalTransferencia = 0m;
        decimal totalGeneral = 0m;
        var porMetodo = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var mov in movimientos)
        {
            var metodoNombre = (mov.MetodoPagoNombre ?? mov.MetodoPago?.Nombre ?? "EFECTIVO").Trim();
            var metodoCodigo = (mov.MetodoPago?.Codigo ?? metodoNombre).ToUpperInvariant();

            totalGeneral += mov.Monto;

            if (porMetodo.ContainsKey(metodoNombre))
                porMetodo[metodoNombre] += mov.Monto;
            else
                porMetodo[metodoNombre] = mov.Monto;

            if (metodoCodigo.Contains("EFECTIVO"))
                totalEfectivo += mov.Monto;
            else if (metodoCodigo.Contains("YAPE") || metodoCodigo.Contains("PLIN"))
                totalYapePlin += mov.Monto;
            else if (metodoCodigo.Contains("TARJETA"))
                totalTarjeta += mov.Monto;
            else if (metodoCodigo.Contains("TRANSFERENCIA"))
                totalTransferencia += mov.Monto;
        }

        return new ResumenMetodosPagoCajaResponse(
            cajaId,
            totalEfectivo,
            totalYapePlin,
            totalTarjeta,
            totalTransferencia,
            porMetodo,
            totalGeneral
        );
    }

    private static MovimientoCajaResponse MapearMovimiento(MovimientoCajaChica m)
    {
        return new MovimientoCajaResponse(
            m.Id,
            m.CajaChicaId,
            m.Tipo,
            m.Tipo == TipoMovimientoCaja.Ingreso ? "Ingreso" : "Egreso",
            m.Monto,
            m.Concepto,
            m.Referencia,
            m.Fecha,
            m.UsuarioId,
            m.Usuario?.NombreCompleto,
            m.PagoId,
            m.MetodoPagoId,
            m.MetodoPagoNombre ?? m.MetodoPago?.Nombre
        );
    }

    private static CajaChicaDetalleResponse MapearDetalle(CajaChica c)
    {
        var ingresos = c.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Monto);
        var egresos = c.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Monto);

        return new CajaChicaDetalleResponse(
            c.Id,
            c.MontoApertura,
            c.MontoCierre,
            c.SaldoCalculado,
            c.FechaApertura,
            c.FechaCierre,
            c.Estado,
            c.Estado == EstadoCajaChica.Abierta ? "Abierta" : "Cerrada",
            c.ObservacionesApertura,
            c.ObservacionesCierre,
            c.UsuarioAperturaId,
            c.UsuarioApertura?.NombreCompleto,
            c.UsuarioCierreId,
            c.UsuarioCierre?.NombreCompleto,
            ingresos,
            egresos,
            c.Movimientos.OrderByDescending(m => m.Fecha).Select(MapearMovimiento).ToList()
        );
    }
}
