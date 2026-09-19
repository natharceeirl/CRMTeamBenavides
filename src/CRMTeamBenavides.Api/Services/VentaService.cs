using CRMTeamBenavides.Api.Features.Ventas;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class VentaService : IVentaService
{
    private readonly ApplicationDbContext _context;

    public VentaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<VentaResponse>> GetAllAsync(
        Guid? clienteId,
        EstadoVenta? estado,
        Guid? ordenServicioId,
        DateTime? fechaDesde,
        DateTime? fechaHasta)
    {
        var query = _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Detalles)
            .Where(v => v.Activo);

        if (clienteId.HasValue)
        {
            query = query.Where(v => v.ClienteId == clienteId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(v => v.Estado == estado.Value);
        }

        if (ordenServicioId.HasValue)
        {
            query = query.Where(v => v.OrdenServicioId == ordenServicioId.Value);
        }

        if (fechaDesde.HasValue)
        {
            var desdeUtc = DateTime.SpecifyKind(fechaDesde.Value, DateTimeKind.Utc);
            query = query.Where(v => v.Fecha >= desdeUtc);
        }

        if (fechaHasta.HasValue)
        {
            var hastaUtc = DateTime.SpecifyKind(fechaHasta.Value, DateTimeKind.Utc);
            query = query.Where(v => v.Fecha <= hastaUtc);
        }

        return await query
            .OrderByDescending(v => v.Fecha)
            .Select(v => new VentaResponse(
                v.Id,
                v.ClienteId,
                v.Cliente.NombreCompleto,
                v.OrdenServicioId,
                v.Estado.ToString(),
                (int)v.Estado,
                v.Fecha,
                v.Total,
                v.Detalles.Count(d => d.Activo),
                v.Activo))
            .ToListAsync();
    }

    public async Task<ServiceResult<VentaDetalleResponse>> GetByIdAsync(Guid id)
    {
        var venta = await _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Comprobante)
            .Include(v => v.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        return venta is null
            ? ServiceResult<VentaDetalleResponse>.NotFound()
            : ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(venta));
    }

    public async Task<ServiceResult<VentaDetalleResponse>> CreateAsync(CreateVentaRequest request)
    {
        if (request.Detalles is null || request.Detalles.Count == 0)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La venta debe contener al menos un detalle.");
        }

        if (request.Detalles.Any(d => d.Cantidad <= 0))
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La cantidad de cada producto debe ser mayor a 0.");
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == request.ClienteId && c.Activo);

        if (cliente is null)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("El cliente indicado no existe o está inactivo.");
        }

        if (request.OrdenServicioId.HasValue)
        {
            var ordenServicio = await _context.OrdenesServicio
                .FirstOrDefaultAsync(o => o.Id == request.OrdenServicioId.Value && o.Activo);

            if (ordenServicio is null)
            {
                return ServiceResult<VentaDetalleResponse>.Invalid("La orden de servicio indicada no existe o está inactiva.");
            }
        }

        var cantidadesPorProducto = request.Detalles
            .GroupBy(d => d.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));

        // Ordenamos deterministicamente los IDs de producto para evitar deadlocks en concurrencia
        var orderedProductIds = cantidadesPorProducto.Keys.OrderBy(id => id).ToList();

        // --- CASO 1: COTIZACIÓN (No bloquea ni descuenta inventario) ---
        if (request.EsCotizacion)
        {
            var productos = await _context.Productos
                .Where(p => orderedProductIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            if (productos.Count != orderedProductIds.Count || productos.Values.Any(p => !p.Activo))
            {
                return ServiceResult<VentaDetalleResponse>.Invalid("Uno o más productos no existen o están inactivos.");
            }

            var cotizacion = new Venta
            {
                ClienteId       = request.ClienteId,
                Cliente         = cliente,
                OrdenServicioId = request.OrdenServicioId,
                Estado          = EstadoVenta.Cotizacion,
                Fecha           = DateTime.UtcNow,
                FechaCreacion   = DateTime.UtcNow,
                Activo          = true
            };

            foreach (var item in request.Detalles)
            {
                var prod = productos[item.ProductoId];
                var detalle = new DetalleVenta
                {
                    VentaId        = cotizacion.Id,
                    Venta          = cotizacion,
                    ProductoId     = prod.Id,
                    Producto       = prod,
                    Cantidad       = item.Cantidad,
                    PrecioUnitario = prod.PrecioVenta, // Precio autoritativo de catálogo
                    FechaCreacion  = DateTime.UtcNow,
                    Activo         = true
                };
                cotizacion.Detalles.Add(detalle);
                _context.DetallesVenta.Add(detalle);
            }

            cotizacion.Total = cotizacion.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
            _context.Ventas.Add(cotizacion);

            await _context.SaveChangesAsync();
            return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(cotizacion));
        }

        // --- CASO 2: VENTA CONFIRMADA DIRECTA (Transaccional con bloqueo pesimista) ---
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Bloqueo pesimista a nivel de fila (FOR UPDATE) en orden para evitar condiciones de carrera y deadlocks
            foreach (var prodId in orderedProductIds)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {prodId} FOR UPDATE");
            }

            var productos = await _context.Productos
                .Where(p => orderedProductIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            if (productos.Count != orderedProductIds.Count || productos.Values.Any(p => !p.Activo))
            {
                return ServiceResult<VentaDetalleResponse>.Invalid("Uno o más productos no existen o están inactivos.");
            }

            // Validar suficiencia de stock bajo el bloqueo
            foreach (var (prodId, cantidadRequerida) in cantidadesPorProducto)
            {
                var prod = productos[prodId];
                if (prod.StockActual < cantidadRequerida)
                {
                    return ServiceResult<VentaDetalleResponse>.Invalid(
                        $"Stock insuficiente para el producto '{prod.Nombre}'. Stock disponible: {prod.StockActual}, solicitado: {cantidadRequerida}.");
                }
            }

            var venta = new Venta
            {
                ClienteId       = request.ClienteId,
                Cliente         = cliente,
                OrdenServicioId = request.OrdenServicioId,
                Estado          = EstadoVenta.Confirmada,
                Fecha           = DateTime.UtcNow,
                FechaCreacion   = DateTime.UtcNow,
                Activo          = true
            };
            _context.Ventas.Add(venta);

            foreach (var item in request.Detalles)
            {
                var prod = productos[item.ProductoId];
                prod.StockActual -= item.Cantidad;
                prod.FechaModificacion = DateTime.UtcNow;

                var detalle = new DetalleVenta
                {
                    VentaId        = venta.Id,
                    Venta          = venta,
                    ProductoId     = prod.Id,
                    Producto       = prod,
                    Cantidad       = item.Cantidad,
                    PrecioUnitario = prod.PrecioVenta, // Precio autoritativo
                    FechaCreacion  = DateTime.UtcNow,
                    Activo         = true
                };
                venta.Detalles.Add(detalle);
                _context.DetallesVenta.Add(detalle);

                var movimiento = new MovimientoInventario
                {
                    ProductoId    = prod.Id,
                    Tipo          = TipoMovimientoInventario.Salida,
                    Cantidad      = item.Cantidad,
                    Motivo        = $"Venta #{venta.Id}",
                    VentaId       = venta.Id,
                    FechaCreacion = DateTime.UtcNow,
                    Activo        = true
                };
                _context.MovimientosInventario.Add(movimiento);
            }

            venta.Total = venta.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(venta));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult<VentaDetalleResponse>> ConfirmarCotizacionAsync(Guid id)
    {
        var venta = await _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        if (venta is null)
        {
            return ServiceResult<VentaDetalleResponse>.NotFound();
        }

        if (venta.Estado == EstadoVenta.Confirmada)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La venta ya se encuentra confirmada.");
        }

        if (venta.Estado == EstadoVenta.Anulada)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("No se puede confirmar una cotización que ha sido anulada.");
        }

        if (venta.Estado != EstadoVenta.Cotizacion)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("Solo se pueden confirmar ventas en estado Cotización.");
        }

        if (!venta.Cliente.Activo)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("El cliente asociado a la cotización se encuentra inactivo.");
        }

        var activeDetails = venta.Detalles.Where(d => d.Activo).ToList();
        if (activeDetails.Count == 0)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La cotización no tiene detalles activos para confirmar.");
        }

        var cantidadesPorProducto = activeDetails
            .GroupBy(d => d.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));

        var orderedProductIds = cantidadesPorProducto.Keys.OrderBy(pId => pId).ToList();

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Bloqueo pesimista ordenado
            foreach (var prodId in orderedProductIds)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {prodId} FOR UPDATE");
            }

            var productos = await _context.Productos
                .Where(p => orderedProductIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var (prodId, cantidadRequerida) in cantidadesPorProducto)
            {
                if (!productos.TryGetValue(prodId, out var prod) || !prod.Activo)
                {
                    return ServiceResult<VentaDetalleResponse>.Invalid("Uno o más productos de la cotización ya no existen o están inactivos.");
                }

                if (prod.StockActual < cantidadRequerida)
                {
                    return ServiceResult<VentaDetalleResponse>.Invalid(
                        $"Stock insuficiente para confirmar la cotización. Producto: '{prod.Nombre}'. Stock disponible: {prod.StockActual}, solicitado: {cantidadRequerida}.");
                }
            }

            foreach (var det in activeDetails)
            {
                var prod = productos[det.ProductoId];
                prod.StockActual -= det.Cantidad;
                prod.FechaModificacion = DateTime.UtcNow;

                var movimiento = new MovimientoInventario
                {
                    ProductoId    = prod.Id,
                    Tipo          = TipoMovimientoInventario.Salida,
                    Cantidad      = det.Cantidad,
                    Motivo        = $"Venta #{venta.Id}",
                    VentaId       = venta.Id,
                    FechaCreacion = DateTime.UtcNow,
                    Activo        = true
                };
                _context.MovimientosInventario.Add(movimiento);
            }

            venta.Estado            = EstadoVenta.Confirmada;
            venta.Fecha             = DateTime.UtcNow;
            venta.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(venta));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult<VentaDetalleResponse>> AnularAsync(Guid id)
    {
        var venta = await _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Comprobante)
            .Include(v => v.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        if (venta is null)
        {
            return ServiceResult<VentaDetalleResponse>.NotFound();
        }

        if (venta.Estado == EstadoVenta.Anulada)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La venta ya se encuentra anulada.");
        }

        // Si era una cotización, se anula directamente sin tocar inventario
        if (venta.Estado == EstadoVenta.Cotizacion)
        {
            venta.Estado            = EstadoVenta.Anulada;
            venta.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(venta));
        }

        // Si era Confirmada, reponer stock transaccionalmente
        var activeDetails = venta.Detalles.Where(d => d.Activo).ToList();
        var orderedProductIds = activeDetails
            .Select(d => d.ProductoId)
            .Distinct()
            .OrderBy(pId => pId)
            .ToList();

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var prodId in orderedProductIds)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {prodId} FOR UPDATE");
            }

            var productos = await _context.Productos
                .Where(p => orderedProductIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var det in activeDetails)
            {
                if (productos.TryGetValue(det.ProductoId, out var prod))
                {
                    prod.StockActual += det.Cantidad;
                    prod.FechaModificacion = DateTime.UtcNow;

                    var movimiento = new MovimientoInventario
                    {
                        ProductoId    = prod.Id,
                        Tipo          = TipoMovimientoInventario.Entrada,
                        Cantidad      = det.Cantidad,
                        Motivo        = $"Devolución por anulación de Venta #{venta.Id}",
                        VentaId       = venta.Id,
                        FechaCreacion = DateTime.UtcNow,
                        Activo        = true
                    };
                    _context.MovimientosInventario.Add(movimiento);
                }
            }

            venta.Estado            = EstadoVenta.Anulada;
            venta.FechaModificacion = DateTime.UtcNow;

            if (venta.Comprobante != null && venta.Comprobante.Estado != "Anulado")
            {
                venta.Comprobante.Estado = "Anulado";
                venta.Comprobante.FechaModificacion = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(venta));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult<ComprobanteResponse>> GetComprobanteAsync(Guid ventaId)
    {
        var venta = await _context.Ventas
            .Include(v => v.Comprobante)
            .FirstOrDefaultAsync(v => v.Id == ventaId && v.Activo);

        if (venta is null)
        {
            return ServiceResult<ComprobanteResponse>.NotFound();
        }

        if (venta.Comprobante is null || !venta.Comprobante.Activo)
        {
            return ServiceResult<ComprobanteResponse>.NotFound();
        }

        return ServiceResult<ComprobanteResponse>.Success(MapToComprobanteResponse(venta.Comprobante));
    }

    public async Task<ServiceResult<ComprobanteResponse>> RegistrarComprobanteAsync(Guid ventaId, RegistrarComprobanteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Tipo))
        {
            return ServiceResult<ComprobanteResponse>.Invalid("El tipo de comprobante es requerido.");
        }

        var venta = await _context.Ventas
            .Include(v => v.Comprobante)
            .FirstOrDefaultAsync(v => v.Id == ventaId && v.Activo);

        if (venta is null)
        {
            return ServiceResult<ComprobanteResponse>.NotFound();
        }

        if (venta.Estado != EstadoVenta.Confirmada)
        {
            return ServiceResult<ComprobanteResponse>.Invalid("Solo se pueden asociar comprobantes a ventas en estado Confirmada.");
        }

        if (venta.Comprobante != null)
        {
            return ServiceResult<ComprobanteResponse>.Invalid("La venta ya cuenta con un comprobante registrado.");
        }

        var comprobante = new Comprobante
        {
            VentaId = ventaId,
            Tipo = request.Tipo.Trim(),
            Serie = string.IsNullOrWhiteSpace(request.Serie) ? null : request.Serie.Trim().ToUpperInvariant(),
            Numero = string.IsNullOrWhiteSpace(request.Numero) ? null : request.Numero.Trim(),
            Estado = "Emitido",
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Comprobantes.Add(comprobante);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Protección ante concurrencia: el índice único IX_Comprobantes_VentaId previene duplicados
            return ServiceResult<ComprobanteResponse>.Invalid("La venta ya cuenta con un comprobante registrado.");
        }

        return ServiceResult<ComprobanteResponse>.Success(MapToComprobanteResponse(comprobante));
    }

    public async Task<ServiceResult<ComprobanteResponse>> AnularComprobanteAsync(Guid ventaId)
    {
        var venta = await _context.Ventas
            .Include(v => v.Comprobante)
            .FirstOrDefaultAsync(v => v.Id == ventaId && v.Activo);

        if (venta is null)
        {
            return ServiceResult<ComprobanteResponse>.NotFound();
        }

        if (venta.Comprobante is null || !venta.Comprobante.Activo)
        {
            return ServiceResult<ComprobanteResponse>.NotFound();
        }

        if (venta.Comprobante.Estado == "Anulado")
        {
            return ServiceResult<ComprobanteResponse>.Invalid("El comprobante ya se encuentra anulado.");
        }

        venta.Comprobante.Estado = "Anulado";
        venta.Comprobante.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<ComprobanteResponse>.Success(MapToComprobanteResponse(venta.Comprobante));
    }

    private static VentaDetalleResponse MapToDetalleResponse(Venta v) => new(
        v.Id,
        v.ClienteId,
        v.Cliente.NombreCompleto,
        v.Cliente.DocumentoIdentidad,
        v.Cliente.Telefono,
        v.OrdenServicioId,
        v.Estado.ToString(),
        (int)v.Estado,
        v.Fecha,
        v.Total,
        v.Detalles
            .Where(d => d.Activo)
            .OrderBy(d => d.FechaCreacion)
            .Select(d => new DetalleVentaResponse(
                d.Id,
                d.ProductoId,
                d.Producto?.Codigo ?? string.Empty,
                d.Producto?.Nombre ?? string.Empty,
                d.Cantidad,
                d.PrecioUnitario,
                d.Cantidad * d.PrecioUnitario))
            .ToList(),
        v.Activo,
        v.Comprobante != null ? MapToComprobanteResponse(v.Comprobante) : null);

    private static ComprobanteResponse MapToComprobanteResponse(Comprobante c) => new(
        c.Id,
        c.VentaId,
        c.Tipo,
        c.Serie,
        c.Numero,
        c.Estado,
        c.FechaCreacion,
        c.Activo);
}
