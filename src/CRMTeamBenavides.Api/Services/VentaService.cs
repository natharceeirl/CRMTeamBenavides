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
        DateTime? fechaHasta,
        Guid? soloClienteId = null)
    {
        var query = _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Detalles)
            .Include(v => v.Pagos)
            .Where(v => v.Activo);

        if (soloClienteId.HasValue)
        {
            query = query.Where(v => v.ClienteId == soloClienteId.Value);
        }
        else if (clienteId.HasValue)
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

        var lista = await query
            .OrderByDescending(v => v.Fecha)
            .ToListAsync();

        return lista.Select(v =>
        {
            var totalPagado = v.Pagos.Where(p => p.Activo).Sum(p => p.Monto);
            var saldo = Math.Max(0m, v.Total - totalPagado);
            var estadoPago = saldo == 0m ? "Pagado" : (totalPagado > 0m ? "Parcial" : "Pendiente");

            return new VentaResponse(
                v.Id,
                v.ClienteId,
                v.Cliente.NombreCompleto,
                v.OrdenServicioId,
                v.Estado.ToString(),
                (int)v.Estado,
                v.Fecha,
                v.Total,
                v.Detalles.Count(d => d.Activo),
                v.Activo,
                v.SubtotalGravado,
                v.SubtotalExonerado,
                v.SubtotalInafecto,
                v.MontoIgv,
                totalPagado,
                saldo,
                estadoPago);
        }).ToList();
    }

    public async Task<ServiceResult<VentaDetalleResponse>> GetByIdAsync(Guid id, Guid? soloClienteId = null)
    {
        var venta = await _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Comprobante)
            .Include(v => v.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Producto)
            .Include(v => v.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Servicio)
            .Include(v => v.Pagos.Where(p => p.Activo))
                .ThenInclude(p => p.MetodoPago)
            .Include(v => v.Pagos.Where(p => p.Activo))
                .ThenInclude(p => p.Usuario)
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        if (venta is null)
        {
            return ServiceResult<VentaDetalleResponse>.NotFound();
        }

        if (soloClienteId.HasValue && venta.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<VentaDetalleResponse>.NotFound();
        }

        return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(venta));
    }

    public async Task<ServiceResult<VentaDetalleResponse>> CreateAsync(
        CreateVentaRequest request,
        bool puedeModificarPrecios = false,
        bool puedeAplicarDescuentos = false,
        Guid? soloClienteId = null)
    {
        if (soloClienteId.HasValue && request.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("No tiene permisos para registrar ventas a nombre de otro cliente.");
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == request.ClienteId && c.Activo);

        if (cliente is null)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("El cliente indicado no existe o está inactivo.");
        }

        var config = await _context.ConfiguracionesEmpresa.FirstOrDefaultAsync();
        var porcentajeIgv = config?.PorcentajeIgv ?? 18.00m;

        // -------------------------------------------------------------------
        // CASO A: VENTA VINCULADA A ORDEN DE SERVICIO (Liquidación de taller)
        // -------------------------------------------------------------------
        if (request.OrdenServicioId.HasValue)
        {
            var ordenServicio = await _context.OrdenesServicio
                .Include(o => o.Detalles.Where(d => d.Activo))
                    .ThenInclude(d => d.Producto)
                .Include(o => o.Detalles.Where(d => d.Activo))
                    .ThenInclude(d => d.Servicio)
                .Include(o => o.Pagos.Where(p => p.Activo))
                .FirstOrDefaultAsync(o => o.Id == request.OrdenServicioId.Value && o.Activo);

            if (ordenServicio is null)
            {
                return ServiceResult<VentaDetalleResponse>.Invalid("La orden de servicio indicada no existe o está inactiva.");
            }

            if (ordenServicio.ClienteId != request.ClienteId)
            {
                return ServiceResult<VentaDetalleResponse>.Invalid("El cliente indicado no coincide con el cliente de la orden de servicio.");
            }

            var ventaOs = new Venta
            {
                ClienteId       = request.ClienteId,
                Cliente         = cliente,
                OrdenServicioId = ordenServicio.Id,
                OrdenServicio   = ordenServicio,
                Estado          = request.EsCotizacion ? EstadoVenta.Cotizacion : EstadoVenta.Confirmada,
                Fecha           = DateTime.UtcNow,
                FechaCreacion   = DateTime.UtcNow,
                Activo          = true
            };

            // Si no se pasaron detalles manuales, liquidamos los ítems vigentes de la OS
            if (request.Detalles is null || request.Detalles.Count == 0)
            {
                var detallesActivos = ordenServicio.Detalles.ToList();
                if (detallesActivos.Count == 0)
                {
                    return ServiceResult<VentaDetalleResponse>.Invalid("La orden de servicio no cuenta con ítems para liquidar.");
                }

                foreach (var det in detallesActivos)
                {
                    var itemVenta = new DetalleVenta
                    {
                        VentaId                 = ventaOs.Id,
                        Venta                   = ventaOs,
                        ProductoId              = det.ProductoId,
                        Producto                = det.Producto,
                        ServicioId              = det.ServicioId,
                        Servicio                = det.Servicio,
                        TipoItem                = det.TipoItem,
                        DetalleServicioOrigenId = det.Id,
                        Cantidad                = det.Cantidad,
                        PrecioUnitario          = det.PrecioUnitario,
                        CostoUnitarioHistorico  = det.CostoUnitarioHistorico,
                        TipoAfectacionIgv       = det.TipoAfectacionIgv,
                        SubtotalGravado         = det.SubtotalGravado,
                        PorcentajeIgvAplicado   = det.PorcentajeIgvAplicado,
                        MontoIgv                = det.MontoIgv,
                        Total                   = det.Total,
                        FechaCreacion           = DateTime.UtcNow,
                        Activo                  = true
                    };
                    ventaOs.Detalles.Add(itemVenta);
                    _context.DetallesVenta.Add(itemVenta);
                }

                ventaOs.SubtotalGravado   = ordenServicio.SubtotalGravado;
                ventaOs.SubtotalExonerado = ordenServicio.SubtotalExonerado;
                ventaOs.SubtotalInafecto  = ordenServicio.SubtotalInafecto;
                ventaOs.MontoIgv          = ordenServicio.MontoIgv;
                ventaOs.Total             = ordenServicio.Total;
            }
            else
            {
                // Detalles especificados manualmente para la OS
                foreach (var item in request.Detalles)
                {
                    if (item.Cantidad <= 0)
                    {
                        return ServiceResult<VentaDetalleResponse>.Invalid("La cantidad de cada producto debe ser mayor a 0.");
                    }

                    var prod = await _context.Productos.FirstOrDefaultAsync(p => p.Id == item.ProductoId && p.Activo);
                    if (prod is null)
                    {
                        return ServiceResult<VentaDetalleResponse>.Invalid($"El producto indicado ({item.ProductoId}) no existe o está inactivo.");
                    }

                    if (item.PrecioUnitario.HasValue && !puedeModificarPrecios && Math.Abs(item.PrecioUnitario.Value - prod.PrecioVenta) > 0.001m)
                    {
                        return ServiceResult<VentaDetalleResponse>.Invalid("No tiene permisos para modificar los precios de catálogo.");
                    }

                    if (item.Descuento.HasValue && item.Descuento.Value > 0 && !puedeAplicarDescuentos)
                    {
                        return ServiceResult<VentaDetalleResponse>.Invalid("No tiene permisos para aplicar descuentos.");
                    }

                    var precioUnitario = (item.PrecioUnitario.HasValue && puedeModificarPrecios)
                        ? item.PrecioUnitario.Value
                        : prod.PrecioVenta;

                    var subtotalGravado = item.Cantidad * precioUnitario;
                    var igvCalculado = Math.Round(subtotalGravado * (porcentajeIgv / 100m), 2, MidpointRounding.AwayFromZero);
                    var totalCalculado = subtotalGravado + igvCalculado;

                    var detVenta = new DetalleVenta
                    {
                        VentaId                = ventaOs.Id,
                        Venta                  = ventaOs,
                        ProductoId             = prod.Id,
                        Producto               = prod,
                        TipoItem               = TipoItemServicio.Repuesto,
                        Cantidad               = item.Cantidad,
                        PrecioUnitario         = precioUnitario,
                        CostoUnitarioHistorico = prod.Costo,
                        TipoAfectacionIgv      = TipoAfectacionIgv.Gravado,
                        SubtotalGravado        = subtotalGravado,
                        PorcentajeIgvAplicado  = porcentajeIgv,
                        MontoIgv               = igvCalculado,
                        Total                  = totalCalculado,
                        FechaCreacion          = DateTime.UtcNow,
                        Activo                 = true
                    };
                    ventaOs.Detalles.Add(detVenta);
                    _context.DetallesVenta.Add(detVenta);
                }

                ventaOs.SubtotalGravado   = ventaOs.Detalles.Sum(d => d.SubtotalGravado);
                ventaOs.SubtotalExonerado = ventaOs.Detalles.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Exonerado).Sum(d => d.Cantidad * d.PrecioUnitario);
                ventaOs.SubtotalInafecto  = ventaOs.Detalles.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Inafecto).Sum(d => d.Cantidad * d.PrecioUnitario);
                ventaOs.MontoIgv          = ventaOs.Detalles.Sum(d => d.MontoIgv);
                ventaOs.Total             = ventaOs.SubtotalGravado + ventaOs.SubtotalExonerado + ventaOs.SubtotalInafecto + ventaOs.MontoIgv;
            }

            // CRÍTICO: NO descontar stock de nuevo. Los repuestos ya fueron descontados al agregarse a la OS.
            // Vinculamos los pagos/anticipos ya existentes en la OS a esta Venta
            foreach (var pagoOs in ordenServicio.Pagos.Where(p => p.Activo && p.VentaId == null))
            {
                pagoOs.VentaId = ventaOs.Id;
                pagoOs.FechaModificacion = DateTime.UtcNow;
                ventaOs.Pagos.Add(pagoOs);
            }

            _context.Ventas.Add(ventaOs);
            await _context.SaveChangesAsync();

            return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(ventaOs));
        }

        // -------------------------------------------------------------------
        // CASO B: VENTA DE MOSTRADOR / DIRECTA (Sin Orden de Servicio)
        // -------------------------------------------------------------------
        if (request.Detalles is null || request.Detalles.Count == 0)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La venta debe contener al menos un detalle.");
        }

        if (request.Detalles.Any(d => d.Cantidad <= 0))
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La cantidad de cada producto debe ser mayor a 0.");
        }

        var cantidadesPorProducto = request.Detalles
            .GroupBy(d => d.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));

        var orderedProductIds = cantidadesPorProducto.Keys.OrderBy(id => id).ToList();

        // 1. Cotización directa (sin reserva ni descuento de inventario)
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
                OrdenServicioId = null,
                Estado          = EstadoVenta.Cotizacion,
                Fecha           = DateTime.UtcNow,
                FechaCreacion   = DateTime.UtcNow,
                Activo          = true
            };

            foreach (var item in request.Detalles)
            {
                var prod = productos[item.ProductoId];

                if (item.PrecioUnitario.HasValue && !puedeModificarPrecios && Math.Abs(item.PrecioUnitario.Value - prod.PrecioVenta) > 0.001m)
                {
                    return ServiceResult<VentaDetalleResponse>.Invalid("No tiene permisos para modificar los precios de catálogo.");
                }

                if (item.Descuento.HasValue && item.Descuento.Value > 0 && !puedeAplicarDescuentos)
                {
                    return ServiceResult<VentaDetalleResponse>.Invalid("No tiene permisos para aplicar descuentos.");
                }

                var precioUnitario = (item.PrecioUnitario.HasValue && puedeModificarPrecios)
                    ? item.PrecioUnitario.Value
                    : prod.PrecioVenta;

                var afectacion = item.TipoAfectacionIgv ?? TipoAfectacionIgv.Gravado;
                var subtotalItem = item.Cantidad * precioUnitario;
                var subtotalGravado = afectacion == TipoAfectacionIgv.Gravado ? subtotalItem : 0m;
                var igvCalculado = afectacion == TipoAfectacionIgv.Gravado
                    ? Math.Round(subtotalGravado * (porcentajeIgv / 100m), 2, MidpointRounding.AwayFromZero)
                    : 0m;
                var totalCalculado = subtotalItem + igvCalculado;

                var detalle = new DetalleVenta
                {
                    VentaId                = cotizacion.Id,
                    Venta                  = cotizacion,
                    ProductoId             = prod.Id,
                    Producto               = prod,
                    TipoItem               = TipoItemServicio.Repuesto,
                    Cantidad               = item.Cantidad,
                    PrecioUnitario         = precioUnitario,
                    CostoUnitarioHistorico = prod.Costo,
                    TipoAfectacionIgv      = afectacion,
                    SubtotalGravado        = subtotalGravado,
                    PorcentajeIgvAplicado  = afectacion == TipoAfectacionIgv.Gravado ? porcentajeIgv : 0m,
                    MontoIgv               = igvCalculado,
                    Total                  = totalCalculado,
                    FechaCreacion          = DateTime.UtcNow,
                    Activo                 = true
                };
                cotizacion.Detalles.Add(detalle);
                _context.DetallesVenta.Add(detalle);
            }

            cotizacion.SubtotalGravado   = cotizacion.Detalles.Sum(d => d.SubtotalGravado);
            cotizacion.SubtotalExonerado = cotizacion.Detalles.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Exonerado).Sum(d => d.Cantidad * d.PrecioUnitario);
            cotizacion.SubtotalInafecto  = cotizacion.Detalles.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Inafecto).Sum(d => d.Cantidad * d.PrecioUnitario);
            cotizacion.MontoIgv          = cotizacion.Detalles.Sum(d => d.MontoIgv);
            cotizacion.Total             = cotizacion.SubtotalGravado + cotizacion.SubtotalExonerado + cotizacion.SubtotalInafecto + cotizacion.MontoIgv;

            _context.Ventas.Add(cotizacion);
            await _context.SaveChangesAsync();
            return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(cotizacion));
        }

        // 2. Venta directa confirmada (Transaccional con bloqueo pesimista y descuento de inventario)
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

            if (productos.Count != orderedProductIds.Count || productos.Values.Any(p => !p.Activo))
            {
                return ServiceResult<VentaDetalleResponse>.Invalid("Uno o más productos no existen o están inactivos.");
            }

            // Validar suficiencia de stock bajo bloqueo
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
                OrdenServicioId = null,
                Estado          = EstadoVenta.Confirmada,
                Fecha           = DateTime.UtcNow,
                FechaCreacion   = DateTime.UtcNow,
                Activo          = true
            };
            _context.Ventas.Add(venta);

            foreach (var item in request.Detalles)
            {
                var prod = productos[item.ProductoId];

                if (item.PrecioUnitario.HasValue && !puedeModificarPrecios && Math.Abs(item.PrecioUnitario.Value - prod.PrecioVenta) > 0.001m)
                {
                    return ServiceResult<VentaDetalleResponse>.Invalid("No tiene permisos para modificar los precios de catálogo.");
                }

                if (item.Descuento.HasValue && item.Descuento.Value > 0 && !puedeAplicarDescuentos)
                {
                    return ServiceResult<VentaDetalleResponse>.Invalid("No tiene permisos para aplicar descuentos.");
                }

                var precioUnitario = (item.PrecioUnitario.HasValue && puedeModificarPrecios)
                    ? item.PrecioUnitario.Value
                    : prod.PrecioVenta;

                prod.StockActual -= item.Cantidad;
                prod.FechaModificacion = DateTime.UtcNow;

                var afectacion = item.TipoAfectacionIgv ?? TipoAfectacionIgv.Gravado;
                var subtotalItem = item.Cantidad * precioUnitario;
                var subtotalGravado = afectacion == TipoAfectacionIgv.Gravado ? subtotalItem : 0m;
                var igvCalculado = afectacion == TipoAfectacionIgv.Gravado
                    ? Math.Round(subtotalGravado * (porcentajeIgv / 100m), 2, MidpointRounding.AwayFromZero)
                    : 0m;
                var totalCalculado = subtotalItem + igvCalculado;

                var detalle = new DetalleVenta
                {
                    VentaId                = venta.Id,
                    Venta                  = venta,
                    ProductoId             = prod.Id,
                    Producto               = prod,
                    TipoItem               = TipoItemServicio.Repuesto,
                    Cantidad               = item.Cantidad,
                    PrecioUnitario         = precioUnitario,
                    CostoUnitarioHistorico = prod.Costo,
                    TipoAfectacionIgv      = afectacion,
                    SubtotalGravado        = subtotalGravado,
                    PorcentajeIgvAplicado  = afectacion == TipoAfectacionIgv.Gravado ? porcentajeIgv : 0m,
                    MontoIgv               = igvCalculado,
                    Total                  = totalCalculado,
                    FechaCreacion          = DateTime.UtcNow,
                    Activo                 = true
                };
                venta.Detalles.Add(detalle);
                _context.DetallesVenta.Add(detalle);

                var movimiento = new MovimientoInventario
                {
                    ProductoId    = prod.Id,
                    Tipo          = TipoMovimientoInventario.Salida,
                    Cantidad      = item.Cantidad,
                    Motivo        = $"Venta mostrador #{venta.Id}",
                    VentaId       = venta.Id,
                    FechaCreacion = DateTime.UtcNow,
                    Activo        = true
                };
                _context.MovimientosInventario.Add(movimiento);
            }

            venta.SubtotalGravado   = venta.Detalles.Sum(d => d.SubtotalGravado);
            venta.SubtotalExonerado = venta.Detalles.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Exonerado).Sum(d => d.Cantidad * d.PrecioUnitario);
            venta.SubtotalInafecto  = venta.Detalles.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Inafecto).Sum(d => d.Cantidad * d.PrecioUnitario);
            venta.MontoIgv          = venta.Detalles.Sum(d => d.MontoIgv);
            venta.Total             = venta.SubtotalGravado + venta.SubtotalExonerado + venta.SubtotalInafecto + venta.MontoIgv;

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

    public async Task<ServiceResult<VentaDetalleResponse>> ConfirmarCotizacionAsync(Guid id, Guid? soloClienteId = null)
    {
        var venta = await _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Producto)
            .Include(v => v.Pagos.Where(p => p.Activo))
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        if (venta is null)
        {
            return ServiceResult<VentaDetalleResponse>.NotFound();
        }

        if (soloClienteId.HasValue && venta.ClienteId != soloClienteId.Value)
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

        // Si la venta está ligada a una Orden de Servicio, el stock ya fue gestionado por la OS.
        if (venta.OrdenServicioId.HasValue)
        {
            venta.Estado = EstadoVenta.Confirmada;
            venta.FechaModificacion = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(venta));
        }

        // Venta directa: validar y descontar stock transaccionalmente
        var activeDetails = venta.Detalles.Where(d => d.Activo && d.ProductoId.HasValue).ToList();
        if (activeDetails.Count == 0)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La cotización no tiene detalles activos para confirmar.");
        }

        var cantidadesPorProducto = activeDetails
            .GroupBy(d => d.ProductoId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));

        var orderedProductIds = cantidadesPorProducto.Keys.OrderBy(pId => pId).ToList();

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
                var prod = productos[det.ProductoId!.Value];
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
            .Include(v => v.Pagos.Where(p => p.Activo))
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        if (venta is null)
        {
            return ServiceResult<VentaDetalleResponse>.NotFound();
        }

        if (venta.Estado == EstadoVenta.Anulada)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La venta ya se encuentra anulada.");
        }

        // Si era cotización o proviene de una OS, no reponemos stock en almacén de venta directa
        if (venta.Estado == EstadoVenta.Cotizacion || venta.OrdenServicioId.HasValue)
        {
            venta.Estado            = EstadoVenta.Anulada;
            venta.FechaModificacion = DateTime.UtcNow;

            if (venta.Comprobante != null && venta.Comprobante.Estado != "Anulado")
            {
                venta.Comprobante.Estado = "Anulado";
                venta.Comprobante.FechaModificacion = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(venta));
        }

        // Si era Confirmada de mostrador, reponer stock transaccionalmente
        var activeDetails = venta.Detalles.Where(d => d.Activo && d.ProductoId.HasValue).ToList();
        var orderedProductIds = activeDetails
            .Select(d => d.ProductoId!.Value)
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
                if (productos.TryGetValue(det.ProductoId!.Value, out var prod))
                {
                    prod.StockActual += det.Cantidad;
                    prod.FechaModificacion = DateTime.UtcNow;

                    var movimiento = new MovimientoInventario
                    {
                        ProductoId    = prod.Id,
                        Tipo          = TipoMovimientoInventario.Entrada,
                        Cantidad      = det.Cantidad,
                        Motivo        = $"Reposición por anulación de venta #{venta.Id}",
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

    public async Task<ServiceResult<ComprobanteResponse>> GetComprobanteAsync(Guid ventaId, Guid? soloClienteId = null)
    {
        var venta = await _context.Ventas
            .Include(v => v.Comprobante)
            .FirstOrDefaultAsync(v => v.Id == ventaId && v.Activo);

        if (venta is null)
        {
            return ServiceResult<ComprobanteResponse>.NotFound();
        }

        if (soloClienteId.HasValue && venta.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<ComprobanteResponse>.NotFound();
        }

        if (venta.Comprobante is null || !venta.Comprobante.Activo)
        {
            return ServiceResult<ComprobanteResponse>.NotFound();
        }

        return ServiceResult<ComprobanteResponse>.Success(MapToComprobanteResponse(venta.Comprobante));
    }

    public async Task<ServiceResult<ComprobanteResponse>> RegistrarComprobanteAsync(
        Guid ventaId, RegistrarComprobanteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Tipo) ||
            !Enum.TryParse<TipoComprobante>(request.Tipo.Trim(), ignoreCase: true, out var tipoEnum) ||
            !Enum.IsDefined(typeof(TipoComprobante), tipoEnum))
        {
            return ServiceResult<ComprobanteResponse>.Invalid("El tipo de comprobante no es válido. Solo se permite Boleta o Factura.");
        }

        var venta = await _context.Ventas
            .Include(v => v.Comprobante)
            .Include(v => v.Pagos.Where(p => p.Activo))
                .ThenInclude(p => p.MetodoPago)
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

        var config = await _context.ConfiguracionesEmpresa.FirstOrDefaultAsync();
        var porcentajeIgv = config?.PorcentajeIgv ?? 18.00m;

        var metodoPrincipal = request.MetodoPagoPrincipal
            ?? venta.Pagos.OrderByDescending(p => p.Monto).FirstOrDefault()?.MetodoPago?.Nombre;

        var serieDefecto = tipoEnum == TipoComprobante.Factura ? "F001" : "B001";
        var serieFinal = string.IsNullOrWhiteSpace(request.Serie) ? serieDefecto : request.Serie.Trim().ToUpperInvariant();
        string numeroFinal;
        if (!string.IsNullOrWhiteSpace(request.Numero))
        {
            numeroFinal = request.Numero.Trim();
        }
        else
        {
            var count = await _context.Comprobantes.CountAsync(c => c.Tipo == tipoEnum.ToString() && c.Serie == serieFinal);
            numeroFinal = (count + 1).ToString("D6");
        }

        var comprobante = new Comprobante
        {
            VentaId              = ventaId,
            Tipo                 = tipoEnum.ToString(),
            Serie                = serieFinal,
            Numero               = numeroFinal,
            Estado               = "Emitido",
            Activo               = true,
            FechaCreacion        = DateTime.UtcNow,
            SubtotalGravado      = venta.SubtotalGravado,
            SubtotalExonerado    = venta.SubtotalExonerado,
            SubtotalInafecto     = venta.SubtotalInafecto,
            PorcentajeIgv        = porcentajeIgv,
            MontoIgv             = venta.MontoIgv,
            Total                = venta.Total,
            MetodoPagoPrincipal  = metodoPrincipal,
            Observaciones        = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
            OrdenServicioId      = venta.OrdenServicioId
        };

        _context.Comprobantes.Add(comprobante);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
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

    private static VentaDetalleResponse MapToDetalleResponse(Venta v)
    {
        var totalPagado = v.Pagos.Where(p => p.Activo).Sum(p => p.Monto);
        var saldo = Math.Max(0m, v.Total - totalPagado);
        var estadoPago = saldo == 0m ? "Pagado" : (totalPagado > 0m ? "Parcial" : "Pendiente");

        return new VentaDetalleResponse(
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
                    d.Producto?.Codigo,
                    d.Producto?.Nombre ?? (d.Servicio?.Nombre ?? "Ítem"),
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.Cantidad * d.PrecioUnitario,
                    (int)d.TipoItem,
                    d.TipoItem.ToString(),
                    d.ServicioId,
                    d.CostoUnitarioHistorico,
                    (int)d.TipoAfectacionIgv,
                    d.TipoAfectacionIgv.ToString(),
                    d.SubtotalGravado,
                    d.PorcentajeIgvAplicado,
                    d.MontoIgv,
                    d.Total))
                .ToList(),
            v.Activo,
            v.Comprobante != null ? MapToComprobanteResponse(v.Comprobante) : null,
            v.SubtotalGravado,
            v.SubtotalExonerado,
            v.SubtotalInafecto,
            v.MontoIgv,
            totalPagado,
            saldo,
            estadoPago,
            v.Pagos
                .Where(p => p.Activo)
                .OrderBy(p => p.Fecha)
                .Select(p => new PagoResponse(
                    p.Id,
                    p.Monto,
                    p.MetodoPagoId,
                    p.MetodoPago?.Nombre ?? string.Empty,
                    p.MetodoPago?.Codigo ?? string.Empty,
                    p.Fecha,
                    p.Referencia,
                    p.EsAnticipo,
                    p.VentaId,
                    p.OrdenServicioId,
                    p.UsuarioId,
                    p.Usuario?.NombreCompleto,
                    p.Observaciones,
                    p.Activo))
                .ToList());
    }

    private static ComprobanteResponse MapToComprobanteResponse(Comprobante c) => new(
        c.Id,
        c.VentaId,
        c.Tipo,
        c.Serie,
        c.Numero,
        c.Estado,
        c.FechaCreacion,
        c.Activo,
        c.SubtotalGravado,
        c.SubtotalExonerado,
        c.SubtotalInafecto,
        c.PorcentajeIgv,
        c.MontoIgv,
        c.Total,
        c.MetodoPagoPrincipal,
        c.Observaciones,
        c.OrdenServicioId);
}
