using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CRMTeamBenavides.Api.Features.Compras;
using CRMTeamBenavides.Api.Services.Exportacion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRMTeamBenavides.Api.Services;

/// <summary>
/// Compras a proveedores. Las reglas están en docs/modulo-compras.md. Orden de los
/// bloqueos, para no cruzarse con ventas y cobros: compra, repuestos y al final la caja.
/// </summary>
public partial class CompraService : ICompraService
{
    private const string MensajeEfectivoSinCaja =
        "Para pagar en efectivo tiene que haber una caja chica abierta: el dinero sale de ella.";
    private const string MensajeNoEncontrada = "Compra no encontrada.";
    private const int MaximoLineas = 200;

    private readonly ApplicationDbContext _context;
    private readonly IConfiguracionService _configuracion;
    private readonly IExportacionExcelService _exportacion;

    public CompraService(
        ApplicationDbContext context,
        IConfiguracionService configuracion,
        IExportacionExcelService exportacion)
    {
        _context = context;
        _configuracion = configuracion;
        _exportacion = exportacion;
    }

    // ------------------------------------------------------------------
    // Consultas
    // ------------------------------------------------------------------

    public async Task<List<CompraResumenResponse>> ListarAsync(FiltrosCompras filtros, CancellationToken ct = default)
    {
        var query = _context.Compras.AsNoTracking().Where(c => c.Activo);

        if (filtros.ProveedorId.HasValue)
            query = query.Where(c => c.ProveedorId == filtros.ProveedorId.Value);

        if (filtros.Estado.HasValue)
            query = query.Where(c => c.Estado == filtros.Estado.Value);

        // Pendiente, pagada o vencida solo se dice de una compra registrada.
        if (!string.IsNullOrWhiteSpace(filtros.EstadoPago))
            query = query.Where(c => c.Estado == EstadoCompra.Registrada);

        if (filtros.FechaDesde.HasValue)
            query = query.Where(c => c.FechaEmision >= filtros.FechaDesde.Value);

        if (filtros.FechaHasta.HasValue)
            query = query.Where(c => c.FechaEmision <= filtros.FechaHasta.Value);

        if (!string.IsNullOrWhiteSpace(filtros.Busqueda))
        {
            var texto = filtros.Busqueda.Trim();
            var patron = $"%{texto}%";
            // «F001-00000123» también encuentra la F001-123 guardada sin ceros.
            var comprobante = ComprobanteBuscado().Match(texto.ToUpperInvariant());
            var patronNormalizado = comprobante.Success
                ? $"%{comprobante.Groups[1].Value}-{CalculoCompra.NormalizarNumero(comprobante.Groups[2].Value)}%"
                : patron;

            query = query.Where(c =>
                EF.Functions.ILike(c.NumeroCompra, patron) ||
                EF.Functions.ILike(c.Serie + "-" + c.Numero, patron) ||
                EF.Functions.ILike(c.Serie + "-" + c.Numero, patronNormalizado) ||
                EF.Functions.ILike(c.Proveedor.RazonSocial, patron) ||
                EF.Functions.ILike(c.Proveedor.NumeroDocumento, patron));
        }

        var filas = await query
            .OrderByDescending(c => c.FechaEmision)
            .ThenByDescending(c => c.FechaCreacion)
            .Select(c => new
            {
                Compra = c,
                ProveedorNombre = c.Proveedor.RazonSocial,
                ProveedorDocumento = c.Proveedor.NumeroDocumento,
                NumeroPedidoLima = c.PedidoLima != null ? c.PedidoLima.NumeroPedido : null,
                Pagado = c.Pagos.Where(p => p.Activo && !p.Anulado).Sum(p => (decimal?)p.Monto) ?? 0m,
                CantidadLineas = c.Detalles.Count(d => d.Activo)
            })
            .ToListAsync(ct);

        var hoy = HoraPeru.Hoy();
        var resultado = new List<CompraResumenResponse>(filas.Count);

        foreach (var fila in filas)
        {
            var c = fila.Compra;
            var situacion = Situacion(c, fila.Pagado, hoy);
            if (!CumpleEstadoPago(filtros.EstadoPago, situacion))
                continue;

            resultado.Add(new CompraResumenResponse(
                c.Id,
                c.NumeroCompra,
                c.ProveedorId,
                fila.ProveedorNombre,
                fila.ProveedorDocumento,
                c.TipoComprobante,
                c.Serie,
                c.Numero,
                c.FechaEmision,
                c.FechaVencimiento,
                c.Moneda,
                c.TipoCambio,
                c.Total,
                c.TotalSoles,
                situacion.Pagado,
                situacion.Saldo,
                situacion.EstadoPago,
                situacion.Vencida,
                c.Estado,
                c.PedidoLimaId,
                fila.NumeroPedidoLima,
                fila.CantidadLineas,
                c.FechaCreacion));
        }

        return resultado;
    }

    public async Task<byte[]> ExportarExcelAsync(FiltrosCompras filtros, CancellationToken ct = default)
    {
        var compras = await ListarAsync(filtros, ct);

        return _exportacion.GenerarExcelCompras(compras.Select(c => new CompraExcelDto(
            c.NumeroCompra,
            c.FechaEmision,
            $"{NombreComprobante(c.TipoComprobante)} {c.Serie}-{c.Numero}",
            c.ProveedorNombre,
            c.ProveedorDocumento,
            c.Moneda.ToString(),
            c.TipoCambio,
            c.Total,
            c.TotalSoles,
            c.TotalPagado,
            c.Saldo,
            EstadoParaExcel(c),
            c.FechaVencimiento,
            c.NumeroPedidoLima)));
    }

