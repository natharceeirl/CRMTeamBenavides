using CRMTeamBenavides.Api.Features.PedidosLima;
using CRMTeamBenavides.Api.Services.Exportacion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class PedidoLimaService : IPedidoLimaService
{
    private readonly ApplicationDbContext _context;
    private readonly IExportacionExcelService _excelService;
    private readonly IAuditoriaService _auditoriaService;

    public PedidoLimaService(
        ApplicationDbContext context,
        IExportacionExcelService excelService,
        IAuditoriaService auditoriaService)
    {
        _context = context;
        _excelService = excelService;
        _auditoriaService = auditoriaService;
    }


    private async Task<string> GenerarNumeroPedidoAsync()
    {
        var seqVal = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('\"PedidoLimaNumeroSeq\"') AS \"Value\"")
            .SingleAsync();
        return $"PL-{seqVal:D6}";
    }

    private static DateTime NormalizarUtc(DateTime fecha)
    {
        return fecha.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(fecha, DateTimeKind.Utc),
            DateTimeKind.Local => fecha.ToUniversalTime(),
            _ => fecha
        };
    }

    public async Task<List<PedidoLimaResponse>> GetAllAsync(
        Guid? soloClienteId = null,
        Guid? clienteId = null,
        EstadoPedidoLima? estado = null,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        string? guia = null,
        CancellationToken ct = default)
    {
        var query = _context.PedidosLima
            .AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(p => p.HistorialEstados.OrderByDescending(h => h.Fecha))
                .ThenInclude(h => h.Usuario)
            .Include(p => p.Pagos.Where(pay => pay.Activo))
                .ThenInclude(pay => pay.MetodoPago)
            .Include(p => p.Pagos.Where(pay => pay.Activo))
                .ThenInclude(pay => pay.Usuario)
            .Where(p => p.Activo);

        if (soloClienteId.HasValue)
        {
            query = query.Where(p => p.ClienteId == soloClienteId.Value);
        }
        else if (clienteId.HasValue)
        {
            query = query.Where(p => p.ClienteId == clienteId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(p => p.Estado == estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(guia))
        {
            var guiaFiltro = guia.Trim().ToLower();
            query = query.Where(p => p.NumeroGuia != null && p.NumeroGuia.ToLower().Contains(guiaFiltro));
        }

        if (fechaInicio.HasValue)
        {
            var inicioUtc = NormalizarUtc(fechaInicio.Value);
            query = query.Where(p => p.Fecha >= inicioUtc);
        }

        if (fechaFin.HasValue)
        {
            var finUtc = NormalizarUtc(fechaFin.Value);
            query = query.Where(p => p.Fecha <= finUtc);
        }

        var pedidos = await query
            .OrderByDescending(p => p.Fecha)
            .ToListAsync(ct);

        return pedidos.Select(MapToResponse).ToList();
    }

    public async Task<ServiceResult<PedidoLimaResponse>> GetByIdAsync(
        Guid id,
        Guid? soloClienteId = null,
        CancellationToken ct = default)
    {
        var pedido = await _context.PedidosLima
            .AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(p => p.HistorialEstados.OrderByDescending(h => h.Fecha))
                .ThenInclude(h => h.Usuario)
            .Include(p => p.Pagos.Where(pay => pay.Activo))
                .ThenInclude(pay => pay.MetodoPago)
            .Include(p => p.Pagos.Where(pay => pay.Activo))
                .ThenInclude(pay => pay.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

        if (pedido is null)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        if (soloClienteId.HasValue && pedido.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        return ServiceResult<PedidoLimaResponse>.Success(MapToResponse(pedido));
    }

    public async Task<ServiceResult<PedidoLimaResponse>> CrearAsync(
        CrearPedidoLimaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        bool puedeModificarPrecios = false,
        CancellationToken ct = default)
    {
        var clienteEfectivoId = soloClienteId ?? request.ClienteId;
        if (!clienteEfectivoId.HasValue)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("Debe especificar el cliente para el pedido.");
        }

        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clienteEfectivoId.Value && c.Activo, ct);

        if (cliente is null)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("El cliente especificado no existe o está inactivo.");
        }

        if (request.Detalles is null || request.Detalles.Count == 0)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("El pedido debe contener al menos un detalle de producto.");
        }

        if (request.Detalles.Any(d => d.Cantidad <= 0))
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("La cantidad de cada ítem debe ser mayor a 0.");
        }

        var productIds = request.Detalles.Select(d => d.ProductoId).Distinct().ToList();
        var productos = await _context.Productos
            .Where(p => productIds.Contains(p.Id) && p.Activo)
            .ToDictionaryAsync(p => p.Id, ct);

        if (productos.Count != productIds.Count)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("Uno o más productos solicitados no existen o están inactivos.");
        }

        var config = await _context.ConfiguracionesEmpresa.AsNoTracking().FirstOrDefaultAsync(ct);
        var porcentajeIgv = config?.PorcentajeIgv ?? 18.00m;

        var numeroPedido = await GenerarNumeroPedidoAsync();

        var pedido = new PedidoLima
        {
            NumeroPedido = numeroPedido,
            ClienteId = clienteEfectivoId.Value,
            Fecha = DateTime.UtcNow,
            Estado = EstadoPedidoLima.Pendiente,
            EmpresaTransporte = request.EmpresaTransporte?.Trim(),
            NumeroGuia = request.NumeroGuia?.Trim(),
            FechaEstimadaLlegada = request.FechaEstimadaLlegada.HasValue ? NormalizarUtc(request.FechaEstimadaLlegada.Value) : null,
            Observaciones = request.Observaciones?.Trim(),
            StockDeducido = false,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        decimal subtotalGravado = 0m;
        decimal subtotalExonerado = 0m;
        decimal subtotalInafecto = 0m;
        decimal montoIgv = 0m;
        bool precioModificado = false;
        string? detalleModificacion = null;
        decimal? valorBase = null;
        decimal? valorSolicitado = null;

        foreach (var item in request.Detalles)
        {
            var prod = productos[item.ProductoId];
            var precioUnitario = item.PrecioUnitario.HasValue
                ? item.PrecioUnitario.Value
                : prod.PrecioVenta;

            if (Math.Abs(precioUnitario - prod.PrecioVenta) > 0.001m)
            {
                precioModificado = true;
                valorBase = prod.PrecioVenta;
                valorSolicitado = precioUnitario;
                detalleModificacion = $"Modificación de precio en repuesto '{prod.Nombre}': base S/ {prod.PrecioVenta:F2} -> solicitado S/ {precioUnitario:F2}";
            }

            var afectacion = item.TipoAfectacionIgv ?? TipoAfectacionIgv.Gravado;
            var subtotalItem = decimal.Round(item.Cantidad * precioUnitario, 2, MidpointRounding.AwayFromZero);

            decimal lineaGravada = 0m;
            decimal lineaIgv = 0m;
            decimal lineaTotal = subtotalItem;
            decimal porcentajeAplicado = 0m;

            if (afectacion == TipoAfectacionIgv.Gravado)
            {
                lineaGravada = subtotalItem;
                lineaIgv = decimal.Round(lineaGravada * (porcentajeIgv / 100m), 2, MidpointRounding.AwayFromZero);
                lineaTotal = lineaGravada + lineaIgv;
                porcentajeAplicado = porcentajeIgv;

                subtotalGravado += lineaGravada;
                montoIgv += lineaIgv;
            }
            else if (afectacion == TipoAfectacionIgv.Exonerado)
            {
                subtotalExonerado += subtotalItem;
            }
            else
            {
                subtotalInafecto += subtotalItem;
            }

            var detalle = new DetallePedidoLima
            {
                PedidoLimaId = pedido.Id,
                ProductoId = prod.Id,
                Cantidad = item.Cantidad,
                PrecioUnitario = precioUnitario,
                CostoUnitarioHistorico = prod.Costo,
                TipoAfectacionIgv = afectacion,
                SubtotalGravado = lineaGravada,
                PorcentajeIgvAplicado = porcentajeAplicado,
                MontoIgv = lineaIgv,
                Total = lineaTotal,
                CreadoPorId = usuarioId,
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            };

            pedido.Detalles.Add(detalle);
        }

        pedido.SubtotalGravado = subtotalGravado;
        pedido.SubtotalExonerado = subtotalExonerado;
        pedido.SubtotalInafecto = subtotalInafecto;
        pedido.PorcentajeIgv = porcentajeIgv;
        pedido.MontoIgv = montoIgv;
        pedido.Total = subtotalGravado + subtotalExonerado + subtotalInafecto + montoIgv;

        if (precioModificado)
        {
            pedido.EstadoAprobacionGerencia = EstadoAprobacionGerencia.Pendiente;
            _context.SolicitudesAprobacion.Add(new SolicitudAprobacion
            {
                Id = Guid.NewGuid(),
                Tipo = "CambioPrecio",
                Entidad = "PedidoLima",
                EntidadId = pedido.Id.ToString(),
                UsuarioSolicitanteId = usuarioId,
                FechaSolicitud = DateTime.UtcNow,
                Estado = EstadoAprobacionGerencia.Pendiente,
                DetalleCambio = detalleModificacion ?? "Modificación de precio en Pedido Lima",
                ValorAnterior = valorBase,
                ValorSolicitado = valorSolicitado,
                Motivo = "Modificación de precio en Pedido Lima",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            });
        }

        var historial = new HistorialEstadoPedidoLima
        {
            PedidoLimaId = pedido.Id,
            EstadoAnterior = null,
            EstadoNuevo = EstadoPedidoLima.Pendiente,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Observacion = "Pedido registrado inicialmente",
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        _context.HistorialEstadosPedidoLima.Add(historial);
        _context.PedidosLima.Add(pedido);
        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(pedido.Id, soloClienteId, ct);
    }

    public async Task<ServiceResult<PedidoLimaResponse>> ActualizarAsync(
        Guid id,
        ActualizarPedidoLimaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default)
    {
        var pedido = await _context.PedidosLima
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

        if (pedido is null)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        if (soloClienteId.HasValue && pedido.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        if (pedido.Estado is EstadoPedidoLima.Cancelado or EstadoPedidoLima.Entregado)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid($"No se puede modificar un pedido en estado {pedido.Estado}.");
        }

        pedido.EmpresaTransporte = request.EmpresaTransporte?.Trim() ?? pedido.EmpresaTransporte;
        pedido.NumeroGuia = request.NumeroGuia?.Trim() ?? pedido.NumeroGuia;
        if (request.FechaEstimadaLlegada.HasValue)
        {
            pedido.FechaEstimadaLlegada = NormalizarUtc(request.FechaEstimadaLlegada.Value);
        }
        if (request.FechaLlegada.HasValue)
        {
            pedido.FechaLlegada = NormalizarUtc(request.FechaLlegada.Value);
        }
        if (request.FechaEntrega.HasValue)
        {
            pedido.FechaEntrega = NormalizarUtc(request.FechaEntrega.Value);
        }
        if (request.Observaciones is not null)
        {
            pedido.Observaciones = request.Observaciones.Trim();
        }

        pedido.ModificadoPorId = usuarioId;
        pedido.FechaModificacion = DateTime.UtcNow;

        _context.HistorialEstadosPedidoLima.Add(new HistorialEstadoPedidoLima
        {
            PedidoLimaId = pedido.Id,
            EstadoAnterior = pedido.Estado,
            EstadoNuevo = pedido.Estado,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Observacion = "Datos de logística y seguimiento actualizados",
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        });

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(pedido.Id, soloClienteId, ct);
    }

    public async Task<ServiceResult<PedidoLimaResponse>> ActualizarPrecioDetalleAsync(
        Guid id,
        Guid detalleId,
        ActualizarPrecioDetallePedidoLimaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        bool puedeModificarPrecios = false,
        CancellationToken ct = default)
    {
        var pedido = await _context.PedidosLima
            .Include(p => p.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

        if (pedido is null)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        if (soloClienteId.HasValue && pedido.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        if (pedido.Estado is EstadoPedidoLima.Cancelado or EstadoPedidoLima.Entregado)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid($"No se puede modificar un pedido en estado {pedido.Estado}.");
        }

        var detalle = pedido.Detalles.FirstOrDefault(d => d.Id == detalleId && d.Activo);
        if (detalle is null)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Detalle de pedido no encontrado.");
        }

        if (request.PrecioUnitario < 0)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("El precio unitario no puede ser negativo.");
        }

        var prod = detalle.Producto ?? await _context.Productos.FirstOrDefaultAsync(p => p.Id == detalle.ProductoId, ct);
        if (prod is null)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("El producto asociado al detalle no existe.");
        }

        var precioBase = prod.PrecioVenta;
        var nuevoPrecio = request.PrecioUnitario;
        bool precioDifiere = Math.Abs(nuevoPrecio - precioBase) > 0.001m;

        detalle.PrecioUnitario = nuevoPrecio;
        if (request.Cantidad.HasValue && request.Cantidad.Value > 0)
        {
            detalle.Cantidad = request.Cantidad.Value;
        }

        var config = await _context.ConfiguracionesEmpresa.FirstOrDefaultAsync(ct);
        var porcentajeIgv = config?.PorcentajeIgv ?? 18.00m;

        var subtotal = detalle.Cantidad * detalle.PrecioUnitario;
        if (detalle.TipoAfectacionIgv == TipoAfectacionIgv.Gravado)
        {
            detalle.SubtotalGravado = subtotal;
            detalle.PorcentajeIgvAplicado = porcentajeIgv;
            detalle.MontoIgv = decimal.Round(subtotal * (porcentajeIgv / 100m), 2, MidpointRounding.AwayFromZero);
            detalle.Total = decimal.Round(subtotal + detalle.MontoIgv, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            detalle.SubtotalGravado = 0m;
            detalle.PorcentajeIgvAplicado = 0m;
            detalle.MontoIgv = 0m;
            detalle.Total = subtotal;
        }
        detalle.FechaModificacion = DateTime.UtcNow;

        pedido.SubtotalGravado = pedido.Detalles.Where(d => d.Activo && d.TipoAfectacionIgv == TipoAfectacionIgv.Gravado).Sum(d => d.SubtotalGravado);
        pedido.SubtotalExonerado = pedido.Detalles.Where(d => d.Activo && d.TipoAfectacionIgv == TipoAfectacionIgv.Exonerado).Sum(d => d.Total);
        pedido.SubtotalInafecto = pedido.Detalles.Where(d => d.Activo && d.TipoAfectacionIgv == TipoAfectacionIgv.Inafecto).Sum(d => d.Total);
        pedido.MontoIgv = pedido.Detalles.Where(d => d.Activo).Sum(d => d.MontoIgv);
        pedido.Total = pedido.SubtotalGravado + pedido.SubtotalExonerado + pedido.SubtotalInafecto + pedido.MontoIgv;
        pedido.ModificadoPorId = usuarioId;
        pedido.FechaModificacion = DateTime.UtcNow;

        var prodIds = pedido.Detalles.Where(d => d.Activo).Select(d => d.ProductoId).Distinct().ToList();
        var productosBase = await _context.Productos
            .Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PrecioVenta, ct);

        bool quedanPreciosModificados = false;
        foreach (var d in pedido.Detalles.Where(d => d.Activo))
        {
            if (productosBase.TryGetValue(d.ProductoId, out var baseP) && Math.Abs(d.PrecioUnitario - baseP) > 0.001m)
            {
                quedanPreciosModificados = true;
                break;
            }
        }

        if (quedanPreciosModificados)
        {
            pedido.EstadoAprobacionGerencia = EstadoAprobacionGerencia.Pendiente;
            _context.SolicitudesAprobacion.Add(new SolicitudAprobacion
            {
                Id = Guid.NewGuid(),
                Tipo = "CambioPrecio",
                Entidad = "PedidoLima",
                EntidadId = pedido.Id.ToString(),
                UsuarioSolicitanteId = usuarioId,
                FechaSolicitud = DateTime.UtcNow,
                Estado = EstadoAprobacionGerencia.Pendiente,
                DetalleCambio = $"Actualización de precio en repuesto '{prod.Nombre}': base S/ {precioBase:F2} -> solicitado S/ {nuevoPrecio:F2}",
                ValorAnterior = precioBase,
                ValorSolicitado = nuevoPrecio,
                Motivo = "Modificación de precio de repuesto en Pedido Lima",
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            });
        }
        else
        {
            if (pedido.EstadoAprobacionGerencia is EstadoAprobacionGerencia.Pendiente or EstadoAprobacionGerencia.Rechazado)
            {
                pedido.EstadoAprobacionGerencia = EstadoAprobacionGerencia.NoAplica;
                pedido.ObservacionesAprobacionGerencia = null;
            }

            var solicitudesPendientes = await _context.SolicitudesAprobacion
                .Where(s => s.Entidad == "PedidoLima" && s.EntidadId == pedido.Id.ToString() && s.Estado == EstadoAprobacionGerencia.Pendiente && s.Activo)
                .ToListAsync(ct);

            foreach (var sol in solicitudesPendientes)
            {
                sol.Activo = false;
                sol.ObservacionesRespuesta = "Retirada automáticamente por restablecimiento de precios a catálogo.";
                sol.FechaRespuesta = DateTime.UtcNow;
                sol.FechaModificacion = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(pedido.Id, soloClienteId, ct);
    }

    public async Task<ServiceResult<PedidoLimaResponse>> CambiarEstadoAsync(
        Guid id,
        CambiarEstadoPedidoLimaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        bool puedeDespachar = false,
        CancellationToken ct = default)
    {
        var pedido = await _context.PedidosLima
            .Include(p => p.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

        if (pedido is null)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        if (soloClienteId.HasValue && pedido.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        if (pedido.Estado == request.NuevoEstado)
        {
            return await GetByIdAsync(pedido.Id, soloClienteId, ct);
        }

        if (pedido.Estado == EstadoPedidoLima.Cancelado)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("No se puede cambiar el estado de un pedido cancelado.");
        }

        if (pedido.Estado == EstadoPedidoLima.Entregado)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("No se puede cambiar el estado de un pedido ya entregado.");
        }

        // Si se cambia a Cancelado, delegar a CancelarAsync
        if (request.NuevoEstado == EstadoPedidoLima.Cancelado)
        {
            return await CancelarAsync(id, new CancelarPedidoLimaRequest(request.Observacion ?? "Cancelación de pedido"), usuarioId, soloClienteId, ct);
        }

        if ((int)request.NuevoEstado < (int)pedido.Estado)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid($"No se permite retroceder el estado del pedido de {pedido.Estado} a {request.NuevoEstado}.");
        }

        if (request.NuevoEstado == EstadoPedidoLima.Confirmado || request.NuevoEstado == EstadoPedidoLima.Entregado)
        {
            if (pedido.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Pendiente)
            {
                return ServiceResult<PedidoLimaResponse>.Invalid(
                    "No se puede confirmar ni entregar el pedido de Lima porque tiene modificaciones de precio pendientes de aprobación por Gerencia.");
            }

            if (pedido.EstadoAprobacionGerencia == EstadoAprobacionGerencia.Rechazado)
            {
                return ServiceResult<PedidoLimaResponse>.Invalid(
                    "No se puede confirmar ni entregar el pedido de Lima porque la aprobación de Gerencia fue rechazada.");
            }
        }

        // Si pasa a Recibido: los repuestos llegaron al taller y entran al stock.
        if (request.NuevoEstado == EstadoPedidoLima.Recibido)
        {
            return await RegistrarLlegadaAsync(pedido, request.Observacion, usuarioId, soloClienteId, ct);
        }

        // Si pasa a Entregado (Despacho): deducir stock de forma segura y pesimista
        if (request.NuevoEstado == EstadoPedidoLima.Entregado)
        {
            if (!puedeDespachar)
            {
                return ServiceResult<PedidoLimaResponse>.Forbidden("No tiene permisos para despachar pedidos y deducir stock.");
            }

            // Lo que se entrega es lo que llegó: sin la llegada registrada, el stock no
            // tiene esos repuestos y la entrega lo dejaría corto.
            if (!pedido.StockDeducido && !await TieneLlegadaRegistradaAsync(pedido.Id, ct))
            {
                return ServiceResult<PedidoLimaResponse>.Invalid(
                    "Antes de entregarlo, marca el pedido como «Recibido»: su llegada es la que suma los repuestos al stock.");
            }

            if (!pedido.StockDeducido)
            {
                var orderedProductIds = pedido.Detalles
                    .Select(d => d.ProductoId)
                    .Distinct()
                    .OrderBy(pId => pId)
                    .ToList();

                await using var tx = await _context.Database.BeginTransactionAsync(ct);
                try
                {
                    // Bloqueo pesimista en orden determinista
                    foreach (var prodId in orderedProductIds)
                    {
                        await _context.Database.ExecuteSqlInterpolatedAsync(
                            $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {prodId} FOR UPDATE", ct);
                    }

                    var productosBd = await _context.Productos
                        .Where(p => orderedProductIds.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id, ct);

                    // Los productos ya venían cargados con el pedido: se releen después del
                    // bloqueo para no restar sobre un stock viejo.
                    foreach (var prod in productosBd.Values)
                    {
                        await _context.Entry(prod).ReloadAsync(ct);
                    }

                    // Agrupar cantidades requeridas
                    var requeridos = pedido.Detalles
                        .GroupBy(d => d.ProductoId)
                        .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));

                    foreach (var (prodId, cantRequerida) in requeridos)
                    {
                        var prod = productosBd[prodId];
                        if (prod.StockActual < cantRequerida)
                        {
                            await tx.RollbackAsync(ct);
                            return ServiceResult<PedidoLimaResponse>.Invalid(
                                $"Stock insuficiente para despachar el producto '{prod.Nombre}'. Disponible: {prod.StockActual}, Requerido: {cantRequerida}.");
                        }
                    }

                    // Deducir stock y registrar movimientos
                    foreach (var detalle in pedido.Detalles)
                    {
                        var prod = productosBd[detalle.ProductoId];
                        prod.StockActual -= detalle.Cantidad;

                        var movimiento = new MovimientoInventario
                        {
                            ProductoId = prod.Id,
                            Tipo = TipoMovimientoInventario.Salida,
                            Cantidad = detalle.Cantidad,
                            CostoUnitario = detalle.CostoUnitarioHistorico,
                            Motivo = $"Despacho de Pedido Lima {pedido.NumeroPedido}",
                            PedidoLimaId = pedido.Id,
                            CreadoPorId = usuarioId,
                            FechaCreacion = DateTime.UtcNow,
                            Activo = true
                        };

                        _context.MovimientosInventario.Add(movimiento);
                    }

                    pedido.StockDeducido = true;
                    if (!pedido.FechaEntrega.HasValue)
                    {
                        pedido.FechaEntrega = DateTime.UtcNow;
                    }

                    var estadoAnteriorEntrega = pedido.Estado;
                    pedido.Estado = EstadoPedidoLima.Entregado;
                    pedido.ModificadoPorId = usuarioId;
                    pedido.FechaModificacion = DateTime.UtcNow;

                    _context.HistorialEstadosPedidoLima.Add(new HistorialEstadoPedidoLima
                    {
                        PedidoLimaId = pedido.Id,
                        EstadoAnterior = estadoAnteriorEntrega,
                        EstadoNuevo = EstadoPedidoLima.Entregado,
                        UsuarioId = usuarioId,
                        Fecha = DateTime.UtcNow,
                        Observacion = request.Observacion?.Trim() ?? "Despacho y entrega de repuestos con deducción de stock",
                        CreadoPorId = usuarioId,
                        FechaCreacion = DateTime.UtcNow,
                        Activo = true
                    });

                    await _context.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);

                    return await GetByIdAsync(pedido.Id, soloClienteId, ct);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }
        }

        var estadoAnterior = pedido.Estado;
        pedido.Estado = request.NuevoEstado;
        pedido.ModificadoPorId = usuarioId;
        pedido.FechaModificacion = DateTime.UtcNow;

        _context.HistorialEstadosPedidoLima.Add(new HistorialEstadoPedidoLima
        {
            PedidoLimaId = pedido.Id,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = request.NuevoEstado,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Observacion = request.Observacion?.Trim() ?? $"Cambio de estado a {request.NuevoEstado}",
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        });

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(pedido.Id, soloClienteId, ct);
    }

    private Task<bool> TieneLlegadaRegistradaAsync(Guid pedidoId, CancellationToken ct) =>
        _context.MovimientosInventario.AnyAsync(m =>
            m.PedidoLimaId == pedidoId && m.Tipo == TipoMovimientoInventario.Entrada && m.Activo, ct);

    /// <summary>
    /// El pedido llegó al taller: cada repuesto entra al stock con su movimiento de
    /// kárdex, y la entrega después lo descuenta. Si el pedido se cancela ya llegado,
    /// los repuestos se quedan en el inventario, porque físicamente están en el taller.
    /// </summary>
    private async Task<ServiceResult<PedidoLimaResponse>> RegistrarLlegadaAsync(
        PedidoLima pedido,
        string? observacion,
        Guid? usuarioId,
        Guid? soloClienteId,
        CancellationToken ct)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            if (!await TieneLlegadaRegistradaAsync(pedido.Id, ct))
            {
                var productosIds = pedido.Detalles
                    .Select(d => d.ProductoId)
                    .Distinct()
                    .OrderBy(pId => pId)
                    .ToList();

                // Bloqueo pesimista en orden determinista, como en el despacho
                foreach (var prodId in productosIds)
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {prodId} FOR UPDATE", ct);
                }

                var productosBd = await _context.Productos
                    .Where(p => productosIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, ct);
                foreach (var prod in productosBd.Values)
                {
                    await _context.Entry(prod).ReloadAsync(ct);
                }

                foreach (var detalle in pedido.Detalles)
                {
                    var prod = productosBd[detalle.ProductoId];
                    prod.StockActual += detalle.Cantidad;
                    prod.FechaModificacion = DateTime.UtcNow;

                    _context.MovimientosInventario.Add(new MovimientoInventario
                    {
                        ProductoId = prod.Id,
                        Tipo = TipoMovimientoInventario.Entrada,
                        Cantidad = detalle.Cantidad,
                        CostoUnitario = detalle.CostoUnitarioHistorico,
                        Motivo = $"Llegada del pedido de Lima {pedido.NumeroPedido}",
                        PedidoLimaId = pedido.Id,
                        CreadoPorId = usuarioId,
                        FechaCreacion = DateTime.UtcNow,
                        Activo = true
                    });
                }
            }

            if (!pedido.FechaLlegada.HasValue)
            {
                pedido.FechaLlegada = DateTime.UtcNow;
            }

            var estadoAnterior = pedido.Estado;
            pedido.Estado = EstadoPedidoLima.Recibido;
            pedido.ModificadoPorId = usuarioId;
            pedido.FechaModificacion = DateTime.UtcNow;

            _context.HistorialEstadosPedidoLima.Add(new HistorialEstadoPedidoLima
            {
                PedidoLimaId = pedido.Id,
                EstadoAnterior = estadoAnterior,
                EstadoNuevo = EstadoPedidoLima.Recibido,
                UsuarioId = usuarioId,
                Fecha = DateTime.UtcNow,
                Observacion = observacion?.Trim() ?? "Llegada al taller: los repuestos entran al stock",
                CreadoPorId = usuarioId,
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            });

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return await GetByIdAsync(pedido.Id, soloClienteId, ct);
    }

    public async Task<ServiceResult<PedidoLimaResponse>> CancelarAsync(
        Guid id,
        CancelarPedidoLimaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.MotivoCancelacion))
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("El motivo de cancelación es obligatorio.");
        }

        var pedido = await _context.PedidosLima
            .Include(p => p.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

        if (pedido is null)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        if (soloClienteId.HasValue && pedido.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        if (pedido.Estado == EstadoPedidoLima.Cancelado)
        {
            return ServiceResult<PedidoLimaResponse>.Conflict("El pedido ya se encuentra cancelado.");
        }

        if (pedido.Estado == EstadoPedidoLima.Entregado)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid("No se puede cancelar un pedido que ya ha sido entregado.");
        }

        var estadoAnterior = pedido.Estado;
        pedido.Estado = EstadoPedidoLima.Cancelado;
        pedido.MotivoCancelacion = request.MotivoCancelacion.Trim();
        pedido.ModificadoPorId = usuarioId;
        pedido.FechaModificacion = DateTime.UtcNow;

        _context.HistorialEstadosPedidoLima.Add(new HistorialEstadoPedidoLima
        {
            PedidoLimaId = pedido.Id,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = EstadoPedidoLima.Cancelado,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Observacion = $"Cancelación: {request.MotivoCancelacion.Trim()}",
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        });

        var solicitudesPendientes = await _context.SolicitudesAprobacion
            .Where(s => s.Entidad == "PedidoLima" && s.EntidadId == pedido.Id.ToString() && s.Estado == EstadoAprobacionGerencia.Pendiente && s.Activo)
            .ToListAsync(ct);

        foreach (var sol in solicitudesPendientes)
        {
            sol.Activo = false;
            sol.ObservacionesRespuesta = $"Cancelada automáticamente por cancelación del pedido de Lima: {request.MotivoCancelacion.Trim()}";
            sol.FechaRespuesta = DateTime.UtcNow;
            sol.FechaModificacion = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(pedido.Id, soloClienteId, ct);
    }

    public async Task<byte[]> ExportarExcelAsync(
        Guid? soloClienteId = null,
        Guid? clienteId = null,
        EstadoPedidoLima? estado = null,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        var pedidos = await GetAllAsync(soloClienteId, clienteId, estado, fechaInicio, fechaFin, null, ct);

        var dtos = pedidos.Select(p => new PedidoLimaExcelDto(
            NumeroPedido: p.NumeroPedido,
            Fecha: p.Fecha,
            ClienteNombre: p.ClienteNombre,
            ClienteDocumento: p.ClienteDocumento ?? string.Empty,
            EmpresaTransporte: p.EmpresaTransporte ?? "N/A",
            NumeroGuia: p.NumeroGuia ?? "N/A",
            CantidadItems: p.Detalles.Sum(d => d.Cantidad),
            SubtotalGravado: p.SubtotalGravado,
            MontoIgv: p.MontoIgv,
            Total: p.Total,
            Estado: p.EstadoDescripcion,
            FechaEstimadaLlegada: p.FechaEstimadaLlegada,
            FechaLlegada: p.FechaLlegada,
            FechaEntrega: p.FechaEntrega,
            Observaciones: p.Observaciones
        )).ToList();

        return _excelService.GenerarExcelPedidosLima(dtos);
    }

    public async Task<ServiceResult<PedidoLimaResponse>> AprobacionGerenciaAsync(
        Guid id,
        AprobacionGerenciaPedidoLimaRequest request,
        Guid? usuarioId = null,
        CancellationToken ct = default)
    {
        var pedido = await _context.PedidosLima
            .Include(p => p.Cliente)
            .Include(p => p.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(p => p.HistorialEstados)
                .ThenInclude(h => h.Usuario)
            .Include(p => p.Pagos.Where(pay => pay.Activo))
                .ThenInclude(pay => pay.MetodoPago)
            .Include(p => p.Pagos.Where(pay => pay.Activo))
                .ThenInclude(pay => pay.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);

        if (pedido is null)
        {
            return ServiceResult<PedidoLimaResponse>.NotFound("Pedido no encontrado.");
        }

        if (pedido.Estado == EstadoPedidoLima.Entregado || pedido.Estado == EstadoPedidoLima.Cancelado)
        {
            return ServiceResult<PedidoLimaResponse>.Invalid(
                $"No se puede modificar la aprobación de gerencia en un pedido en estado terminal '{pedido.Estado}'.");
        }

        pedido.EstadoAprobacionGerencia = request.Estado;
        pedido.FechaAprobacionGerencia = DateTime.UtcNow;
        pedido.UsuarioAprobacionGerenciaId = usuarioId;
        if (!string.IsNullOrWhiteSpace(request.Observaciones))
        {
            pedido.ObservacionesAprobacionGerencia = request.Observaciones.Trim();
        }
        pedido.FechaModificacion = DateTime.UtcNow;

        var solicitudes = await _context.SolicitudesAprobacion
            .Where(s => s.Entidad == "PedidoLima" && s.EntidadId == pedido.Id.ToString() && s.Estado == EstadoAprobacionGerencia.Pendiente)
            .ToListAsync(ct);

        foreach (var s in solicitudes)
        {
            s.Estado = request.Estado;
            s.UsuarioAprobadorId = usuarioId;
            s.FechaRespuesta = DateTime.UtcNow;
            s.ObservacionesRespuesta = request.Observaciones?.Trim();
            s.FechaModificacion = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        await _auditoriaService.RegistrarEventoAsync(
            usuarioId,
            request.Estado == EstadoAprobacionGerencia.Aprobado ? "AprobacionGerenciaPedidoLimaAprobada" : "AprobacionGerenciaPedidoLimaRechazada",
            "PedidoLima",
            pedido.Id.ToString(),
            new
            {
                PedidoLimaId = pedido.Id,
                EstadoAprobacionGerencia = request.Estado.ToString(),
                Observaciones = request.Observaciones?.Trim()
            });

        return ServiceResult<PedidoLimaResponse>.Success(MapToResponse(pedido));
    }

    private static PedidoLimaResponse MapToResponse(PedidoLima p)
    {
        var totalPagado = p.Pagos != null ? p.Pagos.Where(pay => pay.Activo).Sum(pay => pay.Monto) : 0m;
        var saldo = Math.Max(0m, p.Total - totalPagado);
        var estadoPago = saldo == 0m && totalPagado > 0m ? "Pagado" : (totalPagado > 0m ? "Parcial" : "Pendiente");

        var pagosDto = p.Pagos?
            .Where(pay => pay.Activo)
            .OrderBy(pay => pay.Fecha)
            .Select(pay => new CRMTeamBenavides.Api.Features.Ventas.PagoResponse(
                pay.Id,
                pay.Monto,
                pay.MetodoPagoId,
                pay.MetodoPago?.Nombre ?? string.Empty,
                pay.MetodoPago?.Codigo ?? string.Empty,
                pay.Fecha,
                pay.Referencia,
                pay.EsAnticipo,
                pay.VentaId,
                pay.OrdenServicioId,
                pay.UsuarioId,
                pay.Usuario?.NombreCompleto,
                pay.Observaciones,
                pay.Activo
            )).ToList();

        return new PedidoLimaResponse(
            p.Id,
            p.NumeroPedido ?? string.Empty,
            p.ClienteId,
            p.Cliente?.NombreCompleto ?? "Cliente no registrado",
            p.Cliente?.NumeroDocumento ?? p.Cliente?.DocumentoIdentidad,
            p.Cliente?.Telefono,
            p.Fecha,
            p.Estado,
            p.Estado.ToString(),
            p.EmpresaTransporte,
            p.NumeroGuia,
            p.FechaEstimadaLlegada,
            p.FechaLlegada,
            p.FechaEntrega,
            p.SubtotalGravado,
            p.SubtotalExonerado,
            p.SubtotalInafecto,
            p.PorcentajeIgv,
            p.MontoIgv,
            p.Total,
            p.Observaciones,
            p.MotivoCancelacion,
            p.StockDeducido,
            p.Detalles.Select(d => new DetallePedidoLimaResponse(
                d.Id,
                d.ProductoId,
                d.Producto?.Codigo ?? string.Empty,
                d.Producto?.Nombre ?? string.Empty,
                d.Cantidad,
                d.PrecioUnitario,
                d.CostoUnitarioHistorico,
                d.TipoAfectacionIgv,
                d.TipoAfectacionIgv.ToString(),
                d.SubtotalGravado,
                d.PorcentajeIgvAplicado,
                d.MontoIgv,
                d.Total
            )).ToList(),
            p.HistorialEstados.Select(h => new HistorialEstadoPedidoLimaResponse(
                h.Id,
                h.EstadoAnterior,
                h.EstadoAnterior?.ToString(),
                h.EstadoNuevo,
                h.EstadoNuevo.ToString(),
                h.UsuarioId,
                h.Usuario?.NombreCompleto ?? h.Usuario?.UserName,
                h.Fecha,
                h.Observacion
            )).ToList(),
            p.FechaCreacion,
            totalPagado,
            saldo,
            estadoPago,
            (int)p.EstadoAprobacionGerencia,
            p.EstadoAprobacionGerencia.ToString(),
            pagosDto);
    }
}
