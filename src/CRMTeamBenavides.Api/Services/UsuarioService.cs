using CRMTeamBenavides.Api.Features.Roles;
using CRMTeamBenavides.Api.Features.Usuarios;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class UsuarioService : IUsuarioService
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ApplicationDbContext _context;

    public UsuarioService(UserManager<Usuario> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<List<UsuarioResponse>> GetAllAsync()
    {
        var usuarios = await _userManager.Users
            .Where(u => u.Activo)
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync();

        var responses = new List<UsuarioResponse>();
        foreach (var usuario in usuarios)
        {
            var roles = await GetRoleNamesAsync(usuario.Id);
            responses.Add(MapToResponse(usuario, roles));
        }

        return responses;
    }

    public async Task<ServiceResult<UsuarioResponse>> GetByIdAsync(Guid id)
    {
        var usuario = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.Activo);
        if (usuario is null)
        {
            return ServiceResult<UsuarioResponse>.NotFound();
        }

        var roles = await GetRoleNamesAsync(usuario.Id);
        return ServiceResult<UsuarioResponse>.Success(MapToResponse(usuario, roles));
    }

    public async Task<ServiceResult<UsuarioResponse>> CreateAsync(CreateUsuarioRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreCompleto))
        {
            return ServiceResult<UsuarioResponse>.Invalid("NombreCompleto es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ServiceResult<UsuarioResponse>.Invalid("Email es obligatorio.");
        }

        var existente = await _userManager.FindByEmailAsync(request.Email);
        if (existente is not null)
        {
            return ServiceResult<UsuarioResponse>.Invalid("Ya existe un usuario con ese email.");
        }

        var usuario = new Usuario
        {
            UserName = request.Email,
            Email = request.Email,
            NombreCompleto = request.NombreCompleto.Trim(),
            PhoneNumber = request.PhoneNumber,
            Activo = true,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(usuario, request.Password);
        if (!result.Succeeded)
        {
            var errores = string.Join("; ", result.Errors.Select(e => e.Description));
            return ServiceResult<UsuarioResponse>.Invalid(errores);
        }

        return ServiceResult<UsuarioResponse>.Success(MapToResponse(usuario, new List<string>()));
    }

    public async Task<ServiceResult<UsuarioResponse>> UpdateAsync(Guid id, UpdateUsuarioRequest request)
    {
        var usuario = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.Activo);
        if (usuario is null)
        {
            return ServiceResult<UsuarioResponse>.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.NombreCompleto))
        {
            return ServiceResult<UsuarioResponse>.Invalid("NombreCompleto es obligatorio.");
        }

        usuario.NombreCompleto = request.NombreCompleto.Trim();
        usuario.PhoneNumber = request.PhoneNumber;
        usuario.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var roles = await GetRoleNamesAsync(usuario.Id);
        return ServiceResult<UsuarioResponse>.Success(MapToResponse(usuario, roles));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
    {
        var usuario = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.Activo);
        if (usuario is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        usuario.Activo = false;
        usuario.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<List<RolResponse>>> GetRolesAsync(Guid usuarioId)
    {
        var usuarioExiste = await _userManager.Users.AnyAsync(u => u.Id == usuarioId && u.Activo);
        if (!usuarioExiste)
        {
            return ServiceResult<List<RolResponse>>.NotFound();
        }

        var roles = await _context.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId)
            .Include(ur => ur.Rol)
            .Select(ur => new RolResponse(
                ur.Rol.Id,
                ur.Rol.Nombre,
                ur.Rol.Descripcion,
                ur.Rol.Activo))
            .ToListAsync();

        return ServiceResult<List<RolResponse>>.Success(roles);
    }

    public async Task<ServiceResult<bool>> AsignarRolAsync(Guid usuarioId, Guid rolId)
    {
        var usuario = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo);
        if (usuario is null)
        {
            return ServiceResult<bool>.Invalid("El usuario indicado no existe o está inactivo.");
        }

        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Id == rolId && r.Activo);
        if (rol is null)
        {
            return ServiceResult<bool>.Invalid("El rol indicado no existe o está inactivo.");
        }

        var yaAsignado = await _context.UsuarioRoles
            .AnyAsync(ur => ur.UsuarioId == usuarioId && ur.RolId == rolId);
        if (yaAsignado)
        {
            return ServiceResult<bool>.Invalid("El rol ya está asignado a este usuario.");
        }

        _context.UsuarioRoles.Add(new UsuarioRol { UsuarioId = usuarioId, RolId = rolId });
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> QuitarRolAsync(Guid usuarioId, Guid rolId)
    {
        var asignacion = await _context.UsuarioRoles
            .FirstOrDefaultAsync(ur => ur.UsuarioId == usuarioId && ur.RolId == rolId);

        if (asignacion is null)
        {
            return ServiceResult<bool>.NotFound();
        }

        _context.UsuarioRoles.Remove(asignacion);
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private async Task<List<string>> GetRoleNamesAsync(Guid usuarioId)
    {
        return await _context.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId)
            .Include(ur => ur.Rol)
            .Select(ur => ur.Rol.Nombre)
            .ToListAsync();
    }

    private static UsuarioResponse MapToResponse(Usuario usuario, List<string> roles) => new(
        usuario.Id,
        usuario.Email ?? string.Empty,
        usuario.NombreCompleto,
        usuario.PhoneNumber,
        usuario.Activo,
        roles);
}
