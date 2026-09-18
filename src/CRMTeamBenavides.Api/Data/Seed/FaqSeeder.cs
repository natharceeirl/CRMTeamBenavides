using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Data.Seed;

public static class FaqSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (await context.FaqItems.AnyAsync()) return;

        var faqs = new List<FaqItem>
        {
            new()
            {
                Categoria       = "Horarios",
                Pregunta        = "¿Cuál es el horario de atención del taller?",
                Respuesta       = "Nuestro horario de atención es de lunes a viernes de 8:00 a 18:00 y sábados de 8:00 a 13:00.",
                PalabrasClave   = "horario, atencion, hora, apertura, cierre, abierto, sabados",
                Orden           = 1,
                VecesConsultada = 0,
                Activo          = true
            },
            new()
            {
                Categoria       = "Servicios",
                Pregunta        = "¿Qué servicios mecánicos realizan?",
                Respuesta       = "Realizamos mantenimiento preventivo, afinamiento, cambio de aceite, sistema de frenos, suspensión, dirección y diagnóstico computarizado.",
                PalabrasClave   = "servicios, mantenimiento, afinamiento, aceite, frenos, suspension, diagnostico, mecanica",
                Orden           = 2,
                VecesConsultada = 0,
                Activo          = true
            },
            new()
            {
                Categoria       = "Cotizaciones",
                Pregunta        = "¿Cómo puedo solicitar una cotización?",
                Respuesta       = "Puedes solicitar una cotización directamente con nuestros asesores a través de este chat o visitando nuestro taller con los datos de tu vehículo.",
                PalabrasClave   = "cotizacion, presupuesto, precio, repuesto, repuestos, costo, valor",
                Orden           = 3,
                VecesConsultada = 0,
                Activo          = true
            }
        };

        context.FaqItems.AddRange(faqs);
        await context.SaveChangesAsync();
    }
}
