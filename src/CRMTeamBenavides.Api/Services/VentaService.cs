using CRMTeamBenavides.Api.Features.Ventas;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class VentaService : IVentaService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public VentaService(ApplicationDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<VentaResponse>> GetAllAsync(
        Guid? clienteId,
        EstadoVenta? estado,
        Guid? ordenServicioId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        Guid? soloClienteId = null,
        bool? pendienteComprobante = null)
    {
        var query = _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.OrdenServicio)
            .Include(v => v.Detalles)
            .Include(v => v.Pagos)
            .Include(v => v.Comprobante)
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

        if (pendienteComprobante == true)
        {
            query = query.Where(v => v.Comprobante == null && v.Estado == EstadoVenta.Confirmada);
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
            var estadoComprobante = v.Comprobante == null ? "Pendiente" : v.Comprobante.Estado;

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
                estadoPago,
                estadoComprobante,
                (int)v.EstadoAprobacionGerencia,
                v.EstadoAprobacionGerencia.ToString(),
                v.OrdenServicio?.NumeroOrden);
        }).ToList();
    }

    public async Task<ServiceResult<VentaDetalleResponse>> GetByIdAsync(Guid id, Guid? soloClienteId = null)
    {
        var venta = await _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.OrdenServicio)
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
        Guid? soloClienteId = null,
        Guid? usuarioId = null)
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
            // La orden se bloquea mientras se liquida: dos liquidaciones simultáneas
            // no deben pasar las dos el control de venta vigente.
            await using var transaccionOs = await _context.Database.BeginTransactionAsync();
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"OrdenesServicio\" WHERE \"Id\" = {request.OrdenServicioId.Value} FOR UPDATE");

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

            // Se liquida el trabajo terminado: antes de «Lista» los ítems todavía cambian.
            if (ordenServicio.Estado is not (EstadoOrdenServicio.Lista or EstadoOrdenServicio.Entregada))
            {
                return ServiceResult<VentaDetalleResponse>.Invalid(
                    "La orden se liquida cuando está lista o entregada.");
            }

            // Una orden se liquida una sola vez: sus repuestos ya salieron del stock y
            // sus adelantos ya pasaron a esa venta. Para rehacerla, primero se anula.
            var yaLiquidada = await _context.Ventas.AnyAsync(v =>
                v.OrdenServicioId == ordenServicio.Id && v.Activo && v.Estado != EstadoVenta.Anulada);
            if (yaLiquidada)
            {
                return ServiceResult<VentaDetalleResponse>.Conflict(
                    "La orden de servicio ya tiene una venta vigente. Anúlala antes de liquidarla de nuevo.");
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

            bool requiereAprobacionOs = false;
            var detallesCambioOs = new List<object>();

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

                    var precioBase = prod.PrecioVenta;
                    var precioUnitario = item.PrecioUnitario ?? precioBase;

                    if (Math.Abs(precioUnitario - precioBase) > 0.001m || (item.Descuento.HasValue && item.Descuento.Value > 0))
                    {
                        requiereAprobacionOs = true;
                        detallesCambioOs.Add(new
                        {
                            ProductoId = prod.Id,
                            ProductoNombre = prod.Nombre,
                            PrecioBase = precioBase,
                            PrecioSolicitado = precioUnitario,
                            Descuento = item.Descuento ?? 0m
                        });
                    }

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

            if (requiereAprobacionOs)
            {
                ventaOs.EstadoAprobacionGerencia = EstadoAprobacionGerencia.Pendiente;
                _context.SolicitudesAprobacion.Add(new SolicitudAprobacion
                {
                    Id = Guid.NewGuid(),
                    Tipo = "CambioPrecio",
                    Entidad = "Venta",
                    EntidadId = ventaOs.Id.ToString(),
                    UsuarioSolicitanteId = usuarioId,
                    FechaSolicitud = DateTime.UtcNow,
                    Estado = EstadoAprobacionGerencia.Pendiente,
                    DetalleCambio = $"Modificación de precio al liquidar OS #{ordenServicio.Id}",
                    Motivo = "Modificación de precio al liquidar OS",
                    FechaCreacion = DateTime.UtcNow,
                    Activo = true
                });
            }
            else
            {
                ventaOs.EstadoAprobacionGerencia = ordenServicio.EstadoAprobacionGerencia;
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
            await transaccionOs.CommitAsync();

            await _auditoriaService.RegistrarEventoAsync(
                usuarioId,
                "Crear",
                "Venta",
                ventaOs.Id.ToString(),
                new
                {
                    Tipo = "LiquidacionOS",
                    OrdenServicioId = ventaOs.OrdenServicioId,
                    ClienteId = ventaOs.ClienteId,
                    Total = ventaOs.Total,
                    SubtotalGravado = ventaOs.SubtotalGravado,
                    Estado = ventaOs.Estado.ToString()
                });

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

            bool requiereAprobacionCotiz = false;
            var detallesCambioCotiz = new List<object>();

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

                var precioBase = prod.PrecioVenta;
                var precioUnitario = item.PrecioUnitario ?? precioBase;

                if (Math.Abs(precioUnitario - precioBase) > 0.001m || (item.Descuento.HasValue && item.Descuento.Value > 0))
                {
                    requiereAprobacionCotiz = true;
                    detallesCambioCotiz.Add(new
                    {
                        ProductoId = prod.Id,
                        ProductoNombre = prod.Nombre,
                        PrecioBase = precioBase,
                        PrecioSolicitado = precioUnitario,
                        Descuento = item.Descuento ?? 0m
                    });
                }

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

            if (requiereAprobacionCotiz)
            {
                cotizacion.EstadoAprobacionGerencia = EstadoAprobacionGerencia.Pendiente;
                _context.SolicitudesAprobacion.Add(new SolicitudAprobacion
                {
                    Id = Guid.NewGuid(),
                    Tipo = "CambioPrecio",
                    Entidad = "Venta",
                    EntidadId = cotizacion.Id.ToString(),
                    UsuarioSolicitanteId = usuarioId,
                    FechaSolicitud = DateTime.UtcNow,
                    Estado = EstadoAprobacionGerencia.Pendiente,
                    DetalleCambio = $"Modificación de precio en cotización ({detallesCambioCotiz.Count} ítems)",
                    Motivo = "Modificación de precio en cotización",
                    FechaCreacion = DateTime.UtcNow,
                    Activo = true
                });
            }
            else
            {
                cotizacion.EstadoAprobacionGerencia = EstadoAprobacionGerencia.NoAplica;
            }

            _context.Ventas.Add(cotizacion);
            await _context.SaveChangesAsync();

            await _auditoriaService.RegistrarEventoAsync(
                usuarioId,
                "Crear",
                "Venta",
                cotizacion.Id.ToString(),
                new
                {
                    Tipo = "CotizacionDirecta",
                    ClienteId = cotizacion.ClienteId,
                    Total = cotizacion.Total,
                    SubtotalGravado = cotizacion.SubtotalGravado,
                    Estado = cotizacion.Estado.ToString()
                });

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

            bool requiereAprobacionVenta = false;
            var detallesCambioVenta = new List<object>();

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

                var precioBase = prod.PrecioVenta;
                var precioUnitario = item.PrecioUnitario ?? precioBase;

                if (Math.Abs(precioUnitario - precioBase) > 0.001m || (item.Descuento.HasValue && item.Descuento.Value > 0))
                {
                    requiereAprobacionVenta = true;
                    detallesCambioVenta.Add(new
                    {
                        ProductoId = prod.Id,
                        ProductoNombre = prod.Nombre,
                        PrecioBase = precioBase,
                        PrecioSolicitado = precioUnitario,
                        Descuento = item.Descuento ?? 0m
                    });
                }

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

            if (requiereAprobacionVenta)
            {
                venta.EstadoAprobacionGerencia = EstadoAprobacionGerencia.Pendiente;
                _context.SolicitudesAprobacion.Add(new SolicitudAprobacion
                {
                    Id = Guid.NewGuid(),
                    Tipo = "CambioPrecio",
                    Entidad = "Venta",
                    EntidadId = venta.Id.ToString(),
                    UsuarioSolicitanteId = usuarioId,
                    FechaSolicitud = DateTime.UtcNow,
                    Estado = EstadoAprobacionGerencia.Pendiente,
                    DetalleCambio = $"Modificación de precio en venta directa ({detallesCambioVenta.Count} ítems)",
                    Motivo = "Modificación de precio en venta directa",
                    FechaCreacion = DateTime.UtcNow,
                    Activo = true
                });
            }
            else
            {
                venta.EstadoAprobacionGerencia = EstadoAprobacionGerencia.NoAplica;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _auditoriaService.RegistrarEventoAsync(
                usuarioId,
                "Crear",
                "Venta",
                venta.Id.ToString(),
                new
                {
                    Tipo = "VentaDirecta",
                    ClienteId = venta.ClienteId,
                    Total = venta.Total,
                    SubtotalGravado = venta.SubtotalGravado,
                    Estado = venta.Estado.ToString()
                });

            return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(venta));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult<VentaDetalleResponse>> ActualizarAsync(
        Guid id,
        ActualizarVentaRequest request,
        bool puedeModificarPrecios = false,
        bool puedeAplicarDescuentos = false,
        Guid? soloClienteId = null,
        Guid? usuarioId = null)
    {
        if (request.Detalles is null || request.Detalles.Count == 0)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La venta debe contener al menos un detalle.");
        }

        if (request.Detalles.Any(d => d.Cantidad <= 0))
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("La cantidad de cada producto debe ser mayor a 0.");
        }

        var venta = await _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Detalles.Where(d => d.Activo))
            .Include(v => v.Pagos.Where(p => p.Activo))
            .Include(v => v.Comprobante)
            .FirstOrDefaultAsync(v => v.Id == id && v.Activo);

        if (venta is null)
        {
            return ServiceResult<VentaDetalleResponse>.NotFound();
        }

        if (soloClienteId.HasValue && venta.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<VentaDetalleResponse>.NotFound();
        }

        if (venta.Estado != EstadoVenta.Cotizacion)
        {
            bool esVentaConfirmadaRecuperable = venta.Estado == EstadoVenta.Confirmada
                && (venta.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Rechazado || venta.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Pendiente)
                && !venta.Pagos.Any(p => p.Activo)
                && venta.Comprobante == null;

            if (!esVentaConfirmadaRecuperable)
            {
                return ServiceResult<VentaDetalleResponse>.Invalid(
                    "Solo se pueden modificar ventas en estado Cotización o ventas confirmadas con aprobación de precios pendiente o rechazada y sin pagos ni comprobantes emitidos.");
            }
        }

        if (venta.Pagos.Any(p => p.Activo))
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("No se puede modificar una venta que ya registra pagos.");
        }

        if (venta.Comprobante != null)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("No se puede modificar una venta que ya cuenta con comprobante emitido.");
        }

        var config = await _context.ConfiguracionesEmpresa.FirstOrDefaultAsync();
        var porcentajeIgv = config?.PorcentajeIgv ?? 18.00m;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var oldDetallesActivos = venta.Detalles.Where(d => d.Activo).ToList();
            var oldCantidades = oldDetallesActivos
                .Where(d => d.ProductoId != null)
                .GroupBy(d => d.ProductoId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));

            var newCantidades = request.Detalles
                .GroupBy(d => d.ProductoId)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));

            var allProductIds = oldCantidades.Keys.Union(newCantidades.Keys).OrderBy(pid => pid).ToList();

            if (venta.Estado == EstadoVenta.Confirmada)
            {
                foreach (var prodId in allProductIds)
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {prodId} FOR UPDATE");
                }
            }

            var productos = await _context.Productos
                .Where(p => allProductIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            if (newCantidades.Keys.Any(pid => !productos.ContainsKey(pid) || !productos[pid].Activo))
            {
                await transaction.RollbackAsync();
                return ServiceResult<VentaDetalleResponse>.Invalid("Uno o más productos no existen o están inactivos.");
            }

            // Si es venta confirmada, ajustar inventario según diferencia de cantidades
            if (venta.Estado == EstadoVenta.Confirmada)
            {
                foreach (var prodId in allProductIds)
                {
                    var prod = productos[prodId];
                    int oldQty = oldCantidades.GetValueOrDefault(prodId, 0);
                    int newQty = newCantidades.GetValueOrDefault(prodId, 0);
                    int diff = newQty - oldQty;

                    if (diff > 0)
                    {
                        if (prod.StockActual < diff)
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult<VentaDetalleResponse>.Invalid(
                                $"Stock insuficiente para el producto '{prod.Nombre}'. Stock disponible: {prod.StockActual}, solicitado adicional: {diff}.");
                        }
                        prod.StockActual -= diff;
                        prod.FechaModificacion = DateTime.UtcNow;
                        _context.MovimientosInventario.Add(new MovimientoInventario
                        {
                            ProductoId    = prod.Id,
                            Tipo          = TipoMovimientoInventario.Salida,
                            Cantidad      = diff,
                            Motivo        = $"Ajuste por incremento de cantidad en venta mostrador #{venta.Id}",
                            VentaId       = venta.Id,
                            FechaCreacion = DateTime.UtcNow,
                            Activo        = true
                        });
                    }
                    else if (diff < 0)
                    {
                        int devolucion = Math.Abs(diff);
                        prod.StockActual += devolucion;
                        prod.FechaModificacion = DateTime.UtcNow;
                        _context.MovimientosInventario.Add(new MovimientoInventario
                        {
                            ProductoId    = prod.Id,
                            Tipo          = TipoMovimientoInventario.Entrada,
                            Cantidad      = devolucion,
                            Motivo        = $"Ajuste por reducción o eliminación de ítem en venta mostrador #{venta.Id}",
                            VentaId       = venta.Id,
                            FechaCreacion = DateTime.UtcNow,
                            Activo        = true
                        });
                    }
                }
            }

            foreach (var det in oldDetallesActivos)
            {
                det.Activo = false;
                det.FechaModificacion = DateTime.UtcNow;
            }

            bool requiereAprobacion = false;
            var detallesCambio = new List<object>();

            decimal subtotalGravadoTotal = 0m;
            decimal subtotalExoneradoTotal = 0m;
            decimal subtotalInafectoTotal = 0m;
            decimal montoIgvTotal = 0m;

            foreach (var item in request.Detalles)
            {
                var prod = productos[item.ProductoId];
                var precioBase = prod.PrecioVenta;
                var precioUnitario = item.PrecioUnitario ?? precioBase;

                bool precioModificado = Math.Abs(precioUnitario - precioBase) > 0.001m;
                bool tieneDescuento = item.Descuento.HasValue && item.Descuento.Value > 0;

                if (precioModificado || tieneDescuento)
                {
                    requiereAprobacion = true;
                    detallesCambio.Add(new
                    {
                        ProductoId = prod.Id,
                        ProductoNombre = prod.Nombre,
                        PrecioBase = precioBase,
                        PrecioSolicitado = precioUnitario,
                        Descuento = item.Descuento ?? 0m
                    });
                }

                var afectacion = item.TipoAfectacionIgv ?? TipoAfectacionIgv.Gravado;
                var subtotalItem = item.Cantidad * precioUnitario;
                var subtotalGravado = afectacion == TipoAfectacionIgv.Gravado ? subtotalItem : 0m;
                var igvCalculado = afectacion == TipoAfectacionIgv.Gravado
                    ? decimal.Round(subtotalItem * (porcentajeIgv / 100m), 2, MidpointRounding.AwayFromZero)
                    : 0m;
                var totalLinea = decimal.Round(subtotalItem + igvCalculado, 2, MidpointRounding.AwayFromZero);

                subtotalGravadoTotal += subtotalGravado;
                if (afectacion == TipoAfectacionIgv.Exonerado) subtotalExoneradoTotal += subtotalItem;
                if (afectacion == TipoAfectacionIgv.Inafecto) subtotalInafectoTotal += subtotalItem;
                montoIgvTotal += igvCalculado;

                var nuevoDetalle = new DetalleVenta
                {
                    VentaId = venta.Id,
                    ProductoId = prod.Id,
                    Producto = prod,
                    TipoItem = TipoItemServicio.Repuesto,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = precioUnitario,
                    CostoUnitarioHistorico = prod.Costo,
                    TipoAfectacionIgv = afectacion,
                    SubtotalGravado = subtotalGravado,
                    PorcentajeIgvAplicado = afectacion == TipoAfectacionIgv.Gravado ? porcentajeIgv : 0m,
                    MontoIgv = igvCalculado,
                    Total = totalLinea,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };
                venta.Detalles.Add(nuevoDetalle);
                _context.DetallesVenta.Add(nuevoDetalle);
            }

            venta.SubtotalGravado = decimal.Round(subtotalGravadoTotal, 2, MidpointRounding.AwayFromZero);
            venta.SubtotalExonerado = decimal.Round(subtotalExoneradoTotal, 2, MidpointRounding.AwayFromZero);
            venta.SubtotalInafecto = decimal.Round(subtotalInafectoTotal, 2, MidpointRounding.AwayFromZero);
            venta.MontoIgv = decimal.Round(montoIgvTotal, 2, MidpointRounding.AwayFromZero);
            venta.Total = decimal.Round(venta.SubtotalGravado + venta.SubtotalExonerado + venta.SubtotalInafecto + venta.MontoIgv, 2, MidpointRounding.AwayFromZero);
            venta.FechaModificacion = DateTime.UtcNow;

            if (requiereAprobacion)
            {
                await AprobacionService.RetirarSolicitudesPendientesPorObjetivoAsync(
                    _context,
                    "Venta",
                    venta.Id.ToString(),
                    "entidad_venta",
                    "Superada automáticamente por nueva modificación de precios.");

                venta.EstadoAprobacionGerencia = EstadoAprobacionGerencia.Pendiente;
                _context.SolicitudesAprobacion.Add(new SolicitudAprobacion
                {
                    Id = Guid.NewGuid(),
                    Tipo = "CambioPrecio",
                    Entidad = "Venta",
                    EntidadId = venta.Id.ToString(),
                    UsuarioSolicitanteId = usuarioId,
                    FechaSolicitud = DateTime.UtcNow,
                    Estado = EstadoAprobacionGerencia.Pendiente,
                    DetalleCambio = System.Text.Json.JsonSerializer.Serialize(detallesCambio),
                    ValorAnterior = 0m,
                    ValorSolicitado = venta.Total,
                    Motivo = venta.Estado == EstadoVenta.Confirmada
                        ? "Modificación de precios en venta mostrador corregida"
                        : "Modificación de precios en cotización actualizada",
                    FechaCreacion = DateTime.UtcNow,
                    Activo = true
                });
            }
            else
            {
                if (venta.EstadoAprobacionGerencia is EstadoAprobacionGerencia.Pendiente or EstadoAprobacionGerencia.Rechazado)
                {
                    venta.EstadoAprobacionGerencia = EstadoAprobacionGerencia.NoAplica;
                    venta.ObservacionesAprobacionGerencia = null;
                }

                var solicitudesPendientes = await _context.SolicitudesAprobacion
                    .Where(s => s.Entidad == "Venta" && s.EntidadId == venta.Id.ToString() && s.Estado == EstadoAprobacionGerencia.Pendiente && s.Activo)
                    .ToListAsync();

                foreach (var sol in solicitudesPendientes)
                {
                    sol.Activo = false;
                    sol.ObservacionesRespuesta = "Retirada automáticamente por restablecimiento de precios a catálogo.";
                    sol.FechaRespuesta = DateTime.UtcNow;
                    sol.FechaModificacion = DateTime.UtcNow;
                }
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

        if (venta.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Pendiente)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid(
                "No se puede confirmar la cotización porque tiene modificaciones de precio pendientes de aprobación por Gerencia.");
        }

        if (venta.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Rechazado)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid(
                "No se puede confirmar la cotización porque la modificación de precios fue rechazada por Gerencia.");
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

    public async Task<ServiceResult<VentaDetalleResponse>> AnularAsync(Guid id, Guid? usuarioId = null)
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

        var estadoAnterior = venta.Estado.ToString();

        var solicitudesPendientes = await _context.SolicitudesAprobacion
            .Where(s => s.Entidad == "Venta" && s.EntidadId == venta.Id.ToString() && s.Estado == EstadoAprobacionGerencia.Pendiente && s.Activo)
            .ToListAsync();

        foreach (var sol in solicitudesPendientes)
        {
            sol.Activo = false;
            sol.ObservacionesRespuesta = "Cancelada automáticamente por anulación de la venta/cotización.";
            sol.FechaRespuesta = DateTime.UtcNow;
            sol.FechaModificacion = DateTime.UtcNow;
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

            await _auditoriaService.RegistrarEventoAsync(
                usuarioId,
                "Anular",
                "Venta",
                venta.Id.ToString(),
                new
                {
                    ValoresAnteriores = new { Estado = estadoAnterior },
                    ValoresNuevos = new { Estado = EstadoVenta.Anulada.ToString() },
                    Total = venta.Total,
                    OrdenServicioId = venta.OrdenServicioId
                });

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

            await _auditoriaService.RegistrarEventoAsync(
                usuarioId,
                "Anular",
                "Venta",
                venta.Id.ToString(),
                new
                {
                    ValoresAnteriores = new { Estado = estadoAnterior },
                    ValoresNuevos = new { Estado = EstadoVenta.Anulada.ToString() },
                    Total = venta.Total,
                    OrdenServicioId = venta.OrdenServicioId
                });

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
        Guid ventaId, RegistrarComprobanteRequest request, Guid? usuarioId = null)
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

        if (venta.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Pendiente)
        {
            return ServiceResult<ComprobanteResponse>.Invalid(
                "No se puede emitir comprobante mientras la venta tenga modificaciones de precio pendientes de aprobación por Gerencia.");
        }

        if (venta.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Rechazado)
        {
            return ServiceResult<ComprobanteResponse>.Invalid(
                "No se puede emitir comprobante mientras la venta tenga la aprobación gerencial rechazada.");
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

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            string numeroFinal;
            if (!string.IsNullOrWhiteSpace(request.Numero))
            {
                numeroFinal = request.Numero.Trim();
            }
            else
            {
                // Serializar generación de correlativos por (Tipo, Serie) usando pg_advisory_xact_lock
                // a nivel de transacción PostgreSQL para evitar números duplicados concurrentes
                var lockKey = $"comprobante_{tipoEnum.ToString().ToLowerInvariant()}_{serieFinal.ToLowerInvariant()}";
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))");

                var numerosExistentes = await _context.Comprobantes
                    .Where(c => c.Tipo == tipoEnum.ToString() && c.Serie == serieFinal)
                    .Select(c => c.Numero)
                    .ToListAsync();

                int maxNumero = 0;
                foreach (var numStr in numerosExistentes)
                {
                    if (int.TryParse(numStr, out var numVal) && numVal > maxNumero)
                    {
                        maxNumero = numVal;
                    }
                }

                numeroFinal = (maxNumero + 1).ToString("D6");
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
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                return ServiceResult<ComprobanteResponse>.Invalid("La venta ya cuenta con un comprobante registrado.");
            }

            await _auditoriaService.RegistrarEventoAsync(
                usuarioId,
                "Crear",
                "Comprobante",
                comprobante.Id.ToString(),
                new
                {
                    VentaId = ventaId,
                    Tipo = comprobante.Tipo,
                    Serie = comprobante.Serie,
                    Numero = comprobante.Numero,
                    Total = comprobante.Total,
                    MetodoPagoPrincipal = comprobante.MetodoPagoPrincipal
                });

            return ServiceResult<ComprobanteResponse>.Success(MapToComprobanteResponse(comprobante));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult<ComprobanteResponse>> AnularComprobanteAsync(Guid ventaId, Guid? usuarioId = null)
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

        await _auditoriaService.RegistrarEventoAsync(
            usuarioId,
            "Anular",
            "Comprobante",
            venta.Comprobante.Id.ToString(),
            new
            {
                VentaId = ventaId,
                ValoresAnteriores = new { Estado = "Emitido" },
                ValoresNuevos = new { Estado = "Anulado" }
            });

        return ServiceResult<ComprobanteResponse>.Success(MapToComprobanteResponse(venta.Comprobante));
    }

    public async Task<ServiceResult<VentaDetalleResponse>> AprobacionGerenciaAsync(
        Guid ventaId,
        CRMTeamBenavides.Api.Features.OrdenesServicio.AprobacionGerenciaRequest request,
        Guid? usuarioId = null)
    {
        var venta = await _context.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Comprobante)
            .Include(v => v.Detalles.Where(d => d.Activo))
                .ThenInclude(d => d.Producto)
            .Include(v => v.Pagos.Where(p => p.Activo))
            .FirstOrDefaultAsync(v => v.Id == ventaId && v.Activo);

        if (venta is null)
        {
            return ServiceResult<VentaDetalleResponse>.NotFound();
        }

        if (venta.Estado == EstadoVenta.Anulada)
        {
            return ServiceResult<VentaDetalleResponse>.Invalid("No se puede modificar la aprobación de una venta anulada.");
        }

        venta.EstadoAprobacionGerencia = request.Estado;
        venta.FechaAprobacionGerencia = DateTime.UtcNow;
        venta.UsuarioAprobacionGerenciaId = usuarioId;
        venta.ObservacionesAprobacionGerencia = request.Observaciones?.Trim();
        venta.FechaModificacion = DateTime.UtcNow;

        var solicitudes = await _context.SolicitudesAprobacion
            .Where(s => s.Entidad == "Venta" && s.EntidadId == ventaId.ToString() && s.Estado == EstadoAprobacionGerencia.Pendiente)
            .ToListAsync();

        foreach (var s in solicitudes)
        {
            s.Estado = request.Estado;
            s.UsuarioAprobadorId = usuarioId;
            s.FechaRespuesta = DateTime.UtcNow;
            s.ObservacionesRespuesta = request.Observaciones?.Trim();
            s.FechaModificacion = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarEventoAsync(
            usuarioId,
            request.Estado == EstadoAprobacionGerencia.Aprobado ? "AprobacionGerencia" : "RechazoGerencia",
            "Venta",
            venta.Id.ToString(),
            new
            {
                Estado = request.Estado.ToString(),
                Observaciones = request.Observaciones?.Trim(),
                Total = venta.Total
            });

        return ServiceResult<VentaDetalleResponse>.Success(MapToDetalleResponse(venta));
    }

    private static VentaDetalleResponse MapToDetalleResponse(Venta v)
    {
        var totalPagado = v.Pagos.Where(p => p.Activo).Sum(p => p.Monto);
        var saldo = Math.Max(0m, v.Total - totalPagado);
        var estadoPago = saldo == 0m ? "Pagado" : (totalPagado > 0m ? "Parcial" : "Pendiente");
        var estadoComprobante = v.Comprobante == null ? "Pendiente" : v.Comprobante.Estado;

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
                .ToList(),
            estadoComprobante,
            (int)v.EstadoAprobacionGerencia,
            v.EstadoAprobacionGerencia.ToString(),
            v.OrdenServicio?.NumeroOrden);
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
