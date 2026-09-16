using CRMTeamBenavides.Api.Configuration;
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
        var usuario = await _userManager.FindByEmailAsync(request.Email);

        // Mismo resultado si el email no existe, el usuario está inactivo o
        // la contraseña es incorrecta: no se debe poder distinguir el motivo.
        if (usuario is null || !usuario.Activo)
        {
            return AuthResult<LoginResponse>.Failure(AuthResultStatus.InvalidCredentials);
        }

        var passwordValid = await _userManager.CheckPasswordAsync(usuario, request.Password);
        if (!passwordValid)
        {
            return AuthResult<LoginResponse>.Failure(AuthResultStatus.InvalidCredentials);
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

        var (accessToken, accessTokenExpiration) = _tokenService.GenerateAccessToken(usuario);

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
        var (accessToken, accessTokenExpiration) = _tokenService.GenerateAccessToken(usuario);
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

        var response = new MeResponse(
            usuario.Id,
            usuario.Email ?? string.Empty,
            usuario.NombreCompleto,
            usuario.Activo,
            roles);

        return ServiceResult<MeResponse>.Success(response);
    }
}
