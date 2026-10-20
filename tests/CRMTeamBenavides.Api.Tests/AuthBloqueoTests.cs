using CRMTeamBenavides.Api.Configuration;
using CRMTeamBenavides.Api.Features.Auth;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

public class AuthBloqueoTests : IDisposable
{
    private const string Email = "tecnico@prueba.pe";
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    private async Task FallarAsync(int veces)
    {
        for (var i = 0; i < veces; i++)
        {
            var intento = await _identity.EntrarAsync(Email, "no-es-la-clave");
            Assert.Equal(AuthResultStatus.InvalidCredentials, intento.Status);
        }
    }

    [Fact]
    public async Task Cinco_contrasenas_fallidas_bloquean_la_cuenta_15_minutos_aunque_despues_llegue_la_correcta()
    {
        await _identity.CrearUsuarioAsync(Email);

        await FallarAsync(SeguridadLogin.IntentosAntesDeBloquear);
        var conLaCorrecta = await _identity.EntrarAsync(Email, IdentityDePrueba.Clave);

        Assert.Equal(AuthResultStatus.InvalidCredentials, conLaCorrecta.Status);
        var usuario = await _identity.LeerUsuarioAsync(Email);
        Assert.NotNull(usuario.LockoutEnd);
        var restante = usuario.LockoutEnd!.Value - DateTimeOffset.UtcNow;
        Assert.InRange(restante, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(SeguridadLogin.MinutosDeBloqueo));
    }

    [Fact]
    public async Task Un_acierto_antes_del_limite_reinicia_el_contador()
    {
        await _identity.CrearUsuarioAsync(Email);

        await FallarAsync(SeguridadLogin.IntentosAntesDeBloquear - 1);
        Assert.True((await _identity.EntrarAsync(Email, IdentityDePrueba.Clave)).IsSuccess);
        Assert.Equal(0, (await _identity.LeerUsuarioAsync(Email)).AccessFailedCount);

        await FallarAsync(SeguridadLogin.IntentosAntesDeBloquear - 1);
        Assert.True((await _identity.EntrarAsync(Email, IdentityDePrueba.Clave)).IsSuccess);
    }

    [Fact]
    public async Task Vencido_el_bloqueo_entra_con_la_contrasena_correcta()
    {
        await _identity.CrearUsuarioAsync(Email);
        await FallarAsync(SeguridadLogin.IntentosAntesDeBloquear);

        await _identity.ConAsync(async sp =>
        {
            var usuarios = sp.GetRequiredService<UserManager<Usuario>>();
            var usuario = await usuarios.FindByEmailAsync(Email);
            return await usuarios.SetLockoutEndDateAsync(usuario!, DateTimeOffset.UtcNow.AddSeconds(-1));
        });

        Assert.True((await _identity.EntrarAsync(Email, IdentityDePrueba.Clave)).IsSuccess);
    }

    [Fact]
    public async Task Restablecer_la_contrasena_desde_Usuarios_tambien_quita_el_bloqueo()
    {
        var usuario = await _identity.CrearUsuarioAsync(Email);
        await FallarAsync(SeguridadLogin.IntentosAntesDeBloquear);

        var restablecida = await _identity.ConAsync(sp =>
            sp.GetRequiredService<IAuthService>().ResetPasswordAsync(usuario.Id, new ResetPasswordRequest("Otra.Clave2")));

        Assert.True(restablecida.IsSuccess);
        Assert.True((await _identity.EntrarAsync(Email, "Otra.Clave2")).IsSuccess);
    }

    [Fact]
    public async Task Un_correo_que_no_existe_responde_igual_que_una_contrasena_incorrecta()
    {
        var intento = await _identity.EntrarAsync("nadie@prueba.pe", IdentityDePrueba.Clave);

        Assert.Equal(AuthResultStatus.InvalidCredentials, intento.Status);
    }
}
