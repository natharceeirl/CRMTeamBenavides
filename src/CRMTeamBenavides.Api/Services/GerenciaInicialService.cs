using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

/// <summary>
/// El primer usuario de Gerencia de una base nueva, y la recuperación de su acceso si
/// olvida la contraseña: no hay recuperación por correo y solo Gerencia restablece
/// contraseñas. Lo usa el comando <c>crear-gerencia</c> desde el servidor; ningún
/// endpoint lo expone.
/// </summary>
public class GerenciaInicialService
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ApplicationDbContext _context;

    public GerenciaInicialService(UserManager<Usuario> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    /// <summary>
    /// Crea el usuario con el rol Gerencia/Admin. Si ya hay un usuario de Gerencia activo
    /// no hace nada, para que nadie con acceso al servidor sume otro por esta vía.
    /// </summary>
    public async Task<ServiceResult<Usuario>> CrearAsync(string email, string nombreCompleto, string password)
    {
        email = email.Trim();
        nombreCompleto = nombreCompleto.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            return ServiceResult<Usuario>.Invalid("El correo es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            return ServiceResult<Usuario>.Invalid("El nombre completo es obligatorio.");
        }

        if (await HayGerenciaActivaAsync())
        {
            return ServiceResult<Usuario>.Invalid(
                "Ya hay un usuario de Gerencia activo; no se creó otro. Para recuperar su acceso: crear-gerencia --restablecer.");
        }

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return ServiceResult<Usuario>.Invalid("Ya existe un usuario con ese correo.");
        }

        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == RolesDefinidos.GerenciaAdmin && r.Activo);
        if (rol is null)
        {
            return ServiceResult<Usuario>.Invalid(
                $"No existe el rol {RolesDefinidos.GerenciaAdmin}. Revisar que la base tenga las migraciones aplicadas.");
        }

        // El usuario y su rol van juntos: un usuario sin rol bloquearía el siguiente intento con «ya existe».
        await using var transaccion = await _context.Database.BeginTransactionAsync();

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            NombreCompleto = nombreCompleto,
            Activo = true,
            EmailConfirmed = true
        };

        var resultado = await _userManager.CreateAsync(usuario, password);
        if (!resultado.Succeeded)
        {
            return ServiceResult<Usuario>.Invalid(string.Join("; ", resultado.Errors.Select(e => e.Description)));
        }

        _context.UsuarioRoles.Add(new UsuarioRol { UsuarioId = usuario.Id, RolId = rol.Id });
        await _context.SaveChangesAsync();
        await transaccion.CommitAsync();

        return ServiceResult<Usuario>.Success(usuario);
    }

    /// <summary>
    /// Cambia la contraseña de un usuario de Gerencia activo, le quita el bloqueo por
    /// intentos fallidos y cierra sus sesiones abiertas, por si alguien más la conocía.
    /// </summary>
    public async Task<ServiceResult<Usuario>> RestablecerAsync(string email, string password)
    {
        var usuario = await _userManager.FindByEmailAsync(email.Trim());
        if (usuario is null || !usuario.Activo || !await EsGerenciaAsync(usuario.Id))
        {
            return ServiceResult<Usuario>.Invalid("No hay un usuario de Gerencia activo con ese correo.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await _userManager.ResetPasswordAsync(usuario, token, password);
        if (!resultado.Succeeded)
        {
            return ServiceResult<Usuario>.Invalid(string.Join("; ", resultado.Errors.Select(e => e.Description)));
        }

        await _userManager.SetLockoutEndDateAsync(usuario, null);
        await _userManager.ResetAccessFailedCountAsync(usuario);

        var ahora = DateTime.UtcNow;
        var sesiones = await _context.RefreshTokens
            .Where(rt => rt.UsuarioId == usuario.Id && rt.FechaRevocacion == null)
            .ToListAsync();
        foreach (var sesion in sesiones)
        {
            sesion.FechaRevocacion = ahora;
            sesion.FechaExpiracion = ahora;
        }
        await _context.SaveChangesAsync();

        return ServiceResult<Usuario>.Success(usuario);
    }

    private Task<bool> HayGerenciaActivaAsync() =>
        _context.UsuarioRoles.AnyAsync(ur =>
            ur.Rol.Nombre == RolesDefinidos.GerenciaAdmin && ur.Rol.Activo && ur.Usuario.Activo);

    private Task<bool> EsGerenciaAsync(Guid usuarioId) =>
        _context.UsuarioRoles.AnyAsync(ur =>
            ur.UsuarioId == usuarioId && ur.Rol.Nombre == RolesDefinidos.GerenciaAdmin && ur.Rol.Activo);
}
