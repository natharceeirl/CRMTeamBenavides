using System.Net;
using CRMTeamBenavides.Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// El límite por IP y los encabezados reenviados, con la misma cadena que Program.cs:
/// UseForwardedHeaders primero y la política del login en el endpoint.
/// </summary>
public class LimiteLoginTests
{
    private static async Task<WebApplication> LevantarAsync(params string[] redesConfiables)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(redesConfiables.Select((red, i) =>
            KeyValuePair.Create($"{ProxyInverso.SeccionRedesConfiables}:{i}", (string?)red)));
        builder.Services.AddProxyInverso(builder.Configuration);
        builder.Services.AddLimiteLogin();

        var app = builder.Build();
        app.UseForwardedHeaders();
        app.UseRateLimiter();
        // Responde lo que la API ve de la petición: esquema e IP.
        app.MapPost("/api/auth/login", (HttpContext contexto) =>
                Results.Text($"{contexto.Request.Scheme} {contexto.Connection.RemoteIpAddress}"))
            .RequireRateLimiting(SeguridadLogin.PoliticaPorIp);

        await app.StartAsync();
        return app;
    }

    private static Task<HttpContext> EntrarDesdeAsync(
        WebApplication app, string ip, string? reenviadoPara = null, string? esquemaReenviado = null) =>
        app.GetTestServer().SendAsync(contexto =>
        {
            contexto.Request.Method = HttpMethods.Post;
            contexto.Request.Path = "/api/auth/login";
            contexto.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            if (reenviadoPara is not null) contexto.Request.Headers["X-Forwarded-For"] = reenviadoPara;
            if (esquemaReenviado is not null) contexto.Request.Headers["X-Forwarded-Proto"] = esquemaReenviado;
        });

    private static async Task<string> LeerAsync(HttpContext contexto)
    {
        using var lector = new StreamReader(contexto.Response.Body);
        return await lector.ReadToEndAsync();
    }

    [Fact]
    public async Task Desde_una_misma_IP_corta_el_intento_once_del_minuto_y_dice_por_que()
    {
        await using var app = await LevantarAsync();

        for (var i = 0; i < SeguridadLogin.IntentosPorMinutoPorIp; i++)
        {
            Assert.Equal(StatusCodes.Status200OK, (await EntrarDesdeAsync(app, "198.51.100.1")).Response.StatusCode);
        }
        var cortado = await EntrarDesdeAsync(app, "198.51.100.1");

        Assert.Equal(StatusCodes.Status429TooManyRequests, cortado.Response.StatusCode);
        Assert.Contains("Demasiados intentos", await LeerAsync(cortado));
        Assert.False(string.IsNullOrEmpty(cortado.Response.Headers.RetryAfter));
    }

    [Fact]
    public async Task Cada_IP_tiene_su_propio_limite()
    {
        await using var app = await LevantarAsync();

        for (var i = 0; i < SeguridadLogin.IntentosPorMinutoPorIp; i++)
        {
            await EntrarDesdeAsync(app, "198.51.100.1");
        }

        Assert.Equal(StatusCodes.Status200OK, (await EntrarDesdeAsync(app, "198.51.100.2")).Response.StatusCode);
    }

    [Fact]
    public async Task Detras_de_Caddy_cuenta_la_IP_del_usuario_y_ve_el_HTTPS()
    {
        await using var app = await LevantarAsync("10.0.0.0/8");

        for (var i = 0; i < SeguridadLogin.IntentosPorMinutoPorIp; i++)
        {
            await EntrarDesdeAsync(app, "10.0.0.2", reenviadoPara: "203.0.113.1", esquemaReenviado: "https");
        }
        var otroUsuario = await EntrarDesdeAsync(app, "10.0.0.2", reenviadoPara: "203.0.113.2", esquemaReenviado: "https");

        Assert.Equal(StatusCodes.Status200OK, otroUsuario.Response.StatusCode);
        Assert.Equal("https 203.0.113.2", await LeerAsync(otroUsuario));
    }

    [Fact]
    public async Task No_cree_en_los_encabezados_que_llegan_de_una_red_no_configurada()
    {
        await using var app = await LevantarAsync();

        HttpContext ultimo = null!;
        for (var i = 0; i <= SeguridadLogin.IntentosPorMinutoPorIp; i++)
        {
            // Cada intento inventa otra IP: si se le creyera, nunca llegaría al límite.
            ultimo = await EntrarDesdeAsync(app, "198.51.100.7", reenviadoPara: $"203.0.113.{i + 1}", esquemaReenviado: "https");
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests, ultimo.Response.StatusCode);
    }

    [Fact]
    public void Una_red_mal_escrita_falla_al_arrancar_con_un_mensaje_claro()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection([KeyValuePair.Create($"{ProxyInverso.SeccionRedesConfiables}:0", (string?)"10.0.0/8x")])
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddProxyInverso(configuracion));
        Assert.Contains("10.0.0/8x", error.Message);
    }
}
