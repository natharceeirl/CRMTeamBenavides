namespace CRMTeamBenavides.Api.Services;

/// <summary>
/// La hora del taller (Arequipa). Perú está en UTC-5 todo el año, sin horario de
/// verano, así que basta un desfase fijo y no depende de la base de zonas horarias
/// del servidor. Se usa para textos que lee una persona y para filtros por día.
/// </summary>
public static class HoraPeru
{
    private static readonly TimeSpan Desfase = TimeSpan.FromHours(-5);

    /// <summary>Convierte una fecha guardada en UTC a la hora local de Perú.</summary>
    public static DateTime DesdeUtc(DateTime utc) =>
        DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Desfase), DateTimeKind.Unspecified);

    /// <summary>Las 00:00 en Perú del día indicado, expresadas en UTC.</summary>
    public static DateTime InicioDelDiaUtc(DateTime dia) =>
        DateTime.SpecifyKind(dia.Date.Subtract(Desfase), DateTimeKind.Utc);

    /// <summary>«03/10/2026 16:30», en hora de Perú.</summary>
    public static string Texto(DateTime utc) => DesdeUtc(utc).ToString("dd/MM/yyyy HH:mm");
}
