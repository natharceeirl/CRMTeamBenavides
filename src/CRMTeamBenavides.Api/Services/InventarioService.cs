using CRMTeamBenavides.Api.Features.Inventario;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class InventarioService : IInventarioService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public InventarioService(ApplicationDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<ProductoResponse>> GetAllProductosAsync(Guid? categoriaId, string? busqueda, bool? bajoStock, string? marca = null)
    {
        var query = _context.Productos
            .Include(p => p.Categoria)
            .Where(p => p.Activo);

        if (categoriaId.HasValue)
        {
            query = query.Where(p => p.CategoriaId == categoriaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(marca))
        {
            var m = marca.Trim().ToLower();
            query = query.Where(p => p.Marca != null && p.Marca.ToLower() == m);
        }

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.Trim().ToLower();
            query = query.Where(p => p.Codigo.ToLower().Contains(term) || p.Nombre.ToLower().Contains(term) || (p.Marca != null && p.Marca.ToLower().Contains(term)));
        }

        if (bajoStock.HasValue && bajoStock.Value)
        {
            query = query.Where(p => p.StockActual <= (p.StockMinimo ?? (p.Categoria.StockMinimoDefault ?? 4)));
        }

        return await query
            .OrderBy(p => p.Nombre)
            .Select(p => MapToResponse(p))
            .ToListAsync();
    }

    public async Task<List<string>> ObtenerMarcasAsync()
    {
        return await _context.Productos
            .Where(p => p.Activo && !string.IsNullOrWhiteSpace(p.Marca))
            .Select(p => p.Marca!.Trim())
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();
    }

    public async Task<ServiceResult<ProductoResponse>> GetProductoByIdAsync(Guid id)
    {
        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);

        return producto is null
            ? ServiceResult<ProductoResponse>.NotFound()
            : ServiceResult<ProductoResponse>.Success(MapToResponse(producto));
    }

    public async Task<ServiceResult<ProductoResponse>> CreateProductoAsync(CreateProductoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            return ServiceResult<ProductoResponse>.Invalid("El código (SKU) es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return ServiceResult<ProductoResponse>.Invalid("El nombre del producto es obligatorio.");
        }

        if (request.PrecioVenta < 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("El precio de venta debe ser mayor o igual a 0.");
        }

        if (request.StockInicial < 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("El stock inicial debe ser mayor o igual a 0.");
        }

        if (request.StockMinimo.HasValue && request.StockMinimo.Value < 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("El stock mínimo debe ser mayor o igual a 0.");
        }

        if (request.Costo < 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("El costo debe ser mayor o igual a 0.");
        }

        var categoria = await _context.CategoriasProducto
            .FirstOrDefaultAsync(c => c.Id == request.CategoriaId && c.Activo);

        if (categoria is null)
        {
            return ServiceResult<ProductoResponse>.Invalid("La categoría indicada no existe o está inactiva.");
        }

        var codigoNormalizado = request.Codigo.Trim();
        var codigoExiste = await _context.Productos
            .AnyAsync(p => p.Codigo.ToLower() == codigoNormalizado.ToLower());

        if (codigoExiste)
        {
            return ServiceResult<ProductoResponse>.Invalid($"Ya existe un producto registrado con el código '{codigoNormalizado}'.");
        }

        var unidad = string.IsNullOrWhiteSpace(request.Unidad) ? "unidad" : request.Unidad.Trim();

        var producto = new Producto
        {
            CategoriaId   = request.CategoriaId,
            Categoria     = categoria,
            Codigo        = codigoNormalizado,
            Nombre        = request.Nombre.Trim(),
            Descripcion   = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
            Unidad        = unidad,
            PrecioVenta   = request.PrecioVenta,
            Costo         = request.Costo,
            StockActual   = request.StockInicial,
            StockMinimo   = request.StockMinimo,
            Marca         = string.IsNullOrWhiteSpace(request.Marca) ? null : request.Marca.Trim(),
            FotoUrl       = string.IsNullOrWhiteSpace(request.FotoUrl) ? null : request.FotoUrl.Trim(),
            FechaCreacion = DateTime.UtcNow,
            Activo        = true
        };

        if (request.StockInicial > 0)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();

                var movimiento = new MovimientoInventario
                {
                    ProductoId    = producto.Id,
                    Tipo          = TipoMovimientoInventario.Entrada,
                    Cantidad      = request.StockInicial,
                    CostoUnitario = request.Costo,
                    Motivo        = "Inventario inicial",
                    FechaCreacion = DateTime.UtcNow,
                    Activo        = true
                };
                _context.MovimientosInventario.Add(movimiento);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();
        }

        return ServiceResult<ProductoResponse>.Success(MapToResponse(producto));
    }

    public async Task<ServiceResult<ProductoResponse>> AltaRapidaProductoAsync(AltaRapidaProductoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return ServiceResult<ProductoResponse>.Invalid("El nombre del producto/repuesto es obligatorio.");
        }

        if (request.PrecioVenta < 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("El precio de venta debe ser mayor o igual a 0.");
        }

        Guid categoriaId;
        if (request.CategoriaId.HasValue)
        {
            var cat = await _context.CategoriasProducto.FirstOrDefaultAsync(c => c.Id == request.CategoriaId.Value && c.Activo);
            if (cat == null)
            {
                return ServiceResult<ProductoResponse>.Invalid("La categoría especificada no existe.");
            }
            categoriaId = cat.Id;
        }
        else
        {
            var defaultCat = await _context.CategoriasProducto.FirstOrDefaultAsync(c => c.Activo);
            if (defaultCat == null)
            {
                defaultCat = new CategoriaProducto
                {
                    Nombre = "Repuestos y Servicios",
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };
                _context.CategoriasProducto.Add(defaultCat);
                await _context.SaveChangesAsync();
            }
            categoriaId = defaultCat.Id;
        }

        var codigo = !string.IsNullOrWhiteSpace(request.Codigo)
            ? request.Codigo.Trim().ToUpperInvariant()
            : $"REP-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var existente = await _context.Productos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Codigo == codigo && p.Activo);
        if (existente != null)
        {
            return ServiceResult<ProductoResponse>.Success(MapToResponse(existente));
        }

        var createReq = new CreateProductoRequest(
            categoriaId,
            codigo,
            request.Nombre.Trim(),
            null,
            "UND",
            request.PrecioVenta,
            Math.Max(0, request.StockInicial),
            StockMinimo: 2,
            Costo: 0m,
            Marca: request.Marca?.Trim(),
            FotoUrl: null
        );

        return await CreateProductoAsync(createReq);
    }

    public async Task<ServiceResult<ProductoResponse>> UpdateProductoAsync(Guid id, UpdateProductoRequest request)
    {
        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);

        if (producto is null)
        {
            return ServiceResult<ProductoResponse>.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            return ServiceResult<ProductoResponse>.Invalid("El código (SKU) es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return ServiceResult<ProductoResponse>.Invalid("El nombre del producto es obligatorio.");
        }

        if (request.PrecioVenta < 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("El precio de venta debe ser mayor o igual a 0.");
        }

        if (request.StockMinimo.HasValue && request.StockMinimo.Value < 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("El stock mínimo debe ser mayor o igual a 0.");
        }

        if (request.Costo.HasValue && request.Costo.Value < 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("El costo debe ser mayor o igual a 0.");
        }

        var categoria = await _context.CategoriasProducto
            .FirstOrDefaultAsync(c => c.Id == request.CategoriaId && c.Activo);

        if (categoria is null)
        {
            return ServiceResult<ProductoResponse>.Invalid("La categoría indicada no existe o está inactiva.");
        }

        var codigoNormalizado = request.Codigo.Trim();
        var codigoExiste = await _context.Productos
            .AnyAsync(p => p.Codigo.ToLower() == codigoNormalizado.ToLower() && p.Id != id);

        if (codigoExiste)
        {
            return ServiceResult<ProductoResponse>.Invalid($"Ya existe otro producto registrado con el código '{codigoNormalizado}'.");
        }

        producto.CategoriaId       = request.CategoriaId;
        producto.Categoria         = categoria;
        producto.Codigo            = codigoNormalizado;
        producto.Nombre            = request.Nombre.Trim();
        producto.Descripcion       = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        producto.Unidad            = string.IsNullOrWhiteSpace(request.Unidad) ? "unidad" : request.Unidad.Trim();
        producto.PrecioVenta       = request.PrecioVenta;
        if (request.Costo.HasValue)
        {
            producto.Costo = request.Costo.Value;
        }
        producto.StockMinimo       = request.StockMinimo;
        if (request.Marca != null)
        {
            producto.Marca = string.IsNullOrWhiteSpace(request.Marca) ? null : request.Marca.Trim();
        }
        if (request.FotoUrl != null)
        {
            producto.FotoUrl = string.IsNullOrWhiteSpace(request.FotoUrl) ? null : request.FotoUrl.Trim();
        }
        producto.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<ProductoResponse>.Success(MapToResponse(producto));
    }

    public async Task<ServiceResult<ProductoResponse>> ActualizarFotoAsync(Guid id, string fotoUrl)
    {
        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);

        if (producto is null)
        {
            return ServiceResult<ProductoResponse>.NotFound();
        }

        producto.FotoUrl = string.IsNullOrWhiteSpace(fotoUrl) ? null : fotoUrl.Trim();
        producto.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ServiceResult<ProductoResponse>.Success(MapToResponse(producto));
    }

    public async Task<ServiceResult<bool>> DeleteProductoAsync(Guid id)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);

        if (producto is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        producto.Activo = false;
        producto.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<ProductoResponse>> RegistrarEntradaAsync(Guid productoId, RegistrarEntradaRequest request)
    {
        if (request.Cantidad <= 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("La cantidad debe ser mayor a 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return ServiceResult<ProductoResponse>.Invalid("El motivo de la entrada es obligatorio.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Bloqueo pesimista a nivel de fila (FOR UPDATE) para evitar condiciones de carrera en stock
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {productoId} FOR UPDATE");

            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == productoId && p.Activo);

            if (producto is null)
            {
                return ServiceResult<ProductoResponse>.NotFound();
            }

            producto.StockActual += request.Cantidad;
            producto.FechaModificacion = DateTime.UtcNow;

            var movimiento = new MovimientoInventario
            {
                ProductoId    = producto.Id,
                Tipo          = TipoMovimientoInventario.Entrada,
                Cantidad      = request.Cantidad,
                CostoUnitario = request.CostoUnitario ?? producto.Costo,
                Motivo        = request.Motivo.Trim(),
                FechaCreacion = DateTime.UtcNow,
                Activo        = true
            };
            _context.MovimientosInventario.Add(movimiento);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult<ProductoResponse>.Success(MapToResponse(producto));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult<ProductoResponse>> RegistrarSalidaAsync(Guid productoId, RegistrarSalidaRequest request)
    {
        if (request.Cantidad <= 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("La cantidad debe ser mayor a 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return ServiceResult<ProductoResponse>.Invalid("El motivo de la salida es obligatorio.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Bloqueo pesimista a nivel de fila (FOR UPDATE) para evitar condiciones de carrera en stock
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {productoId} FOR UPDATE");

            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == productoId && p.Activo);

            if (producto is null)
            {
                return ServiceResult<ProductoResponse>.NotFound();
            }

            if (producto.StockActual < request.Cantidad)
            {
                return ServiceResult<ProductoResponse>.Invalid(
                    $"Stock insuficiente. Stock disponible: {producto.StockActual}, solicitado: {request.Cantidad}.");
            }

            producto.StockActual -= request.Cantidad;
            producto.FechaModificacion = DateTime.UtcNow;

            var movimiento = new MovimientoInventario
            {
                ProductoId    = producto.Id,
                Tipo          = TipoMovimientoInventario.Salida,
                Cantidad      = request.Cantidad,
                CostoUnitario = producto.Costo,
                Motivo        = request.Motivo.Trim(),
                FechaCreacion = DateTime.UtcNow,
                Activo        = true
            };
            _context.MovimientosInventario.Add(movimiento);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult<ProductoResponse>.Success(MapToResponse(producto));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult<ProductoResponse>> RegistrarAjusteAsync(Guid productoId, RegistrarAjusteRequest request, Guid? usuarioId = null)
    {
        if (request.NuevoStock < 0)
        {
            return ServiceResult<ProductoResponse>.Invalid("El nuevo stock debe ser mayor o igual a 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return ServiceResult<ProductoResponse>.Invalid("El motivo del ajuste es obligatorio.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Bloqueo pesimista a nivel de fila (FOR UPDATE) para evitar condiciones de carrera en stock
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {productoId} FOR UPDATE");

            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == productoId && p.Activo);

            if (producto is null)
            {
                return ServiceResult<ProductoResponse>.NotFound();
            }

            var stockAnterior = producto.StockActual;
            var diferencia = Math.Abs(request.NuevoStock - producto.StockActual);

            producto.StockActual       = request.NuevoStock;
            producto.FechaModificacion = DateTime.UtcNow;

            var movimiento = new MovimientoInventario
            {
                ProductoId    = producto.Id,
                Tipo          = TipoMovimientoInventario.Ajuste,
                Cantidad      = diferencia,
                CostoUnitario = request.CostoUnitario ?? producto.Costo,
                Motivo        = request.Motivo.Trim(),
                FechaCreacion = DateTime.UtcNow,
                Activo        = true
            };
            _context.MovimientosInventario.Add(movimiento);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _auditoriaService.RegistrarEventoAsync(
                usuarioId,
                "Ajuste",
                "Inventario",
                producto.Id.ToString(),
                new
                {
                    ProductoId = producto.Id,
                    ProductoCodigo = producto.Codigo,
                    StockAnterior = stockAnterior,
                    NuevoStock = request.NuevoStock,
                    Diferencia = request.NuevoStock - stockAnterior,
                    Motivo = request.Motivo.Trim()
                });

            return ServiceResult<ProductoResponse>.Success(MapToResponse(producto));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult<List<MovimientoInventarioResponse>>> GetMovimientosByProductoAsync(Guid productoId)
    {
        var productoExiste = await _context.Productos.AnyAsync(p => p.Id == productoId);
        if (!productoExiste)
        {
            return ServiceResult<List<MovimientoInventarioResponse>>.NotFound();
        }

        var movimientos = await _context.MovimientosInventario
            .Include(m => m.Producto)
            .Where(m => m.ProductoId == productoId && m.Activo)
            .OrderByDescending(m => m.FechaCreacion)
            .Select(m => new MovimientoInventarioResponse(
                m.Id,
                m.ProductoId,
                m.Producto.Codigo,
                m.Producto.Nombre,
                m.Tipo.ToString(),
                (int)m.Tipo,
                m.Cantidad,
                m.Motivo,
                m.OrdenServicioId,
                m.VentaId,
                m.FechaCreacion,
                m.CostoUnitario))
            .ToListAsync();

        return ServiceResult<List<MovimientoInventarioResponse>>.Success(movimientos);
    }

    public async Task<List<MovimientoInventarioResponse>> GetAllMovimientosAsync(
        Guid? productoId,
        TipoMovimientoInventario? tipo,
        DateTime? fechaDesde,
        DateTime? fechaHasta)
    {
        var query = _context.MovimientosInventario
            .Include(m => m.Producto)
            .Where(m => m.Activo);

        if (productoId.HasValue)
        {
            query = query.Where(m => m.ProductoId == productoId.Value);
        }

        if (tipo.HasValue)
        {
            query = query.Where(m => m.Tipo == tipo.Value);
        }

        if (fechaDesde.HasValue)
        {
            var desdeUtc = DateTime.SpecifyKind(fechaDesde.Value, DateTimeKind.Utc);
            query = query.Where(m => m.FechaCreacion >= desdeUtc);
        }

        if (fechaHasta.HasValue)
        {
            var hastaUtc = DateTime.SpecifyKind(fechaHasta.Value, DateTimeKind.Utc);
            query = query.Where(m => m.FechaCreacion <= hastaUtc);
        }

        return await query
            .OrderByDescending(m => m.FechaCreacion)
            .Select(m => new MovimientoInventarioResponse(
                m.Id,
                m.ProductoId,
                m.Producto.Codigo,
                m.Producto.Nombre,
                m.Tipo.ToString(),
                (int)m.Tipo,
                m.Cantidad,
                m.Motivo,
                m.OrdenServicioId,
                m.VentaId,
                m.FechaCreacion,
                m.CostoUnitario))
            .ToListAsync();
    }

    private static ProductoResponse MapToResponse(Producto p)
    {
        int minimoEfectivo = p.ObtenerStockMinimoEfectivo();
        return new ProductoResponse(
            p.Id,
            p.Codigo,
            p.Nombre,
            p.Descripcion,
            p.Unidad,
            p.PrecioVenta,
            p.StockActual,
            p.StockMinimo,
            p.StockActual <= minimoEfectivo,
            p.CategoriaId,
            p.Categoria?.Nombre ?? string.Empty,
            p.Activo,
            p.FechaCreacion,
            p.Costo,
            minimoEfectivo,
            p.Marca,
            p.FotoUrl);
    }
}
