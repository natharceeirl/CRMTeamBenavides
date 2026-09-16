using CRMTeamBenavides.Api.Features.Permisos;
using CRMTeamBenavides.Api.Features.Roles;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class RolService : IRolService
{
    private readonly ApplicationDbContext _context;

    public RolService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RolResponse>> GetAllAsync()
    {
        return await _context.Roles
            .Where(r => r.Activo)
            .OrderBy(r => r.Nombre)
            .Select(r => MapToResponse(r))
            .ToListAsync();
    }

    public async Task<ServiceResult<RolResponse>> GetByIdAsync(Guid id)
    {
        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id && r.Activo);

        return rol is null
            ? ServiceResult<RolResponse>.NotFound()
            : ServiceResult<RolResponse>.Success(MapToResponse(rol));
    }

    public async Task<ServiceResult<RolResponse>> CreateAsync(CreateRolRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return ServiceResult<RolResponse>.Invalid("Nombre es obligatorio.");
        }

        var duplicado = await _context.Roles.AnyAsync(r => r.Nombre == request.Nombre && r.Activo);
        if (duplicado)
        {
            return ServiceResult<RolResponse>.Invalid("Ya existe un rol activo con ese nombre.");
        }

        var rol = new Rol
        {
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion
        };

        _context.Roles.Add(rol);
        await _context.SaveChangesAsync();

        return ServiceResult<RolResponse>.Success(MapToResponse(rol));
    }

    public async Task<ServiceResult<RolResponse>> UpdateAsync(Guid id, UpdateRolRequest request)
    {
        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id && r.Activo);
        if (rol is null)
        {
            return ServiceResult<RolResponse>.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return ServiceResult<RolResponse>.Invalid("Nombre es obligatorio.");
        }

        var duplicado = await _context.Roles
            .AnyAsync(r => r.Nombre == request.Nombre && r.Activo && r.Id != id);
        if (duplicado)
        {
            return ServiceResult<RolResponse>.Invalid("Ya existe otro rol activo con ese nombre.");
        }

        rol.Nombre = request.Nombre.Trim();
        rol.Descripcion = request.Descripcion;
        rol.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<RolResponse>.Success(MapToResponse(rol));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
    {
        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id && r.Activo);
        if (rol is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        rol.Activo = false;
        rol.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<List<PermisoResponse>>> GetPermisosAsync(Guid rolId)
    {
        var rolExiste = await _context.Roles.AnyAsync(r => r.Id == rolId && r.Activo);
        if (!rolExiste)
        {
            return ServiceResult<List<PermisoResponse>>.NotFound();
        }

        var permisos = await _context.RolPermisos
            .Where(rp => rp.RolId == rolId)
            .Include(rp => rp.Permiso)
            .Select(rp => new PermisoResponse(
                rp.Permiso.Id,
                rp.Permiso.Codigo,
                rp.Permiso.Descripcion,
                rp.Permiso.Activo))
            .ToListAsync();

        return ServiceResult<List<PermisoResponse>>.Success(permisos);
    }

    public async Task<ServiceResult<bool>> AsignarPermisoAsync(Guid rolId, Guid permisoId)
    {
        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Id == rolId && r.Activo);
        if (rol is null)
        {
            return ServiceResult<bool>.Invalid("El rol indicado no existe o está inactivo.");
        }

        var permiso = await _context.Permisos.FirstOrDefaultAsync(p => p.Id == permisoId && p.Activo);
        if (permiso is null)
        {
            return ServiceResult<bool>.Invalid("El permiso indicado no existe o está inactivo.");
        }

        var yaAsignado = await _context.RolPermisos
            .AnyAsync(rp => rp.RolId == rolId && rp.PermisoId == permisoId);
        if (yaAsignado)
        {
            return ServiceResult<bool>.Invalid("El permiso ya está asignado a este rol.");
        }

        _context.RolPermisos.Add(new RolPermiso { RolId = rolId, PermisoId = permisoId });
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> QuitarPermisoAsync(Guid rolId, Guid permisoId)
    {
        var asignacion = await _context.RolPermisos
            .FirstOrDefaultAsync(rp => rp.RolId == rolId && rp.PermisoId == permisoId);

        if (asignacion is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        _context.RolPermisos.Remove(asignacion);
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private static RolResponse MapToResponse(Rol rol) => new(
        rol.Id,
        rol.Nombre,
        rol.Descripcion,
        rol.Activo);
}
