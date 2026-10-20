using CRMTeamBenavides.Api.Configuration;
using CRMTeamBenavides.Api.Features.Auth;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Data.Seed;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Identity y el login sobre EF InMemory, con el mismo bloqueo que Program.cs. Cada
/// llamada usa su propio scope, como cada petición en la API.
/// </summary>
internal sealed class IdentityDePrueba : IDisposable
{
    /// <summary>Cumple las reglas por defecto de Identity, que son más estrictas que las de la API.</summary>
    public const string Clave = "Clave.Segura1";

    private readonly ServiceProvider _servicios;

    public IdentityDePrueba()
    {
        var servicios = new ServiceCollection();
        var nombreBase = Guid.NewGuid().ToString();

        servicios.AddLogging();
        servicios.AddDataProtection();
        servicios.AddDbContext<ApplicationDbContext>(opciones => opciones
            .UseInMemoryDatabase(nombreBase)
            .ConfigureWarnings(avisos => avisos.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        servicios
            .AddIdentityCore<Usuario>(opciones => SeguridadLogin.ConfigurarBloqueo(opciones.Lockout))
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        servicios.Configure<JwtSettings>(jwt =>
        {
            jwt.SecretKey = new string('k', 64);
            jwt.Issuer = "pruebas";
            jwt.Audience = "pruebas";
            jwt.AccessTokenExpirationMinutes = 30;
            jwt.RefreshTokenExpirationDays = 7;
        });
        servicios.AddScoped<ITokenService, TokenService>();
        servicios.AddScoped<IAuthService, AuthService>();
        servicios.AddScoped<GerenciaInicialService>();

        _servicios = servicios.BuildServiceProvider();
    }

    public async Task<T> ConAsync<T>(Func<IServiceProvider, Task<T>> accion)
    {
        using var alcance = _servicios.CreateScope();
        return await accion(alcance.ServiceProvider);
    }

    public Task SembrarRolesAsync() =>
        ConAsync(async sp =>
        {
            await RolSeeder.SeedAsync(sp.GetRequiredService<ApplicationDbContext>());
            return true;
        });

    public Task<Usuario> CrearUsuarioAsync(string email, string? rol = null) =>
        ConAsync(async sp =>
        {
            var usuario = new Usuario { UserName = email, Email = email, NombreCompleto = email, Activo = true };
            var resultado = await sp.GetRequiredService<UserManager<Usuario>>().CreateAsync(usuario, Clave);
            Assert.True(resultado.Succeeded, string.Join("; ", resultado.Errors.Select(e => e.Description)));

            if (rol is not null)
            {
                var contexto = sp.GetRequiredService<ApplicationDbContext>();
                var rolId = await contexto.Roles.Where(r => r.Nombre == rol).Select(r => r.Id).SingleAsync();
                contexto.UsuarioRoles.Add(new UsuarioRol { UsuarioId = usuario.Id, RolId = rolId });
                await contexto.SaveChangesAsync();
            }

            return usuario;
        });

    public Task<AuthResult<LoginResponse>> EntrarAsync(string email, string password) =>
        ConAsync(sp => sp.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest(email, password), "127.0.0.1"));

    public Task<Usuario> LeerUsuarioAsync(string email) =>
        ConAsync(async sp => (await sp.GetRequiredService<UserManager<Usuario>>().FindByEmailAsync(email))!);

    public void Dispose() => _servicios.Dispose();
}
