using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Configuration.Autorizacion;

public record UserIsolationContext(
    Guid? UsuarioId,
    bool EsStaff,
    bool EsTecnico,
    bool EsCliente,
    Guid? ClienteId,
    bool EsClienteHuerfano)
{
    /// <summary>
    /// Devuelve el ClienteId efectivo para aislamiento de consultas.
    /// Si es un usuario cliente legítimo, devuelve su ClienteId.
    /// Si es un usuario cliente huérfano (sin registro Cliente activo asociado),
    /// devuelve Guid.Empty para garantizar que las consultas filtren a 0 resultados
    /// y NUNCA se interpreten como null (acceso irrestricto de staff).
    /// Si es personal autorizado (staff), devuelve null (sin restricción de cliente).
    /// </summary>
    public Guid? SoloClienteId =>
        EsCliente ? (ClienteId ?? Guid.Empty) : null;

    /// <summary>
    /// Si el usuario cliente no tiene ningún registro Cliente activo vinculado,
    /// se debe denegar el acceso inmediatamente (fail-safe por defecto).
    /// </summary>
    public bool DebeDenegarAcceso => EsClienteHuerfano;
}

public static class UserIsolationHelper
{
    public static Guid? ObtenerUsuarioId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue("uid");

        return Guid.TryParse(idStr, out var id) ? id : null;
    }

    public static async Task<UserIsolationContext> ResolverContextoAsync(
        ClaimsPrincipal user,
        ApplicationDbContext dbContext,
        CancellationToken ct = default)
    {
        var usuarioId = ObtenerUsuarioId(user);
        if (!usuarioId.HasValue)
        {
            return new UserIsolationContext(
                UsuarioId: null,
                EsStaff: false,
                EsTecnico: false,
                EsCliente: false,
                ClienteId: null,
                EsClienteHuerfano: false);
        }

        var esGerencia = user.IsInRole(RolesDefinidos.GerenciaAdmin);
        var esRecepcion = user.IsInRole(RolesDefinidos.Recepcion);
        var esVendedor = user.IsInRole(RolesDefinidos.Vendedor);
        var esTecnico = user.IsInRole(RolesDefinidos.Tecnico) && !esGerencia && !esRecepcion;

        if (esGerencia || esRecepcion || esVendedor)
        {
            return new UserIsolationContext(
                UsuarioId: usuarioId,
                EsStaff: true,
                EsTecnico: esTecnico,
                EsCliente: false,
                ClienteId: null,
                EsClienteHuerfano: false);
        }

        var esRolCliente = user.IsInRole(RolesDefinidos.Cliente);
        var tienePermisoPortal = user.HasClaim("permission", PermisosDefinidos.PortalAcceso)
            || user.HasClaim(ClaimTypes.Role, RolesDefinidos.Cliente);

        if (!esRolCliente && !tienePermisoPortal)
        {
            var rolesUsuario = await dbContext.UsuarioRoles
                .Where(ur => ur.UsuarioId == usuarioId.Value && ur.Rol.Activo)
                .Select(ur => ur.Rol.Nombre)
                .ToListAsync(ct);

            esRolCliente = rolesUsuario.Contains(RolesDefinidos.Cliente);
        }

        if (esRolCliente || (!esTecnico && !esGerencia && !esRecepcion && !esVendedor))
        {
            var cliente = await dbContext.Clientes
                .AsNoTracking()
                .Where(c => c.UsuarioId == usuarioId.Value && c.Activo)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync(ct);

            var clienteId = cliente?.Id;
            var esHuerfano = !clienteId.HasValue;

            return new UserIsolationContext(
                UsuarioId: usuarioId,
                EsStaff: false,
                EsTecnico: esTecnico,
                EsCliente: true,
                ClienteId: clienteId,
                EsClienteHuerfano: esHuerfano);
        }

        return new UserIsolationContext(
            UsuarioId: usuarioId,
            EsStaff: false,
            EsTecnico: esTecnico,
            EsCliente: false,
            ClienteId: null,
            EsClienteHuerfano: false);
    }
}
