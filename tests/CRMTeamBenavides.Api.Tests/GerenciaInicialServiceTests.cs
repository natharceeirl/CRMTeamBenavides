using CRMTeamBenavides.Api.Configuration;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Auth;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

public class GerenciaInicialServiceTests : IDisposable
{
    private const string Email = "gerencia@prueba.pe";
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    private Task<ServiceResult<Usuario>> CrearAsync(
        string email, string password = IdentityDePrueba.Clave) =>
        _identity.ConAsync(sp => sp.GetRequiredService<GerenciaInicialService>().CrearAsync(email, "Gerencia de prueba", password));

    private Task<ServiceResult<Usuario>> RestablecerAsync(
        string email, string password) =>
        _identity.ConAsync(sp => sp.GetRequiredService<GerenciaInicialService>().RestablecerAsync(email, password));

    private Task<int> ContarUsuariosAsync() =>
        _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().Usuarios.CountAsync());

    [Fact]
    public async Task En_una_base_nueva_crea_el_usuario_de_Gerencia_y_puede_entrar()
    {
        await _identity.SembrarRolesAsync();

        var creado = await CrearAsync(Email);
        var entrada = await _identity.EntrarAsync(Email, IdentityDePrueba.Clave);

        Assert.True(creado.IsSuccess, creado.Error);
        Assert.True(entrada.IsSuccess);
        var yo = await _identity.ConAsync(sp => sp.GetRequiredService<IAuthService>().GetCurrentUserAsync(creado.Data!.Id));
        Assert.Contains(RolesDefinidos.GerenciaAdmin, yo.Data!.Roles);
    }

    [Fact]
    public async Task Con_un_usuario_de_Gerencia_activo_no_crea_otro()
    {
        await _identity.SembrarRolesAsync();
        await CrearAsync(Email);

        var segundo = await CrearAsync("otra.gerencia@prueba.pe");

        Assert.False(segundo.IsSuccess);
        Assert.Contains("Ya hay un usuario de Gerencia activo", segundo.Error);
        Assert.Equal(1, await ContarUsuariosAsync());
    }

    [Fact]
    public async Task Si_la_unica_Gerencia_esta_desactivada_deja_crear_otra()
    {
        await _identity.SembrarRolesAsync();
        await CrearAsync(Email);
        await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var anterior = await contexto.Usuarios.SingleAsync();
            anterior.Activo = false;
            return await contexto.SaveChangesAsync();
        });

        var nueva = await CrearAsync("nueva.gerencia@prueba.pe");

        Assert.True(nueva.IsSuccess, nueva.Error);
    }

    [Fact]
    public async Task No_reutiliza_el_correo_de_otro_usuario()
    {
        await _identity.SembrarRolesAsync();
        await _identity.CrearUsuarioAsync("recepcion@prueba.pe", RolesDefinidos.Recepcion);

        var creado = await CrearAsync("recepcion@prueba.pe");

        Assert.False(creado.IsSuccess);
        Assert.Equal("Ya existe un usuario con ese correo.", creado.Error);
    }

    [Fact]
    public async Task Una_contrasena_que_no_cumple_las_reglas_no_deja_nada_creado()
    {
        await _identity.SembrarRolesAsync();

        var creado = await CrearAsync(Email, password: "123");

        Assert.False(creado.IsSuccess);
        Assert.Equal(0, await ContarUsuariosAsync());
    }

    [Fact]
    public async Task Restablecer_cambia_la_contrasena_quita_el_bloqueo_y_cierra_las_sesiones()
    {
        await _identity.SembrarRolesAsync();
        await CrearAsync(Email);
        var sesionAnterior = await _identity.EntrarAsync(Email, IdentityDePrueba.Clave);
        for (var i = 0; i < SeguridadLogin.IntentosAntesDeBloquear; i++)
        {
            await _identity.EntrarAsync(Email, "no-es-la-clave");
        }

        var restablecida = await RestablecerAsync(Email, "Nueva.Clave3");

        Assert.True(restablecida.IsSuccess, restablecida.Error);
        Assert.True((await _identity.EntrarAsync(Email, "Nueva.Clave3")).IsSuccess);
        var renovacion = await _identity.ConAsync(sp => sp.GetRequiredService<IAuthService>()
            .RefreshAsync(new RefreshRequest(sesionAnterior.Data!.RefreshToken), "127.0.0.1"));
        Assert.Equal(AuthResultStatus.RefreshTokenRevoked, renovacion.Status);
    }

    [Fact]
    public async Task Restablecer_no_toca_a_usuarios_que_no_son_de_Gerencia()
    {
        await _identity.SembrarRolesAsync();
        await _identity.CrearUsuarioAsync("tecnico@prueba.pe", RolesDefinidos.Tecnico);

        var restablecida = await RestablecerAsync("tecnico@prueba.pe", "Nueva.Clave3");

        Assert.False(restablecida.IsSuccess);
        Assert.True((await _identity.EntrarAsync("tecnico@prueba.pe", IdentityDePrueba.Clave)).IsSuccess);
    }
}