    public async Task<ServiceResult<CompraResponse>> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var compra = await _context.Compras
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.Proveedor)
            .Include(c => c.PedidoLima)
            .Include(c => c.Usuario)
            .Include(c => c.UsuarioAnulacion)
            .Include(c => c.Detalles).ThenInclude(d => d.Producto)
            .Include(c => c.Pagos).ThenInclude(p => p.MetodoPago)
            .Include(c => c.Pagos).ThenInclude(p => p.Usuario)
            .Include(c => c.Pagos).ThenInclude(p => p.UsuarioAnulacion)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);

        if (compra is null)
            return ServiceResult<CompraResponse>.NotFound(MensajeNoEncontrada);

        var pagoIds = compra.Pagos.Select(p => p.Id).ToList();
        var pagosConCaja = (await _context.MovimientosCajaChica
                .AsNoTracking()
                .Where(m => m.PagoCompraId != null && pagoIds.Contains(m.PagoCompraId.Value) && m.Tipo == TipoMovimientoCaja.Egreso)
                .Select(m => m.PagoCompraId!.Value)
                .ToListAsync(ct))
            .ToHashSet();

        return ServiceResult<CompraResponse>.Success(Mapear(compra, pagosConCaja, HoraPeru.Hoy()));
    }

    // ------------------------------------------------------------------
    // Registrar
    // ------------------------------------------------------------------

    public async Task<ServiceResult<CompraResponse>> RegistrarAsync(
        CrearCompraRequest request, Guid? usuarioId, CancellationToken ct = default)
    {
        var (cabecera, errorCabecera) = await ValidarCabeceraAsync(
            request.ProveedorId, request.TipoComprobante, request.Serie, request.Numero,
            request.FechaEmision, request.FechaVencimiento, excluirCompraId: null, proveedorActualId: null, ct);
        if (errorCabecera is not null)
            return errorCabecera;

        var errorTextos = ValidarTextos(request.GuiaRemision, request.Observaciones);
        if (errorTextos is not null)
            return ServiceResult<CompraResponse>.Invalid(errorTextos);

        if (!Enum.IsDefined(request.Moneda))
            return ServiceResult<CompraResponse>.Invalid("La moneda de la compra no es válida.");

        var tipoCambio = 1m;
        if (request.Moneda == MonedaCompra.USD)
        {
            if (request.TipoCambio is not > 0m || request.TipoCambio >= 100m)
                return ServiceResult<CompraResponse>.Invalid("Una compra en dólares necesita su tipo de cambio: soles por dólar, mayor a 0.");
            tipoCambio = Math.Round(request.TipoCambio.Value, 4, MidpointRounding.AwayFromZero);
        }

        var configuracion = await _configuracion.ObtenerConfiguracionEmpresaAsync(ct);
        var porcentajeIgv = request.PorcentajeIgv ?? configuracion.PorcentajeIgv;
        if (porcentajeIgv < 0 || porcentajeIgv > 100)
            return ServiceResult<CompraResponse>.Invalid("El porcentaje de IGV debe estar entre 0 y 100.");

        if (request.Detalles is null || request.Detalles.Count == 0)
            return ServiceResult<CompraResponse>.Invalid("Agrega al menos un repuesto o concepto a la compra.");
        if (request.Detalles.Count > MaximoLineas)
            return ServiceResult<CompraResponse>.Invalid($"Una compra admite hasta {MaximoLineas} líneas.");

        PedidoLima? pedido = null;
        if (request.PedidoLimaId.HasValue)
        {
            pedido = await _context.PedidosLima.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PedidoLimaId.Value && p.Activo, ct);
            if (pedido is null)
                return ServiceResult<CompraResponse>.Invalid("El pedido a Lima indicado no existe.");
            if (pedido.Estado == EstadoPedidoLima.Cancelado)
                return ServiceResult<CompraResponse>.Invalid("No se puede ligar una compra a un pedido a Lima cancelado.");
        }

        // Solo para validar y tomar el nombre; el stock se toca después, con los repuestos bloqueados.
        var productoIds = request.Detalles
            .Where(d => d.ProductoId.HasValue)
            .Select(d => d.ProductoId!.Value)
            .Distinct()
            .ToList();
        var productos = await _context.Productos.AsNoTracking()
            .Where(p => productoIds.Contains(p.Id) && p.Activo)
            .ToDictionaryAsync(p => p.Id, ct);

        var compra = new Compra
        {
            ProveedorId = cabecera!.ProveedorId,
            TipoComprobante = cabecera.TipoComprobante,
            Serie = cabecera.Serie,
            Numero = cabecera.Numero,
            FechaEmision = request.FechaEmision,
            FechaVencimiento = request.FechaVencimiento,
            Moneda = request.Moneda,
            TipoCambio = tipoCambio,
            PorcentajeIgv = porcentajeIgv,
            PreciosIncluyenIgv = request.PreciosIncluyenIgv,
            PedidoLimaId = pedido?.Id,
            GuiaRemision = Opcional(request.GuiaRemision),
            Observaciones = Opcional(request.Observaciones),
            Estado = EstadoCompra.Registrada,
            UsuarioId = usuarioId,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        var orden = 0;
        foreach (var linea in request.Detalles)
        {
            orden++;
            if (linea.Cantidad < 1 || linea.Cantidad > 100_000)
                return ServiceResult<CompraResponse>.Invalid($"Línea {orden}: la cantidad debe ser un entero entre 1 y 100 000.");
            if (linea.PrecioUnitario < 0 || linea.PrecioUnitario > 10_000_000)
                return ServiceResult<CompraResponse>.Invalid($"Línea {orden}: el precio unitario no es válido.");
            if (!Enum.IsDefined(linea.TipoAfectacionIgv))
                return ServiceResult<CompraResponse>.Invalid($"Línea {orden}: la afectación de IGV no es válida.");

            Producto? producto = null;
            string descripcion;
            if (linea.ProductoId.HasValue)
            {
                if (!productos.TryGetValue(linea.ProductoId.Value, out producto))
                    return ServiceResult<CompraResponse>.Invalid($"Línea {orden}: el repuesto no existe o fue dado de baja.");
                descripcion = producto.Nombre.Length > 250 ? producto.Nombre[..250] : producto.Nombre;
            }
            else
            {
                descripcion = linea.Descripcion?.Trim() ?? string.Empty;
                if (descripcion.Length == 0)
                    return ServiceResult<CompraResponse>.Invalid($"Línea {orden}: elige un repuesto o escribe el concepto.");
                if (descripcion.Length > 250)
                    return ServiceResult<CompraResponse>.Invalid($"Línea {orden}: el concepto no puede pasar de 250 caracteres.");
            }

            var precio = Math.Round(linea.PrecioUnitario, 4, MidpointRounding.AwayFromZero);
            var montos = CalculoCompra.CalcularLinea(
                linea.Cantidad, precio, linea.TipoAfectacionIgv, porcentajeIgv, request.PreciosIncluyenIgv);

            compra.Detalles.Add(new DetalleCompra
            {
                CompraId = compra.Id,
                Orden = orden,
                ProductoId = producto?.Id,
                Descripcion = descripcion,
                Cantidad = linea.Cantidad,
                PrecioUnitario = precio,
                TipoAfectacionIgv = linea.TipoAfectacionIgv,
                Subtotal = montos.Subtotal,
                MontoIgv = montos.MontoIgv,
                Total = montos.Total,
                CostoUnitarioSoles = CalculoCompra.CostoUnitarioSoles(montos, linea.Cantidad, cabecera.TipoComprobante, tipoCambio),
                // Lo de un pedido a Lima entra al stock cuando el pedido se marca «Recibido».
                MueveStock = producto is not null && pedido is null,
                CreadoPorId = usuarioId,
                FechaCreacion = DateTime.UtcNow,
                Activo = true
            });
        }

        compra.SubtotalGravado = compra.Detalles.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Gravado).Sum(d => d.Subtotal);
        compra.SubtotalExonerado = compra.Detalles.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Exonerado).Sum(d => d.Subtotal);
        compra.SubtotalInafecto = compra.Detalles.Where(d => d.TipoAfectacionIgv == TipoAfectacionIgv.Inafecto).Sum(d => d.Subtotal);
        compra.MontoIgv = compra.Detalles.Sum(d => d.MontoIgv);
        compra.Total = compra.Detalles.Sum(d => d.Total);
        compra.TotalSoles = CalculoCompra.Redondear(compra.Total * tipoCambio);

        if (compra.Total <= 0)
            return ServiceResult<CompraResponse>.Invalid("El total de la compra debe ser mayor a 0.");

        var pagos = request.Pagos ?? new List<RegistrarPagoCompraRequest>();
        var metodoIds = pagos.Select(p => p.MetodoPagoId).Distinct().ToList();
        var metodos = await _context.MetodosPago.AsNoTracking()
            .Where(m => metodoIds.Contains(m.Id) && m.Activo)
            .ToDictionaryAsync(m => m.Id, ct);

        foreach (var pago in pagos)
        {
            var errorPago = ValidarPago(pago, metodos.GetValueOrDefault(pago.MetodoPagoId));
            if (errorPago is not null)
                return ServiceResult<CompraResponse>.Invalid(errorPago);
        }

        var totalPagos = pagos.Sum(p => CalculoCompra.Redondear(p.Monto));
        if (totalPagos > compra.Total)
            return ServiceResult<CompraResponse>.Invalid(
                $"Los pagos ({Monto(compra.Moneda, totalPagos)}) pasan el total de la compra ({Monto(compra.Moneda, compra.Total)}).");

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            compra.NumeroCompra = await SiguienteNumeroAsync(ct);
            _context.Compras.Add(compra);

            var lineasConStock = compra.Detalles.Where(d => d.MueveStock).OrderBy(d => d.Orden).ToList();
            var productosBloqueados = await BloquearProductosAsync(lineasConStock.Select(d => d.ProductoId!.Value), ct);

            foreach (var detalle in lineasConStock)
            {
                var producto = productosBloqueados[detalle.ProductoId!.Value];
                if (!producto.Activo)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<CompraResponse>.Invalid($"El repuesto «{producto.Nombre}» se dio de baja mientras se registraba la compra.");
                }

                var costoAnterior = producto.Costo;
                var costoNuevo = CalculoCompra.NuevoCosto(
                    configuracion.MetodoCosteo, producto.StockActual, producto.Costo, detalle.Cantidad, detalle.CostoUnitarioSoles);

                producto.StockActual += detalle.Cantidad;
                producto.Costo = costoNuevo;
                producto.ModificadoPorId = usuarioId;
                producto.FechaModificacion = DateTime.UtcNow;

                detalle.CostoAnteriorProducto = costoAnterior;
                detalle.CostoResultanteProducto = costoNuevo;

                _context.MovimientosInventario.Add(new MovimientoInventario
                {
                    ProductoId = producto.Id,
                    Tipo = TipoMovimientoInventario.Entrada,
                    Cantidad = detalle.Cantidad,
                    CostoUnitario = CalculoCompra.Redondear(detalle.CostoUnitarioSoles),
                    Motivo = $"Compra {compra.NumeroCompra} · {Comprobante(compra)}",
                    CompraId = compra.Id,
                    CreadoPorId = usuarioId,
                    FechaCreacion = DateTime.UtcNow,
                    Activo = true
                });
            }

            var caja = new CajaDeLaOperacion();
            foreach (var pago in pagos)
            {
                var errorPago = await AgregarPagoAsync(compra, cabecera.ProveedorNombre, pago, metodos[pago.MetodoPagoId], usuarioId, caja, ct);
                if (errorPago is not null)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<CompraResponse>.Invalid(errorPago);
                }
            }

            Auditar(usuarioId, "Crear", compra.Id, new
            {
                compra.NumeroCompra,
                Comprobante = Comprobante(compra),
                compra.ProveedorId,
                Moneda = compra.Moneda.ToString(),
                compra.TipoCambio,
                compra.Total,
                compra.TotalSoles,
                compra.PedidoLimaId,
                Lineas = compra.Detalles.Count,
                Pagos = totalPagos
            });

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (EsDuplicado(ex))
        {
            await tx.RollbackAsync(ct);
            _context.ChangeTracker.Clear();
            return ServiceResult<CompraResponse>.Conflict("Ese comprobante del proveedor se acaba de registrar en otra compra.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return await ObtenerAsync(compra.Id, ct);
    }

    // ------------------------------------------------------------------
    // Corregir datos del comprobante
    // ------------------------------------------------------------------

    public async Task<ServiceResult<CompraResponse>> ActualizarAsync(
        Guid id, ActualizarCompraRequest request, Guid? usuarioId, CancellationToken ct = default)
    {
        var errorTextos = ValidarTextos(request.GuiaRemision, request.Observaciones);
        if (errorTextos is not null)
            return ServiceResult<CompraResponse>.Invalid(errorTextos);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            await BloquearCompraAsync(id, ct);
            var compra = await _context.Compras.FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);
            if (compra is null)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.NotFound(MensajeNoEncontrada);
            }

            if (compra.Estado == EstadoCompra.Anulada)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.Invalid("Una compra anulada no se edita.");
            }

            var (cabecera, errorCabecera) = await ValidarCabeceraAsync(
                request.ProveedorId, compra.TipoComprobante, request.Serie, request.Numero,
                request.FechaEmision, request.FechaVencimiento, excluirCompraId: compra.Id, proveedorActualId: compra.ProveedorId, ct);
            if (errorCabecera is not null)
            {
                await tx.RollbackAsync(ct);
                return errorCabecera;
            }

            var anterior = new
            {
                compra.ProveedorId,
                compra.Serie,
                compra.Numero,
                compra.FechaEmision,
                compra.FechaVencimiento,
                compra.GuiaRemision,
                compra.Observaciones
            };

            compra.ProveedorId = cabecera!.ProveedorId;
            compra.Serie = cabecera.Serie;
            compra.Numero = cabecera.Numero;
            compra.FechaEmision = request.FechaEmision;
            compra.FechaVencimiento = request.FechaVencimiento;
            compra.GuiaRemision = Opcional(request.GuiaRemision);
            compra.Observaciones = Opcional(request.Observaciones);
            compra.ModificadoPorId = usuarioId;
            compra.FechaModificacion = DateTime.UtcNow;

            Auditar(usuarioId, "Editar", compra.Id, new
            {
                compra.NumeroCompra,
                Anterior = anterior,
                Nuevo = new
                {
                    compra.ProveedorId,
                    compra.Serie,
                    compra.Numero,
                    compra.FechaEmision,
                    compra.FechaVencimiento,
                    compra.GuiaRemision,
                    compra.Observaciones
                }
            });

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (EsDuplicado(ex))
        {
            await tx.RollbackAsync(ct);
            _context.ChangeTracker.Clear();
            return ServiceResult<CompraResponse>.Conflict("Ese comprobante del proveedor ya está registrado en otra compra.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return await ObtenerAsync(id, ct);
    }

    // ------------------------------------------------------------------
    // Pagos
    // ------------------------------------------------------------------

    public async Task<ServiceResult<CompraResponse>> RegistrarPagoAsync(
        Guid id, RegistrarPagoCompraRequest request, Guid? usuarioId, CancellationToken ct = default)
    {
        var metodo = await _context.MetodosPago.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MetodoPagoId && m.Activo, ct);
        var errorPago = ValidarPago(request, metodo);
        if (errorPago is not null)
            return ServiceResult<CompraResponse>.Invalid(errorPago);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            await BloquearCompraAsync(id, ct);
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);
            if (compra is null)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.NotFound(MensajeNoEncontrada);
            }

            if (compra.Estado == EstadoCompra.Anulada)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.Invalid("La compra está anulada: no admite pagos.");
            }

            var saldo = compra.Total - await TotalPagadoAsync(compra.Id, ct);
            if (saldo <= 0)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.Invalid("La compra ya está pagada.");
            }

            var monto = CalculoCompra.Redondear(request.Monto);
            if (monto > saldo)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.Invalid(
                    $"El pago ({Monto(compra.Moneda, monto)}) pasa el saldo de la compra ({Monto(compra.Moneda, saldo)}).");
            }

            var errorCaja = await AgregarPagoAsync(compra, compra.Proveedor.RazonSocial, request, metodo!, usuarioId, new CajaDeLaOperacion(), ct);
            if (errorCaja is not null)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.Invalid(errorCaja);
            }

            Auditar(usuarioId, "Pagar", compra.Id, new
            {
                compra.NumeroCompra,
                Monto = monto,
                Moneda = compra.Moneda.ToString(),
                MetodoPago = metodo!.Nombre,
                SaldoAnterior = saldo
            });

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return await ObtenerAsync(id, ct);
    }

    public async Task<ServiceResult<CompraResponse>> AnularPagoAsync(
        Guid id, Guid pagoId, AnularRequest request, Guid? usuarioId, CancellationToken ct = default)
    {
        var (motivo, errorMotivo) = ValidarMotivo(request);
        if (errorMotivo is not null)
            return ServiceResult<CompraResponse>.Invalid(errorMotivo);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            await BloquearCompraAsync(id, ct);
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);
            if (compra is null)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.NotFound(MensajeNoEncontrada);
            }

            if (compra.Estado == EstadoCompra.Anulada)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.Invalid("La compra está anulada: sus pagos se anularon con ella.");
            }

            var pago = await _context.PagosCompra
                .Include(p => p.MetodoPago)
                .FirstOrDefaultAsync(p => p.Id == pagoId && p.CompraId == id && p.Activo, ct);
            if (pago is null)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.NotFound("Pago no encontrado.");
            }

            if (pago.Anulado)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.Invalid("Ese pago ya está anulado.");
            }

            var errorCaja = await RevertirPagoAsync(compra, pago, motivo!, usuarioId, new CajaDeLaOperacion(), ct);
            if (errorCaja is not null)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.Invalid(errorCaja);
            }

            Auditar(usuarioId, "AnularPago", compra.Id, new
            {
                compra.NumeroCompra,
                PagoId = pago.Id,
                pago.Monto,
                MetodoPago = pago.MetodoPago.Nombre,
                Motivo = motivo
            });

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return await ObtenerAsync(id, ct);
    }

    // ------------------------------------------------------------------
    // Anular la compra
    // ------------------------------------------------------------------

    public async Task<ServiceResult<CompraResponse>> AnularAsync(
        Guid id, AnularRequest request, Guid? usuarioId, CancellationToken ct = default)
    {
        var (motivo, errorMotivo) = ValidarMotivo(request);
        if (errorMotivo is not null)
            return ServiceResult<CompraResponse>.Invalid(errorMotivo);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            await BloquearCompraAsync(id, ct);
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                .Include(c => c.Pagos).ThenInclude(p => p.MetodoPago)
                .FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);
            if (compra is null)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.NotFound(MensajeNoEncontrada);
            }

            if (compra.Estado == EstadoCompra.Anulada)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CompraResponse>.Invalid("La compra ya está anulada.");
            }

            // En orden inverso: con el mismo repuesto en dos líneas, cada una deshace el costo que dejó.
            var lineasConStock = compra.Detalles
                .Where(d => d.Activo && d.MueveStock)
                .OrderByDescending(d => d.Orden)
                .ToList();
            var productos = await BloquearProductosAsync(lineasConStock.Select(d => d.ProductoId!.Value), ct);

            // Una compra vigente posterior del mismo repuesto ya calculó su costo encima
            // de este: deshacerlo pisaría esa compra. Comparar valores no basta, porque
            // dos compras pueden dejar el mismo costo por coincidencia.
            var productoIds = productos.Keys.ToList();
            var conCompraPosterior = (await _context.DetallesCompra
                    .Where(d => d.Activo
                        && d.MueveStock
                        && d.ProductoId != null
                        && productoIds.Contains(d.ProductoId.Value)
                        && d.CompraId != compra.Id
                        && d.Compra.Activo
                        && d.Compra.Estado == EstadoCompra.Registrada
                        && d.Compra.FechaCreacion > compra.FechaCreacion)
                    .Select(d => d.ProductoId!.Value)
                    .Distinct()
                    .ToListAsync(ct))
                .ToHashSet();

            foreach (var detalle in lineasConStock)
            {
                var producto = productos[detalle.ProductoId!.Value];
                if (producto.StockActual < detalle.Cantidad)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<CompraResponse>.Invalid(
                        $"No se puede anular: del repuesto «{producto.Nombre}» quedan {producto.StockActual} en stock y la compra ingresó {detalle.Cantidad}. Parte de lo comprado ya salió.");
                }

                producto.StockActual -= detalle.Cantidad;

                // Si otra compra o una edición manual cambió el costo después, ese costo se respeta.
                if (!conCompraPosterior.Contains(producto.Id)
                    && detalle.CostoAnteriorProducto.HasValue
                    && producto.Costo == detalle.CostoResultanteProducto)
                {
                    producto.Costo = detalle.CostoAnteriorProducto.Value;
                }

                producto.ModificadoPorId = usuarioId;
                producto.FechaModificacion = DateTime.UtcNow;

                _context.MovimientosInventario.Add(new MovimientoInventario
                {
                    ProductoId = producto.Id,
                    Tipo = TipoMovimientoInventario.Salida,
                    Cantidad = detalle.Cantidad,
                    CostoUnitario = CalculoCompra.Redondear(detalle.CostoUnitarioSoles),
                    Motivo = $"Anulación de la compra {compra.NumeroCompra} · {Comprobante(compra)}",
                    CompraId = compra.Id,
                    CreadoPorId = usuarioId,
                    FechaCreacion = DateTime.UtcNow,
                    Activo = true
                });
            }

            var caja = new CajaDeLaOperacion();
            foreach (var pago in compra.Pagos.Where(p => p.Activo && !p.Anulado).OrderBy(p => p.Fecha))
            {
                var errorCaja = await RevertirPagoAsync(compra, pago, $"Anulación de la compra: {motivo}", usuarioId, caja, ct);
                if (errorCaja is not null)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<CompraResponse>.Invalid(errorCaja);
                }
            }

            compra.Estado = EstadoCompra.Anulada;
            compra.FechaAnulacion = DateTime.UtcNow;
            compra.MotivoAnulacion = motivo;
            compra.UsuarioAnulacionId = usuarioId;
            compra.ModificadoPorId = usuarioId;
            compra.FechaModificacion = DateTime.UtcNow;

            Auditar(usuarioId, "Anular", compra.Id, new
            {
                compra.NumeroCompra,
                Comprobante = Comprobante(compra),
                compra.Total,
                Motivo = motivo,
                LineasDevueltas = lineasConStock.Count
            });

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return await ObtenerAsync(id, ct);
    }

    // ------------------------------------------------------------------
    // Pago y caja
    // ------------------------------------------------------------------

    /// <summary>
    /// La caja abierta, bloqueada una sola vez por operación. Releerla en cada pago
    /// borraría el saldo ya descontado por el pago anterior de la misma operación.
    /// </summary>
    private sealed class CajaDeLaOperacion
    {
        public bool Consultada { get; set; }
        public CajaChica? Caja { get; set; }
    }

    private async Task<CajaChica?> CajaAbiertaBloqueadaAsync(CajaDeLaOperacion estado, CancellationToken ct)
    {
        if (estado.Consultada)
            return estado.Caja;

        estado.Consultada = true;

        var cajaId = await _context.CajasChicas
            .Where(c => c.Estado == EstadoCajaChica.Abierta)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (cajaId is null)
            return null;

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM \"CajasChicas\" WHERE \"Id\" = {cajaId.Value} FOR UPDATE", ct);

        var caja = await _context.CajasChicas.FirstAsync(c => c.Id == cajaId.Value, ct);
        // Se relee después del bloqueo: otro pago o el cierre pudo cambiarla mientras se esperaba.
        await _context.Entry(caja).ReloadAsync(ct);

        estado.Caja = caja.Estado == EstadoCajaChica.Abierta ? caja : null;
        return estado.Caja;
    }

    /// <summary>Agrega el pago; si es en efectivo, lo saca de la caja. Devuelve el error, si lo hay.</summary>
    private async Task<string?> AgregarPagoAsync(
        Compra compra,
        string proveedorNombre,
        RegistrarPagoCompraRequest request,
        MetodoPago metodo,
        Guid? usuarioId,
        CajaDeLaOperacion caja,
        CancellationToken ct)
    {
        var monto = CalculoCompra.Redondear(request.Monto);
        var pago = new PagoCompra
        {
            CompraId = compra.Id,
            Monto = monto,
            MontoSoles = CalculoCompra.Redondear(monto * compra.TipoCambio),
            MetodoPagoId = metodo.Id,
            Fecha = DateTime.UtcNow,
            Referencia = Opcional(request.Referencia),
            Observaciones = Opcional(request.Observaciones),
            UsuarioId = usuarioId,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };
        _context.PagosCompra.Add(pago);

        // Yape, tarjeta o transferencia no pasan por el cajón: no hay movimiento de caja.
        if (!CajaChicaService.EsCodigoEfectivo(metodo.Codigo))
            return null;

        var cajaAbierta = await CajaAbiertaBloqueadaAsync(caja, ct);
        if (cajaAbierta is null)
            return MensajeEfectivoSinCaja;

        if (pago.MontoSoles > cajaAbierta.SaldoCalculado)
            return $"El pago en efectivo ({Soles(pago.MontoSoles)}) supera lo que hay en la caja chica ({Soles(cajaAbierta.SaldoCalculado)}).";

        cajaAbierta.SaldoCalculado -= pago.MontoSoles;
        cajaAbierta.FechaModificacion = DateTime.UtcNow;

        _context.MovimientosCajaChica.Add(new MovimientoCajaChica
        {
            CajaChicaId = cajaAbierta.Id,
            Tipo = TipoMovimientoCaja.Egreso,
            Monto = pago.MontoSoles,
            Concepto = $"Pago a proveedor {proveedorNombre} · {compra.NumeroCompra} · {Comprobante(compra)}",
            Referencia = pago.Referencia,
            Fecha = DateTime.UtcNow,
            UsuarioId = usuarioId,
            PagoCompraId = pago.Id,
            MetodoPagoId = metodo.Id,
            MetodoPagoNombre = metodo.Nombre,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        });

        return null;
    }

    /// <summary>
    /// Anula el pago. Si salió de la caja, el efectivo vuelve como ingreso a la caja
    /// abierta: una caja cerrada no se toca. Devuelve el error, si lo hay.
    /// </summary>
    private async Task<string?> RevertirPagoAsync(
        Compra compra,
        PagoCompra pago,
        string motivo,
        Guid? usuarioId,
        CajaDeLaOperacion caja,
        CancellationToken ct)
    {
        var salioDeCaja = await _context.MovimientosCajaChica.AnyAsync(m =>
            m.PagoCompraId == pago.Id && m.Tipo == TipoMovimientoCaja.Egreso && m.Activo, ct);

        if (salioDeCaja)
        {
            var cajaAbierta = await CajaAbiertaBloqueadaAsync(caja, ct);
            if (cajaAbierta is null)
                return $"El pago de {Soles(pago.MontoSoles)} salió en efectivo de la caja: abre la caja chica para que ese dinero vuelva a ella al anularlo.";

            cajaAbierta.SaldoCalculado += pago.MontoSoles;
            cajaAbierta.FechaModificacion = DateTime.UtcNow;

            _context.MovimientosCajaChica.Add(new MovimientoCajaChica
            {
                CajaChicaId = cajaAbierta.Id,
                Tipo = TipoMovimientoCaja.Ingreso,
                Monto = pago.MontoSoles,
                Concepto = $"Anulación de pago a proveedor {compra.Proveedor.RazonSocial} · {compra.NumeroCompra}",
                Referencia = pago.Referencia,
                Fecha = DateTime.UtcNow,
                UsuarioId = usuarioId,
                PagoCompraId = pago.Id,
                MetodoPagoId = pago.MetodoPagoId,
                MetodoPagoNombre = pago.MetodoPago.Nombre,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            });
        }

        pago.Anulado = true;
        pago.FechaAnulacion = DateTime.UtcNow;
        pago.MotivoAnulacion = motivo.Length > 500 ? motivo[..500] : motivo;
        pago.UsuarioAnulacionId = usuarioId;
        pago.ModificadoPorId = usuarioId;
        pago.FechaModificacion = DateTime.UtcNow;

        return null;
    }

    // ------------------------------------------------------------------
    // Validaciones
    // ------------------------------------------------------------------

    private sealed record CabeceraCompra(
        Guid ProveedorId,
        string ProveedorNombre,
        TipoComprobanteCompra TipoComprobante,
        string Serie,
        string Numero);

    private async Task<(CabeceraCompra? Cabecera, ServiceResult<CompraResponse>? Error)> ValidarCabeceraAsync(
        Guid proveedorId,
        TipoComprobanteCompra tipoComprobante,
        string? serieTexto,
        string? numeroTexto,
        DateOnly fechaEmision,
        DateOnly? fechaVencimiento,
        Guid? excluirCompraId,
        Guid? proveedorActualId,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(tipoComprobante))
            return (null, ServiceResult<CompraResponse>.Invalid("El tipo de comprobante no es válido."));

        var serie = serieTexto?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!SerieValida().IsMatch(serie))
            return (null, ServiceResult<CompraResponse>.Invalid("La serie es obligatoria: hasta 10 letras o números, como F001."));

        var numero = CalculoCompra.NormalizarNumero(numeroTexto ?? string.Empty);
        if (!NumeroValido().IsMatch(numero))
            return (null, ServiceResult<CompraResponse>.Invalid("El número es obligatorio: hasta 20 letras, números o guiones."));

        if (fechaEmision == default)
            return (null, ServiceResult<CompraResponse>.Invalid("La fecha de emisión es obligatoria."));

        if (fechaEmision > HoraPeru.Hoy())
            return (null, ServiceResult<CompraResponse>.Invalid("La fecha de emisión no puede ser futura."));

        if (fechaVencimiento.HasValue && fechaVencimiento.Value < fechaEmision)
            return (null, ServiceResult<CompraResponse>.Invalid("El vencimiento no puede ser anterior a la emisión."));

        // Al editar se acepta el proveedor que ya tenía aunque luego se haya dado de baja.
        var proveedor = await _context.Proveedores.AsNoTracking().FirstOrDefaultAsync(p =>
            p.Id == proveedorId && (p.Activo || p.Id == proveedorActualId), ct);
        if (proveedor is null)
            return (null, ServiceResult<CompraResponse>.Invalid("El proveedor no existe o fue dado de baja."));

        var compraExistente = await _context.Compras.AsNoTracking()
            .Where(c => c.Activo
                && c.Estado == EstadoCompra.Registrada
                && c.ProveedorId == proveedorId
                && c.TipoComprobante == tipoComprobante
                && c.Serie == serie
                && c.Numero == numero
                && (excluirCompraId == null || c.Id != excluirCompraId))
            .Select(c => c.NumeroCompra)
            .FirstOrDefaultAsync(ct);
        if (compraExistente is not null)
            return (null, ServiceResult<CompraResponse>.Conflict(
                $"La {NombreComprobante(tipoComprobante).ToLowerInvariant()} {serie}-{numero} de este proveedor ya está registrada en la compra {compraExistente}."));

        return (new CabeceraCompra(proveedor.Id, proveedor.RazonSocial, tipoComprobante, serie, numero), null);
    }

    private static string? ValidarTextos(string? guiaRemision, string? observaciones)
    {
        if (guiaRemision?.Trim().Length > 50)
            return "La guía de remisión no puede pasar de 50 caracteres.";
        if (observaciones?.Trim().Length > 500)
            return "Las observaciones no pueden pasar de 500 caracteres.";
        return null;
    }

    private static string? ValidarPago(RegistrarPagoCompraRequest pago, MetodoPago? metodo)
    {
        if (pago.Monto <= 0)
            return "Cada pago debe ser mayor a 0.";
        if (metodo is null)
            return "El método de pago indicado no existe o está inactivo.";
        if (pago.Referencia?.Trim().Length > 100)
            return "La referencia del pago no puede pasar de 100 caracteres.";
        if (pago.Observaciones?.Trim().Length > 500)
            return "Las observaciones del pago no pueden pasar de 500 caracteres.";
        return null;
    }

    private static (string? Motivo, string? Error) ValidarMotivo(AnularRequest? request)
    {
        var motivo = request?.Motivo?.Trim() ?? string.Empty;
        if (motivo.Length == 0)
            return (null, "Escribe el motivo de la anulación.");
        if (motivo.Length > 500)
            return (null, "El motivo no puede pasar de 500 caracteres.");
        return (motivo, null);
    }

    // ------------------------------------------------------------------
    // Apoyo
    // ------------------------------------------------------------------

    private sealed record SituacionPago(decimal Pagado, decimal Saldo, string? EstadoPago, bool Vencida);

    private static SituacionPago Situacion(Compra compra, decimal pagado, DateOnly hoy)
    {
        if (compra.Estado == EstadoCompra.Anulada)
            return new SituacionPago(pagado, 0m, null, false);

        var saldo = Math.Max(0m, compra.Total - pagado);
        string estadoPago;
        if (saldo <= 0)
            estadoPago = EstadosPagoCompra.Pagada;
        else if (pagado > 0)
            estadoPago = EstadosPagoCompra.Parcial;
        else
            estadoPago = EstadosPagoCompra.Pendiente;
        var vencida = saldo > 0 && compra.FechaVencimiento.HasValue && compra.FechaVencimiento.Value < hoy;

        return new SituacionPago(pagado, saldo, estadoPago, vencida);
    }

    private static string EstadoParaExcel(CompraResumenResponse compra)
    {
        if (compra.Estado == EstadoCompra.Anulada)
            return "Anulada";
        return compra.Vencida ? "Vencida" : compra.EstadoPago ?? string.Empty;
    }

    private static bool CumpleEstadoPago(string? filtro, SituacionPago situacion) => filtro switch
    {
        null or "" => true,
        EstadosPagoCompra.PorPagar => situacion.Saldo > 0,
        EstadosPagoCompra.Vencida => situacion.Vencida,
        _ => string.Equals(situacion.EstadoPago, filtro, StringComparison.OrdinalIgnoreCase)
    };

    private Task<decimal> TotalPagadoAsync(Guid compraId, CancellationToken ct) =>
        _context.PagosCompra
            .Where(p => p.CompraId == compraId && p.Activo && !p.Anulado)
            .SumAsync(p => p.Monto, ct);

    private Task BloquearCompraAsync(Guid compraId, CancellationToken ct) =>
        _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM \"Compras\" WHERE \"Id\" = {compraId} FOR UPDATE", ct);

    /// <summary>Bloquea los repuestos en orden de id, como ventas y pedidos, y los relee ya bloqueados.</summary>
    private async Task<Dictionary<Guid, Producto>> BloquearProductosAsync(IEnumerable<Guid> productoIds, CancellationToken ct)
    {
        var ids = productoIds.Distinct().OrderBy(id => id).ToList();
        foreach (var productoId in ids)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {productoId} FOR UPDATE", ct);
        }

        var productos = await _context.Productos
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
        foreach (var producto in productos.Values)
        {
            await _context.Entry(producto).ReloadAsync(ct);
        }

        return productos;
    }

    private async Task<string> SiguienteNumeroAsync(CancellationToken ct)
    {
        var valor = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('\"CompraNumeroSeq\"') AS \"Value\"")
            .SingleAsync(ct);
        return $"CO-{valor:D6}";
    }

    private static bool EsDuplicado(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private void Auditar(Guid? usuarioId, string accion, Guid compraId, object detalle) =>
        _context.EventosAuditoria.Add(new EventoAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Accion = accion,
            Entidad = "Compra",
            EntidadId = compraId.ToString(),
            Detalle = JsonSerializer.Serialize(detalle)
        });

    private static string? Opcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string Soles(decimal monto) => $"S/ {monto.ToString("N2", CultureInfo.InvariantCulture)}";

    private static string Monto(MonedaCompra moneda, decimal monto) =>
        moneda == MonedaCompra.USD
            ? $"US$ {monto.ToString("N2", CultureInfo.InvariantCulture)}"
            : Soles(monto);

    public static string NombreComprobante(TipoComprobanteCompra tipo) => tipo switch
    {
        TipoComprobanteCompra.Factura => "Factura",
        TipoComprobanteCompra.Boleta => "Boleta",
        TipoComprobanteCompra.Ticket => "Ticket",
        TipoComprobanteCompra.NotaVenta => "Nota de venta",
        TipoComprobanteCompra.ReciboHonorarios => "Recibo por honorarios",
        _ => "Comprobante"
    };

    private static string Comprobante(Compra compra) =>
        $"{NombreComprobante(compra.TipoComprobante)} {compra.Serie}-{compra.Numero}";

    private static CompraResponse Mapear(Compra c, HashSet<Guid> pagosConCaja, DateOnly hoy)
    {
        var pagado = c.Pagos.Where(p => p.Activo && !p.Anulado).Sum(p => p.Monto);
        var situacion = Situacion(c, pagado, hoy);

        return new CompraResponse(
            c.Id,
            c.NumeroCompra,
            c.ProveedorId,
            c.Proveedor.RazonSocial,
            c.Proveedor.NumeroDocumento,
            c.TipoComprobante,
            c.Serie,
            c.Numero,
            c.FechaEmision,
            c.FechaVencimiento,
            c.Moneda,
            c.TipoCambio,
            c.PorcentajeIgv,
            c.PreciosIncluyenIgv,
            c.SubtotalGravado,
            c.SubtotalExonerado,
            c.SubtotalInafecto,
            c.MontoIgv,
            c.Total,
            c.TotalSoles,
            situacion.Pagado,
            situacion.Saldo,
            situacion.EstadoPago,
            situacion.Vencida,
            c.Estado,
            c.PedidoLimaId,
            c.PedidoLima?.NumeroPedido,
            c.GuiaRemision,
            c.Observaciones,
            c.Usuario?.NombreCompleto,
            c.FechaCreacion,
            c.FechaAnulacion,
            c.MotivoAnulacion,
            c.UsuarioAnulacion?.NombreCompleto,
            c.Detalles
                .Where(d => d.Activo)
                .OrderBy(d => d.Orden)
                .Select(d => new DetalleCompraResponse(
                    d.Id,
                    d.ProductoId,
                    d.Producto?.Codigo,
                    d.Descripcion,
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.TipoAfectacionIgv,
                    d.Subtotal,
                    d.MontoIgv,
                    d.Total,
                    d.CostoUnitarioSoles,
                    d.MueveStock))
                .ToList(),
            c.Pagos
                .Where(p => p.Activo)
                .OrderBy(p => p.Fecha)
                .Select(p => new PagoCompraResponse(
                    p.Id,
                    p.Monto,
                    p.MontoSoles,
                    p.MetodoPagoId,
                    p.MetodoPago.Nombre,
                    p.MetodoPago.Codigo,
                    p.Fecha,
                    p.Referencia,
                    p.Observaciones,
                    p.Usuario?.NombreCompleto,
                    pagosConCaja.Contains(p.Id),
                    p.Anulado,
                    p.FechaAnulacion,
                    p.MotivoAnulacion,
                    p.UsuarioAnulacion?.NombreCompleto))
                .ToList());
    }

    [GeneratedRegex(@"^[A-Z0-9]{1,10}$")]
    private static partial Regex SerieValida();

    [GeneratedRegex(@"^[A-Z0-9-]{1,20}$")]
    private static partial Regex NumeroValido();

    [GeneratedRegex(@"^([A-Z0-9]{1,10})-(\d+)$")]
    private static partial Regex ComprobanteBuscado();
}
