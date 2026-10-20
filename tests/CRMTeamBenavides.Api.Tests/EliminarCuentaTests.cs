using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Auth;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

public class EliminarCuentaTests : IDisposable
{
    private const string Email = "cliente@prueba.pe";
    private const string Dni = "40123456";
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    /// <summary>Un cliente con cuenta, una sesión abierta, un teléfono para avisos y un aviso en la bandeja.</summary>
    private async Task<(Usuario Usuario, Guid ClienteId, string RefreshToken)> ClienteConCuentaAsync()
    {
        await _identity.SembrarRolesAsync();
        var usuario = await _identity.CrearUsuarioAsync(Email, RolesDefinidos.Cliente);
        var clienteId = await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var cliente = new Cliente
            {
                NombreCompleto = "Rosa Quispe",
                TipoDocumento = TipoDocumentoCliente.DNI,
                NumeroDocumento = Dni,
                DocumentoIdentidad = Dni,
                Telefono = "959123456",
                UsuarioId = usuario.Id
            };
            contexto.Clientes.Add(cliente);
            contexto.DispositivosNotificacion.Add(new DispositivoNotificacion { UsuarioId = usuario.Id, Token = "token-telefono" });
            contexto.Notificaciones.Add(new Notificacion { UsuarioId = usuario.Id, Titulo = "Tu moto está lista", Mensaje = "OS-000091" });
            await contexto.SaveChangesAsync();
            return cliente.Id;
        });
        var sesion = await _identity.EntrarAsync(Email, IdentityDePrueba.Clave);
        return (usuario, clienteId, sesion.Data!.RefreshToken);
    }

    private Task<ServiceResult<bool>> EliminarAsync(Guid usuarioId, string password) =>
        _identity.ConAsync(sp => sp.GetRequiredService<IAuthService>().EliminarCuentaAsync(usuarioId, new EliminarCuentaRequest(password)));

    [Fact]
    public async Task El_cliente_elimina_su_cuenta_y_ya_no_puede_entrar_ni_por_correo_ni_por_DNI()
    {
        var (usuario, _, refreshToken) = await ClienteConCuentaAsync();

        var eliminada = await EliminarAsync(usuario.Id, IdentityDePrueba.Clave);

        Assert.True(eliminada.IsSuccess, eliminada.Error);
        Assert.Equal(AuthResultStatus.InvalidCredentials, (await _identity.EntrarAsync(Email, IdentityDePrueba.Clave)).Status);
        Assert.Equal(AuthResultStatus.InvalidCredentials, (await _identity.EntrarAsync(Dni, IdentityDePrueba.Clave)).Status);
        var renovacion = await _identity.ConAsync(sp => sp.GetRequiredService<IAuthService>()
            .RefreshAsync(new RefreshRequest(refreshToken), "127.0.0.1"));
        Assert.Equal(AuthResultStatus.RefreshTokenRevoked, renovacion.Status);
    }

    [Fact]
    public async Task Se_borran_los_datos_de_la_cuenta_y_se_conserva_la_ficha_del_cliente()
    {
        var (usuario, clienteId, _) = await ClienteConCuentaAsync();

        await EliminarAsync(usuario.Id, IdentityDePrueba.Clave);

        await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var anonimo = await contexto.Usuarios.SingleAsync(u => u.Id == usuario.Id);
            Assert.False(anonimo.Activo);
            Assert.DoesNotContain("cliente@prueba.pe", anonimo.Email);
            Assert.Equal("Cuenta eliminada", anonimo.NombreCompleto);
            Assert.Null(anonimo.PasswordHash);
            Assert.Null(anonimo.PhoneNumber);
            Assert.False(await contexto.DispositivosNotificacion.AnyAsync(d => d.UsuarioId == usuario.Id));
            Assert.False(await contexto.Notificaciones.AnyAsync(n => n.UsuarioId == usuario.Id));

            var cliente = await contexto.Clientes.SingleAsync(c => c.Id == clienteId);
            Assert.Null(cliente.UsuarioId);
            Assert.Equal("Rosa Quispe", cliente.NombreCompleto);
            Assert.True(await contexto.EventosAuditoria.AnyAsync(e => e.Entidad == "Usuario" && e.EntidadId == usuario.Id.ToString()));
            return true;
        });
    }

    [Fact]
    public async Task Con_la_contrasena_equivocada_no_cambia_nada_y_cuenta_como_intento_fallido()
    {
        var (usuario, _, _) = await ClienteConCuentaAsync();

        var eliminada = await EliminarAsync(usuario.Id, "no-es-la-clave");

        Assert.False(eliminada.IsSuccess);
        Assert.Equal("La contraseña no es correcta.", eliminada.Error);
        Assert.Equal(1, (await _identity.LeerUsuarioAsync(Email)).AccessFailedCount);
        Assert.True((await _identity.EntrarAsync(Email, IdentityDePrueba.Clave)).IsSuccess);
    }

    [Theory]
    [InlineData(RolesDefinidos.Tecnico)]
    [InlineData(RolesDefinidos.GerenciaAdmin)]
    public async Task El_personal_no_elimina_su_cuenta_desde_aqui(string rol)
    {
        await _identity.SembrarRolesAsync();
        var usuario = await _identity.CrearUsuarioAsync("personal@prueba.pe", rol);

        var eliminada = await EliminarAsync(usuario.Id, IdentityDePrueba.Clave);

        Assert.Equal(ServiceResultStatus.Forbidden, eliminada.Status);
        Assert.True((await _identity.EntrarAsync("personal@prueba.pe", IdentityDePrueba.Clave)).IsSuccess);
    }

    [Fact]
    public async Task Tampoco_quien_es_cliente_y_ademas_tiene_un_rol_del_personal()
    {
        var (usuario, _, _) = await ClienteConCuentaAsync();
        await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var recepcion = await contexto.Roles.SingleAsync(r => r.Nombre == RolesDefinidos.Recepcion);
            contexto.UsuarioRoles.Add(new UsuarioRol { UsuarioId = usuario.Id, RolId = recepcion.Id });
            return await contexto.SaveChangesAsync();
        });

        var eliminada = await EliminarAsync(usuario.Id, IdentityDePrueba.Clave);

        Assert.Equal(ServiceResultStatus.Forbidden, eliminada.Status);
    }
}
