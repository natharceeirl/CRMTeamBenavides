using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace CRMTeamBenavides.Api.Configuration;

/// <summary>
/// Límites del inicio de sesión. Por cuenta: Identity la bloquea después de varias
/// contraseñas fallidas. Por IP: el limitador de ASP.NET corta a quien prueba muchas
/// cuentas distintas. Detrás de Caddy la IP es la del usuario solo con los encabezados
/// reenviados configurados (ver <see cref="ProxyInverso"/>).
/// </summary>
public static class SeguridadLogin
{
    public const int IntentosAntesDeBloquear = 5;
    public const int MinutosDeBloqueo = 15;
    public const int IntentosPorMinutoPorIp = 10;

    public const string PoliticaPorIp = "login-por-ip";

    // Un solo mensaje para todo fallo: decir «tu cuenta está bloqueada» revelaría
    // qué correos y DNI existen.
    public static readonly string MensajeCredencialesInvalidas =
        $"Correo o contraseña incorrectos. Después de {IntentosAntesDeBloquear} intentos fallidos la cuenta se bloquea {MinutosDeBloqueo} minutos.";

    public const string MensajeDemasiadosIntentos =
        "Demasiados intentos de inicio de sesión desde esta conexión. Espera un minuto y vuelve a intentar.";

    public static void ConfigurarBloqueo(LockoutOptions opciones)
    {
        opciones.AllowedForNewUsers = true;
        opciones.MaxFailedAccessAttempts = IntentosAntesDeBloquear;
        opciones.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(MinutosDeBloqueo);
    }

    /// <summary>
    /// Política <see cref="PoliticaPorIp"/>: como máximo <see cref="IntentosPorMinutoPorIp"/>
    /// inicios de sesión por minuto desde una misma IP, y 429 con el motivo después.
    /// Todo el taller sale a internet por la misma IP: si el límite se queda corto, se sube aquí.
    /// </summary>
    public static IServiceCollection AddLimiteLogin(this IServiceCollection services)
    {
        services.AddRateLimiter(opciones =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opciones.OnRejected = async (contexto, cancelacion) =>
            {
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                {
                    contexto.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(espera.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await contexto.HttpContext.Response.WriteAsJsonAsync(
                    new { error = MensajeDemasiadosIntentos }, cancelacion);
            };

            opciones.AddPolicy(PoliticaPorIp, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = IntentosPorMinutoPorIp,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }
}
