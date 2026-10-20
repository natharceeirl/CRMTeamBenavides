using CRMTeamBenavides.Api.Configuration;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Auth;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        UserManager<Usuario> userManager,
        ApplicationDbContext context,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _context = context;
        _tokenService = tokenService;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress)
    {
        var input = request.Email?.Trim() ?? string.Empty;
        var usuario = await _userManager.FindByEmailAsync(input);

        if (usuario is null)
        {
            usuario = await _userManager.FindByNameAsync(input);
        }

        if (usuario is null)
        {
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Activo && (c.NumeroDocumento == input || c.DocumentoIdentidad == input) && c.UsuarioId != null);
            if (cliente?.UsuarioId != null)
            {
                usuario = await _userManager.FindByIdAsync(cliente.UsuarioId.Value.ToString());
            }
        }

        // Mismo resultado si el email no existe, el usuario está inactivo o
        // la contraseña es incorrecta: no se debe poder distinguir el motivo.
        if (usuario is null || !usuario.Activo)
        {
            return AuthResult<LoginResponse>.Failure(AuthResultStatus.InvalidCredentials);
        }

        // Una cuenta bloqueada responde igual, aunque la contraseña sea la correcta.
        if (await _userManager.IsLockedOutAsync(usuario))
        {
            return AuthResult<LoginResponse>.Failure(AuthResultStatus.InvalidCredentials);
        }

        var passwordValid = await _userManager.CheckPasswordAsync(usuario, request.Password);
        if (!passwordValid)
        {
            // Identity cuenta el fallo y, al llegar al límite, bloquea la cuenta y pone el contador en cero.
            await _userManager.AccessFailedAsync(usuario);
            return AuthResult<LoginResponse>.Failure(AuthResultStatus.InvalidCredentials);
        }

        if (await _userManager.GetAccessFailedCountAsync(usuario) > 0)
        {
            await _userManager.ResetAccessFailedCountAsync(usuario);
        }

        var response = await IssueTokensAsync(usuario, ipAddress);
        return AuthResult<LoginResponse>.Success(response);
    }

    public async Task<AuthResult<LoginResponse>> RefreshAsync(RefreshRequest request, string? ipAddress)
    {
        var existingToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (existingToken is null)
        {
            return AuthResult<LoginResponse>.Failure(AuthResultStatus.InvalidRefreshToken);
        }

        if (existingToken.FechaRevocacion is not null)
        {
            return AuthResult<LoginResponse>.Failure(AuthResultStatus.RefreshTokenRevoked);
        }

        if (existingToken.FechaExpiracion <= DateTime.UtcNow)
        {
            return AuthResult<LoginResponse>.Failure(AuthResultStatus.RefreshTokenExpired);
        }

        var usuario = await _userManager.FindByIdAsync(existingToken.UsuarioId.ToString());
        if (usuario is null || !usuario.Activo)
        {
            return AuthResult<LoginResponse>.Failure(AuthResultStatus.InvalidRefreshToken);
        }

        // Rotación: el token usado queda revocado y enlazado al nuevo.
        var newRefreshTokenValue = _tokenService.GenerateRefreshToken();
        existingToken.FechaRevocacion = DateTime.UtcNow;
        existingToken.ReemplazadoPorToken = newRefreshTokenValue;

        var roles = await _context.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuario.Id && ur.Rol.Activo)
            .Select(ur => ur.Rol.Nombre)
            .ToListAsync();

        var (accessToken, accessTokenExpiration) = _tokenService.GenerateAccessToken(usuario, roles);

        var newRefreshToken = new RefreshToken
        {
            UsuarioId = usuario.Id,
            Token = newRefreshTokenValue,
            FechaExpiracion = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreadoPorIp = ipAddress
        };

        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync();

        var response = new LoginResponse(
            accessToken,
            accessTokenExpiration,
            newRefreshToken.Token,
            newRefreshToken.FechaExpiracion);

        return AuthResult<LoginResponse>.Success(response);
    }

    private async Task<LoginResponse> IssueTokensAsync(Usuario usuario, string? ipAddress)
    {
        var roles = await _context.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuario.Id && ur.Rol.Activo)
            .Select(ur => ur.Rol.Nombre)
            .ToListAsync();

        var (accessToken, accessTokenExpiration) = _tokenService.GenerateAccessToken(usuario, roles);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UsuarioId = usuario.Id,
            Token = refreshTokenValue,
            FechaExpiracion = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreadoPorIp = ipAddress
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return new LoginResponse(
            accessToken,
            accessTokenExpiration,
            refreshToken.Token,
            refreshToken.FechaExpiracion);
    }

    public async Task<ServiceResult<MeResponse>> GetCurrentUserAsync(Guid usuarioId)
    {
        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null || !usuario.Activo)
        {
            return ServiceResult<MeResponse>.NotFound();
        }

        var roles = await _context.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId && ur.Rol.Activo)
            .Select(ur => ur.Rol.Nombre)
            .ToListAsync();

        List<string> permisos;
        if (roles.Contains(RolesDefinidos.GerenciaAdmin))
        {
            permisos = PermisosDefinidos.Todos.ToList();
        }
        else
        {
            permisos = await _context.UsuarioRoles
                .Where(ur => ur.UsuarioId == usuarioId && ur.Rol.Activo)
                .SelectMany(ur => ur.Rol.RolPermisos)
                .Where(rp => rp.Permiso.Activo)
                .Select(rp => rp.Permiso.Codigo)
                .Distinct()
                .ToListAsync();
        }

        var clienteId = await _context.Clientes
            .Where(c => c.UsuarioId == usuarioId && c.Activo)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        var response = new MeResponse(
            usuario.Id,
            usuario.Email ?? string.Empty,
            usuario.NombreCompleto,
            usuario.Activo,
            roles,
            permisos,
            clienteId);

        return ServiceResult<MeResponse>.Success(response);
    }

    public async Task<ServiceResult<bool>> LogoutAsync(Guid usuarioId)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UsuarioId == usuarioId && rt.FechaRevocacion == null)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.FechaRevocacion = now;
            token.FechaExpiracion = now;
        }

        if (activeTokens.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> CambiarPasswordAsync(Guid usuarioId, CambiarPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PasswordActual) || string.IsNullOrWhiteSpace(request.PasswordNueva))
        {
            return ServiceResult<bool>.Invalid("La contraseña actual y la nueva son obligatorias.");
        }

        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null || !usuario.Activo)
        {
            return ServiceResult<bool>.NotFound();
        }

        var result = await _userManager.ChangePasswordAsync(usuario, request.PasswordActual, request.PasswordNueva);
        if (!result.Succeeded)
        {
            var errores = string.Join("; ", result.Errors.Select(e => e.Description));
            return ServiceResult<bool>.Invalid(errores);
        }

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ResetPasswordAsync(Guid usuarioId, ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NuevaPassword))
        {
            return ServiceResult<bool>.Invalid("La nueva contraseña es obligatoria.");
        }

        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null || !usuario.Activo)
        {
            return ServiceResult<bool>.NotFound();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var result = await _userManager.ResetPasswordAsync(usuario, token, request.NuevaPassword);
        if (!result.Succeeded)
        {
            var errores = string.Join("; ", result.Errors.Select(e => e.Description));
            return ServiceResult<bool>.Invalid(errores);
        }

        // Quien restablece la contraseña también desbloquea la cuenta: no hay que esperar a que venza.
        await _userManager.SetLockoutEndDateAsync(usuario, null);
        await _userManager.ResetAccessFailedCountAsync(usuario);

        return ServiceResult<bool>.Success(true);
    }

    /// <summary>
    /// El cliente elimina su cuenta de la app y del portal (Google Play y Apple lo exigen).
    /// Se borran los datos personales de la cuenta y se cierra el acceso; la ficha del
    /// cliente, sus unidades, órdenes y comprobantes siguen siendo registros del taller.
    /// La fila del usuario queda, anónima y desactivada, porque la auditoría y el
    /// historial apuntan a ella. Más adelante el cliente puede activar otra cuenta.
    /// </summary>
    public async Task<ServiceResult<bool>> EliminarCuentaAsync(Guid usuarioId, EliminarCuentaRequest request)
    {
        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null || !usuario.Activo)
        {
            return ServiceResult<bool>.NotFound();
        }

        // Las cuentas del personal las crea y las da de baja Gerencia, desde Usuarios.
        var roles = await _context.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId && ur.Rol.Activo)
            .Select(ur => ur.Rol.Nombre)
            .ToListAsync();
        if (!roles.Contains(RolesDefinidos.Cliente) || roles.Any(rol => rol != RolesDefinidos.Cliente))
        {
            return ServiceResult<bool>.Forbidden(
                "Solo un cliente puede eliminar su cuenta desde aquí. Las cuentas del personal las da de baja Gerencia.");
        }

        // La contraseña cuenta para el bloqueo igual que en el login: un token robado no sirve para adivinarla.
        if (await _userManager.IsLockedOutAsync(usuario))
        {
            return ServiceResult<bool>.Invalid(SeguridadLogin.MensajeCredencialesInvalidas);
        }

        if (string.IsNullOrEmpty(request.Password) || !await _userManager.CheckPasswordAsync(usuario, request.Password))
        {
            await _userManager.AccessFailedAsync(usuario);
            return ServiceResult<bool>.Invalid("La contraseña no es correcta.");
        }

        await using var transaccion = await _context.Database.BeginTransactionAsync();
        var ahora = DateTime.UtcNow;

        var clientes = await _context.Clientes.Where(c => c.UsuarioId == usuarioId).ToListAsync();
        foreach (var cliente in clientes)
        {
            cliente.UsuarioId = null;
        }

        var sesiones = await _context.RefreshTokens
            .Where(rt => rt.UsuarioId == usuarioId && rt.FechaRevocacion == null)
            .ToListAsync();
        foreach (var sesion in sesiones)
        {
            sesion.FechaRevocacion = ahora;
            sesion.FechaExpiracion = ahora;
        }

        // Sin teléfonos para avisos ni bandeja: son solo de esta cuenta.
        _context.DispositivosNotificacion.RemoveRange(
            await _context.DispositivosNotificacion.Where(d => d.UsuarioId == usuarioId).ToListAsync());
        _context.Notificaciones.RemoveRange(
            await _context.Notificaciones.Where(n => n.UsuarioId == usuarioId).ToListAsync());

        _context.EventosAuditoria.Add(new EventoAuditoria
        {
            UsuarioId = usuarioId,
            Fecha = ahora,
            Accion = "Eliminar",
            Entidad = "Usuario",
            EntidadId = usuarioId.ToString(),
            Detalle = "La cuenta la eliminó su titular. Se borraron sus datos personales; la ficha del cliente se conserva."
        });
        await _context.SaveChangesAsync();

        var anonimo = $"cuenta-eliminada-{usuarioId:N}@cuentas.invalid";
        usuario.UserName = anonimo;
        usuario.Email = anonimo;
        usuario.EmailConfirmed = false;
        usuario.NombreCompleto = "Cuenta eliminada";
        usuario.PhoneNumber = null;
        usuario.PhoneNumberConfirmed = false;
        usuario.PasswordHash = null;
        usuario.Activo = false;
        usuario.FechaModificacion = ahora;
        usuario.ModificadoPorId = usuarioId;
        var resultado = await _userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            return ServiceResult<bool>.Invalid(string.Join("; ", resultado.Errors.Select(e => e.Description)));
        }
        // Invalida cualquier token de Identity ya emitido, como el de restablecer la contraseña.
        await _userManager.UpdateSecurityStampAsync(usuario);

        await transaccion.CommitAsync();
        return ServiceResult<bool>.Success(true);
    }
}
