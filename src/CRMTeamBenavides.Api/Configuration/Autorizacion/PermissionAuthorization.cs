using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using CRMTeamBenavides.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Configuration.Autorizacion;

public class PermissionRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; }

    public PermissionRequirement(params string[] permissions)
    {
        Permissions = permissions.ToList();
    }
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PermissionAuthorizationHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Si el usuario ya tiene el claim de rol Gerencia/Admin en el token o contexto
        if (context.User.IsInRole(RolesDefinidos.GerenciaAdmin))
        {
            context.Succeed(requirement);
            return;
        }

        var subClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(subClaim, out var usuarioId))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Verificar si el usuario tiene el rol Gerencia/Admin en BD
        var esGerencia = await dbContext.UsuarioRoles
            .AnyAsync(ur => ur.UsuarioId == usuarioId
                         && ur.Rol.Activo
                         && ur.Rol.Nombre == RolesDefinidos.GerenciaAdmin);

        if (esGerencia)
        {
            context.Succeed(requirement);
            return;
        }

        // Verificar si alguno de los roles activos del usuario tiene alguno de los permisos requeridos
        var tienePermiso = await dbContext.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId && ur.Rol.Activo)
            .SelectMany(ur => ur.Rol.RolPermisos)
            .AnyAsync(rp => rp.Permiso.Activo && requirement.Permissions.Contains(rp.Permiso.Codigo));

        if (tienePermiso)
        {
            context.Succeed(requirement);
        }
    }
}
