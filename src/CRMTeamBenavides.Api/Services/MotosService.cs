using CRMTeamBenavides.Api.Features.Motos;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

/// <summary>La lista de motos del inventario y el ingreso de las que ya estaban en la tienda.</summary>
public class MotosService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguracionService _configuracion;
    private readonly IAuditoriaService _auditoria;

    public MotosService(ApplicationDbContext context, IConfiguracionService configuracion, IAuditoriaService auditoria)
    {
        _context = context;
        _configuracion = configuracion;
        _auditoria = auditoria;
    }

    public async Task<List<MotoResponse>> ListarAsync(
        Guid? productoId, EstadoUnidadInventario? estado, string? busqueda, CancellationToken ct = default)
    {
        var query = _context.UnidadesInventario.AsNoTracking().Where(u => u.Activo);

        if (productoId.HasValue)
            query = query.Where(u => u.ProductoId == productoId.Value);
        if (estado.HasValue)
            query = query.Where(u => u.Estado == estado.Value);
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var patron = $"%{busqueda.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.NumeroSerieVIN, patron) ||
                EF.Functions.ILike(u.NumeroMotor!, patron) ||
                EF.Functions.ILike(u.Producto.Nombre, patron));
        }

        return await query
            .OrderBy(u => u.Estado).ThenByDescending(u => u.FechaCreacion)
            .Select(u => new MotoResponse(
                u.Id,
                u.ProductoId,
                u.Producto.Codigo,
                u.Producto.Nombre,
                u.NumeroSerieVIN,
                u.NumeroMotor,
                u.Anio,
                u.Color,
                u.Dua,
                u.ItemDua,
                u.CostoUnitario,
                u.Estado == EstadoUnidadInventario.EnStock ? "En stock" : "Vendida",
                (int)u.Estado,
                u.FechaCreacion,
                u.CompraId,
                u.Compra != null ? u.Compra.NumeroCompra : null,
                u.VentaId,
                u.FechaVenta,
                u.VehiculoId,
                u.Venta != null ? u.Venta.Cliente.NombreCompleto : null))
            .ToListAsync(ct);
    }

    /// <summary>Ingresa al stock una moto que ya estaba en la tienda, sin compra.</summary>
    public async Task<ServiceResult<MotoResponse>> IngresarAsync(
        Guid productoId, IngresarMotoRequest request, Guid? usuarioId, CancellationToken ct = default)
    {
        if (request.Costo < 0 || request.Costo > 10_000_000)
            return ServiceResult<MotoResponse>.Invalid("El costo de la moto no es válido.");

        var (motos, error) = MotosInventario.ValidarIngreso([request.Moto], 1, "La moto");
        if (error is not null)
            return ServiceResult<MotoResponse>.Invalid(error);
        var moto = motos[0];

        var ocupada = await MotosInventario.SerieOcupadaAsync(_context, [moto.NumeroSerieVIN], ct);
        if (ocupada is not null)
            return ServiceResult<MotoResponse>.Invalid(ocupada);

        var configuracion = await _configuracion.ObtenerConfiguracionEmpresaAsync(ct);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        if (_context.Database.IsRelational())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"Productos\" WHERE \"Id\" = {productoId} FOR UPDATE", ct);
        }

        var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == productoId && p.Activo, ct);
        if (producto is null)
            return ServiceResult<MotoResponse>.NotFound("El modelo no existe o fue dado de baja.");
        if (producto.Tipo != TipoProducto.Vehiculo)
            return ServiceResult<MotoResponse>.Invalid("«" + producto.Nombre + "» es un repuesto: las motos se ingresan en un modelo de la pestaña Vehículos.");

        var unidad = MotosInventario.NuevaUnidad(producto.Id, moto, request.Costo, usuarioId);
        _context.UnidadesInventario.Add(unidad);

        producto.Costo = CalculoCompra.NuevoCosto(configuracion.MetodoCosteo, producto.StockActual, producto.Costo, 1, unidad.CostoUnitario);
        producto.StockActual += 1;
        producto.ModificadoPorId = usuarioId;
        producto.FechaModificacion = DateTime.UtcNow;

        _context.MovimientosInventario.Add(new MovimientoInventario
        {
            ProductoId = producto.Id,
            Tipo = TipoMovimientoInventario.Entrada,
            Cantidad = 1,
            CostoUnitario = unidad.CostoUnitario,
            Motivo = $"Ingreso de la moto {moto.NumeroSerieVIN} (inventario inicial)",
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true,
        });

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _auditoria.RegistrarEventoAsync(usuarioId, "Crear", "UnidadInventario", unidad.Id.ToString(),
            new { producto.Codigo, moto.NumeroSerieVIN, unidad.CostoUnitario, Origen = "Inventario inicial" }, ct);

        var lista = await ListarAsync(producto.Id, EstadoUnidadInventario.EnStock, null, ct);
        return ServiceResult<MotoResponse>.Success(lista.First(m => m.Id == unidad.Id));
    }
}
