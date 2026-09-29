using CRMTeamBenavides.Api.Features.Servicios;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class ServicioService : IServicioService
{
    private readonly ApplicationDbContext _context;

    public ServicioService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServicioResponse>> GetAllAsync(bool soloActivos = true, CancellationToken ct = default)
    {
        var query = _context.Servicios.AsNoTracking();
        if (soloActivos)
            query = query.Where(s => s.Activo);

        return await query
            .OrderBy(s => s.Nombre)
            .Select(s => new ServicioResponse(
                s.Id,
                s.Nombre,
                s.PrecioSugerido,
                s.TipoAfectacionIgv,
                s.Activo,
                s.FechaCreacion))
            .ToListAsync(ct);
    }

    public async Task<ServicioResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _context.Servicios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s == null) return null;

        return new ServicioResponse(
            s.Id,
            s.Nombre,
            s.PrecioSugerido,
            s.TipoAfectacionIgv,
            s.Activo,
            s.FechaCreacion);
    }

    public async Task<ServiceResult<ServicioResponse>> CreateAsync(CrearServicioRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return ServiceResult<ServicioResponse>.Invalid("El nombre del servicio es obligatorio.");

        if (request.PrecioSugerido < 0)
            return ServiceResult<ServicioResponse>.Invalid("El precio sugerido no puede ser negativo.");

        var servicio = new Servicio
        {
            Nombre = request.Nombre.Trim(),
            PrecioSugerido = request.PrecioSugerido,
            TipoAfectacionIgv = request.TipoAfectacionIgv,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Servicios.Add(servicio);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<ServicioResponse>.Success(
            new ServicioResponse(servicio.Id, servicio.Nombre, servicio.PrecioSugerido, servicio.TipoAfectacionIgv, servicio.Activo, servicio.FechaCreacion));
    }

    public async Task<ServiceResult<ServicioResponse>> UpdateAsync(Guid id, ActualizarServicioRequest request, CancellationToken ct = default)
    {
        var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (servicio == null)
            return ServiceResult<ServicioResponse>.NotFound();

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return ServiceResult<ServicioResponse>.Invalid("El nombre del servicio es obligatorio.");

        if (request.PrecioSugerido < 0)
            return ServiceResult<ServicioResponse>.Invalid("El precio sugerido no puede ser negativo.");

        servicio.Nombre = request.Nombre.Trim();
        servicio.PrecioSugerido = request.PrecioSugerido;
        servicio.TipoAfectacionIgv = request.TipoAfectacionIgv;
        if (request.Activo.HasValue)
            servicio.Activo = request.Activo.Value;
        servicio.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return ServiceResult<ServicioResponse>.Success(
            new ServicioResponse(servicio.Id, servicio.Nombre, servicio.PrecioSugerido, servicio.TipoAfectacionIgv, servicio.Activo, servicio.FechaCreacion));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (servicio == null)
            return ServiceResult<bool>.NotFound();

        servicio.Activo = false;
        servicio.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<bool>.Success(true);
    }
}
