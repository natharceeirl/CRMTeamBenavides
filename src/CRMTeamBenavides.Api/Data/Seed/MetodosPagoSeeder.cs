using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Data.Seed;

public static class MetodosPagoSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        var metodosDefault = new (string Codigo, string Nombre)[]
        {
            ("EFECTIVO", "Efectivo"),
            ("TARJETA", "Tarjeta de Débito/Crédito"),
            ("TRANSFERENCIA", "Transferencia Bancaria"),
            ("YAPE_PLIN", "Yape / Plin")
        };

        var existentes = await context.MetodosPago.ToDictionaryAsync(m => m.Codigo);

        foreach (var (codigo, nombre) in metodosDefault)
        {
            if (!existentes.TryGetValue(codigo, out var metodo))
            {
                context.MetodosPago.Add(new MetodoPago
                {
                    Codigo = codigo,
                    Nombre = nombre,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                });
            }
            else if (!metodo.Activo)
            {
                metodo.Activo = true;
                metodo.FechaModificacion = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
    }
}
