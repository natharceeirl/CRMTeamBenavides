using CRMTeamBenavides.Api.Features.Permisos;
using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class PermisoService : IPermisoService
{
    private readonly ApplicationDbContext _context;

    public PermisoService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PermisoResponse>> GetAllAsync()
    {
        return await _context.Permisos
            .Where(p => p.Activo)
            .OrderBy(p => p.Codigo)
            .Select(p => new PermisoResponse(p.Id, p.Codigo, p.Descripcion, p.Activo))
            .ToListAsync();
    }

    public async Task<ServiceResult<PermisoResponse>> GetByIdAsync(Guid id)
    {
        var permiso = await _context.Permisos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);

        return permiso is null
            ? ServiceResult<PermisoResponse>.NotFound()
            : ServiceResult<PermisoResponse>.Success(
                new PermisoResponse(permiso.Id, permiso.Codigo, permiso.Descripcion, permiso.Activo));
    }
}
