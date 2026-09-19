using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CRMTeamBenavides.Data.Seed;

public static class DevelopmentUserSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var email = configuration["DevelopmentUser:Email"];
        var password = configuration["DevelopmentUser:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            // Sin credenciales en User Secrets, no se crea nada. No es un error:
            // permite arrancar el proyecto sin haber configurado el seeder todavía.
            return;
        }

        var userManager = serviceProvider.GetRequiredService<UserManager<Usuario>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        var usuario = await userManager.FindByEmailAsync(email);
        if (usuario is null)
        {
            usuario = new Usuario
            {
                UserName = email,
                Email = email,
                NombreCompleto = "Usuario de Desarrollo",
                Activo = true,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(usuario, password);

            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"No se pudo crear el usuario de desarrollo: {errors}");
            }
        }

        // Asignar rol administrativo de forma idempotente
        var adminRol = await context.Roles
            .FirstOrDefaultAsync(r => r.Nombre == RolSeeder.RolAdmin && r.Activo)
            ?? await context.Roles.FirstOrDefaultAsync(r => r.Nombre == "Administrador" && r.Activo);

        if (adminRol is not null)
        {
            var tieneRol = await context.UsuarioRoles
                .AnyAsync(ur => ur.UsuarioId == usuario.Id && ur.RolId == adminRol.Id);

            if (!tieneRol)
            {
                context.UsuarioRoles.Add(new UsuarioRol
                {
                    UsuarioId = usuario.Id,
                    RolId = adminRol.Id
                });
                await context.SaveChangesAsync();
            }
        }
    }
}
