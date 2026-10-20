using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace CRMTeamBenavides.Api.Configuration;

/// <summary>
/// En producción la API queda detrás de Caddy, que recibe el HTTPS y reenvía la
/// petición por HTTP. Con esto la API ve el esquema y la IP reales del usuario
/// (<c>X-Forwarded-Proto</c> y <c>X-Forwarded-For</c>).
/// </summary>
public static class ProxyInverso
{
    public const string SeccionRedesConfiables = "Proxy:RedesConfiables";

    /// <summary>
    /// Solo se aceptan los encabezados que llegan desde <c>Proxy:RedesConfiables</c>
    /// (notación CIDR, por ejemplo la red de Docker donde está Caddy). Sin esa lista
    /// se confía únicamente en la propia máquina, como en desarrollo. Si se aceptaran
    /// de cualquiera, bastaría inventarse una IP para saltarse el límite del login.
    /// </summary>
    public static IServiceCollection AddProxyInverso(this IServiceCollection services, IConfiguration configuration)
    {
        var redes = (configuration.GetSection(SeccionRedesConfiables).Get<string[]>() ?? [])
            .Where(red => !string.IsNullOrWhiteSpace(red))
            .Select(red => IPNetwork.TryParse(red.Trim(), out var ipRed)
                ? ipRed
                : throw new InvalidOperationException(
                    $"{SeccionRedesConfiables} tiene una red inválida: «{red}». Usar notación CIDR, por ejemplo 172.18.0.0/16."))
            .ToList();

        services.Configure<ForwardedHeadersOptions>(opciones =>
        {
            opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var red in redes)
            {
                opciones.KnownIPNetworks.Add(red);
            }
        });

        return services;
    }
}
