using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;

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

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return;
        }

        var usuario = new Usuario
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
}
