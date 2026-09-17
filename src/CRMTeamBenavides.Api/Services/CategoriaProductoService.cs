using CRMTeamBenavides.Api.Features.CategoriasProducto;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class CategoriaProductoService : ICategoriaProductoService
{
    private readonly ApplicationDbContext _context;

    public CategoriaProductoService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoriaProductoResponse>> GetAllAsync()
    {
        return await _context.CategoriasProducto
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaProductoResponse(
                c.Id,
                c.Nombre,
                c.Productos.Count(p => p.Activo),
                c.Activo,
                c.FechaCreacion))
            .ToListAsync();
    }

    public async Task<ServiceResult<CategoriaProductoResponse>> GetByIdAsync(Guid id)
    {
        var categoria = await _context.CategoriasProducto
            .Include(c => c.Productos)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo);

        if (categoria is null)
        {
            return ServiceResult<CategoriaProductoResponse>.NotFound();
        }

        var cantidadProductos = categoria.Productos.Count(p => p.Activo);

        return ServiceResult<CategoriaProductoResponse>.Success(new CategoriaProductoResponse(
            categoria.Id,
            categoria.Nombre,
            cantidadProductos,
            categoria.Activo,
            categoria.FechaCreacion));
    }

    public async Task<ServiceResult<CategoriaProductoResponse>> CreateAsync(CreateCategoriaProductoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return ServiceResult<CategoriaProductoResponse>.Invalid("El nombre de la categoría es obligatorio.");
        }

        var nombreNormalizado = request.Nombre.Trim();

        var nombreDuplicado = await _context.CategoriasProducto
            .AnyAsync(c => c.Nombre.ToLower() == nombreNormalizado.ToLower() && c.Activo);

        if (nombreDuplicado)
        {
            return ServiceResult<CategoriaProductoResponse>.Invalid("Ya existe una categoría activa con ese nombre.");
        }

        var categoria = new CategoriaProducto
        {
            Nombre = nombreNormalizado,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        _context.CategoriasProducto.Add(categoria);
        await _context.SaveChangesAsync();

        return ServiceResult<CategoriaProductoResponse>.Success(new CategoriaProductoResponse(
            categoria.Id,
            categoria.Nombre,
            0,
            categoria.Activo,
            categoria.FechaCreacion));
    }

    public async Task<ServiceResult<CategoriaProductoResponse>> UpdateAsync(Guid id, UpdateCategoriaProductoRequest request)
    {
        var categoria = await _context.CategoriasProducto
            .Include(c => c.Productos)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo);

        if (categoria is null)
        {
            return ServiceResult<CategoriaProductoResponse>.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return ServiceResult<CategoriaProductoResponse>.Invalid("El nombre de la categoría es obligatorio.");
        }

        var nombreNormalizado = request.Nombre.Trim();

        var nombreDuplicado = await _context.CategoriasProducto
            .AnyAsync(c => c.Nombre.ToLower() == nombreNormalizado.ToLower() && c.Id != id && c.Activo);

        if (nombreDuplicado)
        {
            return ServiceResult<CategoriaProductoResponse>.Invalid("Ya existe otra categoría activa con ese nombre.");
        }

        categoria.Nombre = nombreNormalizado;
        categoria.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var cantidadProductos = categoria.Productos.Count(p => p.Activo);

        return ServiceResult<CategoriaProductoResponse>.Success(new CategoriaProductoResponse(
            categoria.Id,
            categoria.Nombre,
            cantidadProductos,
            categoria.Activo,
            categoria.FechaCreacion));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
    {
        var categoria = await _context.CategoriasProducto
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo);

        if (categoria is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        var tieneProductosActivos = await _context.Productos
            .AnyAsync(p => p.CategoriaId == id && p.Activo);

        if (tieneProductosActivos)
        {
            return ServiceResult<bool>.Invalid("No se puede desactivar la categoría porque tiene productos activos asociados.");
        }

        categoria.Activo = false;
        categoria.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }
}
