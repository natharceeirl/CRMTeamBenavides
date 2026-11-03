using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Clientes;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

public class ActivacionCuentaServiceTests : IDisposable
{
    private const string Dni = "40123456";
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    private Task<Guid> CrearClienteAsync(string? dni = Dni) =>
        _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var cliente = new Cliente
            {
                NombreCompleto = "Rosa Quispe",
                TipoDocumento = TipoDocumentoCliente.DNI,
                NumeroDocumento = dni,
                Telefono = "959000111",
                Activo = true
            };
            contexto.Clientes.Add(cliente);
            await contexto.SaveChangesAsync();
            return cliente.Id;
        });

    private Task<ServiceResult<CodigoActivacionResponse>> GenerarAsync(Guid clienteId) =>
        _identity.ConAsync(sp => sp.GetRequiredService<ActivacionCuentaService>().GenerarCodigoAsync(clienteId, null));

    private Task<ServiceResult<Usuario>> ActivarAsync(string codigo, string dni = Dni, string? email = null) =>
        _identity.ConAsync(sp => sp.GetRequiredService<ActivacionCuentaService>()
            .ActivarAsync(new ActivarCuentaRequest(dni, codigo, IdentityDePrueba.Clave, email)));

    private Task<Cliente> LeerClienteAsync(Guid id) =>
        _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().Clientes.AsNoTracking().SingleAsync(c => c.Id == id));

    private static string Otro(string codigo) => codigo == "000000" ? "000001" : "000000";

    [Fact]
    public async Task Genera_un_codigo_de_seis_digitos_que_vence_en_48_horas_y_guarda_solo_su_hash()
    {
        var clienteId = await CrearClienteAsync();

        var generado = await GenerarAsync(clienteId);

        Assert.True(generado.IsSuccess, generado.Error);
        Assert.Matches("^[0-9]{6}$", generado.Data!.Codigo);
        Assert.InRange(generado.Data.Vence, DateTime.UtcNow.AddHours(47.9), DateTime.UtcNow.AddHours(48.1));
        var cliente = await LeerClienteAsync(clienteId);
        Assert.Equal(64, cliente.CodigoActivacionHash!.Length);
        Assert.DoesNotContain(generado.Data.Codigo, cliente.CodigoActivacionHash);
    }

    [Fact]
    public async Task Con_el_DNI_y_el_codigo_queda_ligada_a_su_ficha_con_el_rol_Cliente_y_entra_con_su_DNI()
    {
        await _identity.SembrarRolesAsync();
        var clienteId = await CrearClienteAsync();
        var codigo = (await GenerarAsync(clienteId)).Data!.Codigo;

        var activada = await ActivarAsync(codigo);

        Assert.True(activada.IsSuccess, activada.Error);
        var cliente = await LeerClienteAsync(clienteId);
        Assert.Equal(activada.Data!.Id, cliente.UsuarioId);
        Assert.Null(cliente.CodigoActivacionHash);
        Assert.Equal("Rosa Quispe", activada.Data.NombreCompleto);
        var entrada = await _identity.EntrarAsync(Dni, IdentityDePrueba.Clave);
        Assert.True(entrada.IsSuccess);
        var yo = await _identity.ConAsync(sp => sp.GetRequiredService<IAuthService>().GetCurrentUserAsync(activada.Data.Id));
        Assert.Equal([RolesDefinidos.Cliente], yo.Data!.Roles);
    }

    [Fact]
    public async Task El_codigo_sirve_una_sola_vez()
    {
        await _identity.SembrarRolesAsync();
        var clienteId = await CrearClienteAsync();
        var codigo = (await GenerarAsync(clienteId)).Data!.Codigo;
        await ActivarAsync(codigo);

        var segunda = await ActivarAsync(codigo);

        Assert.False(segunda.IsSuccess);
        Assert.Equal(ActivacionCuentaService.MensajeCodigoInvalido, segunda.Error);
        Assert.Equal(1, await _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().Usuarios.CountAsync()));
    }

    [Fact]
    public async Task Un_DNI_sin_codigo_responde_igual_que_un_codigo_equivocado()
    {
        await _identity.SembrarRolesAsync();
        var clienteId = await CrearClienteAsync();
        var codigo = (await GenerarAsync(clienteId)).Data!.Codigo;

        var equivocado = await ActivarAsync(Otro(codigo));
        var otroDni = await ActivarAsync(codigo, dni: "49999999");

        Assert.Equal(ActivacionCuentaService.MensajeCodigoInvalido, equivocado.Error);
        Assert.Equal(ActivacionCuentaService.MensajeCodigoInvalido, otroDni.Error);
    }

    [Fact]
    public async Task Cinco_codigos_equivocados_invalidan_el_codigo_aunque_despues_llegue_el_correcto()
    {
        await _identity.SembrarRolesAsync();
        var clienteId = await CrearClienteAsync();
        var codigo = (await GenerarAsync(clienteId)).Data!.Codigo;

        for (var intento = 0; intento < ActivacionCuentaService.IntentosMaximos; intento++)
        {
            await ActivarAsync(Otro(codigo));
        }
        var correcto = await ActivarAsync(codigo);

        Assert.False(correcto.IsSuccess);
        Assert.Null((await LeerClienteAsync(clienteId)).UsuarioId);
    }

    [Fact]
    public async Task Un_codigo_vencido_no_sirve()
    {
        await _identity.SembrarRolesAsync();
        var clienteId = await CrearClienteAsync();
        var codigo = (await GenerarAsync(clienteId)).Data!.Codigo;
        await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var cliente = await contexto.Clientes.SingleAsync(c => c.Id == clienteId);
            cliente.CodigoActivacionVence = DateTime.UtcNow.AddMinutes(-1);
            return await contexto.SaveChangesAsync();
        });

        var activada = await ActivarAsync(codigo);

        Assert.Equal(ActivacionCuentaService.MensajeCodigoInvalido, activada.Error);
    }

    [Fact]
    public async Task Un_codigo_nuevo_deja_sin_efecto_el_anterior()
    {
        await _identity.SembrarRolesAsync();
        var clienteId = await CrearClienteAsync();
        var primero = (await GenerarAsync(clienteId)).Data!.Codigo;
        var segundo = (await GenerarAsync(clienteId)).Data!.Codigo;

        var conElPrimero = primero == segundo ? null : await ActivarAsync(primero);
        var conElSegundo = await ActivarAsync(segundo);

        Assert.False(conElPrimero?.IsSuccess ?? false);
        Assert.True(conElSegundo.IsSuccess, conElSegundo.Error);
    }

    [Fact]
    public async Task Con_la_cuenta_ya_activa_no_genera_otro_codigo()
    {
        await _identity.SembrarRolesAsync();
        var clienteId = await CrearClienteAsync();
        await ActivarAsync((await GenerarAsync(clienteId)).Data!.Codigo);

        var otro = await GenerarAsync(clienteId);

        Assert.Equal(ServiceResultStatus.Conflict, otro.Status);
    }

    [Fact]
    public async Task Sin_DNI_registrado_no_genera_codigo()
    {
        var clienteId = await CrearClienteAsync(dni: null);

        var generado = await GenerarAsync(clienteId);

        Assert.Equal(ServiceResultStatus.ValidationError, generado.Status);
        Assert.Contains("DNI", generado.Error);
    }

    [Fact]
    public async Task Un_correo_de_otra_cuenta_no_se_acepta_y_el_codigo_sigue_sirviendo()
    {
        await _identity.SembrarRolesAsync();
        await _identity.CrearUsuarioAsync("tecnico@teambenavides.pe", RolesDefinidos.Tecnico);
        var clienteId = await CrearClienteAsync();
        var codigo = (await GenerarAsync(clienteId)).Data!.Codigo;

        var conCorreoAjeno = await ActivarAsync(codigo, email: "tecnico@teambenavides.pe");
        var sinCorreo = await ActivarAsync(codigo);

        Assert.False(conCorreoAjeno.IsSuccess);
        Assert.Contains("correo", conCorreoAjeno.Error);
        Assert.True(sinCorreo.IsSuccess, sinCorreo.Error);
    }
}
