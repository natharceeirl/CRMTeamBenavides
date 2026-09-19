using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Data.Seed;

public static class RolSeeder
{
    public const string RolAdmin = "Admin";
    public const string RolRecepcion = "Recepcion";

    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // 1. Rol Admin
        var existeAdmin = await context.Roles
            .AnyAsync(r => r.Nombre == RolAdmin);

        if (!existeAdmin)
        {
            context.Roles.Add(new Rol
            {
                Nombre = RolAdmin,
                Descripcion = "Administrador del sistema con acceso total",
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            });
        }

        // 2. Rol Recepcion
        var existeRecepcion = await context.Roles
            .AnyAsync(r => r.Nombre == RolRecepcion);

        if (!existeRecepcion)
        {
            context.Roles.Add(new Rol
            {
                Nombre = RolRecepcion,
                Descripcion = "Personal de recepción y atención al cliente",
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }
}
