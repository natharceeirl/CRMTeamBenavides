using CRMTeamBenavides.Api.Features.Clientes;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Una persona se registra con nombres, primer y segundo apellido, como en su DNI, y el
/// nombre completo los une en ese orden. Una empresa, o un cliente de antes, solo tiene
/// el nombre completo.
/// </summary>
public class NombreDeClienteTests : IDisposable
{
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    [Fact]
    public void Una_persona_une_nombres_y_apellidos_en_ese_orden()
    {
        var (nombre, error) = NombreDeCliente.Resolver("lo que diga", "  Roxana   Karina ", "Delgado", " Huamani ");

        Assert.Null(error);
        Assert.Equal(new NombreResuelto("Roxana Karina Delgado Huamani", "Roxana Karina", "Delgado", "Huamani"), nombre);
    }

    [Fact]
    public void Sin_segundo_apellido_queda_solo_el_primero_y_una_empresa_usa_su_nombre()
    {
        Assert.Equal("John Smith", NombreDeCliente.Resolver(null, "John", "Smith", null).Nombre!.NombreCompleto);
        Assert.Equal(new NombreResuelto("TEAM BENAVIDES S.R.L.", null, null, null),
            NombreDeCliente.Resolver("TEAM BENAVIDES S.R.L.", null, null, null).Nombre);
        Assert.Equal("Escribe el nombre del cliente.", NombreDeCliente.Resolver("  ", " ", "Delgado", null).Error);
    }

    [Fact]
    public async Task El_cliente_se_guarda_con_nombres_y_apellidos_y_se_corrige_uno_de_antes()
    {
        var registrado = await _identity.ConAsync(sp => new ClienteService(sp.GetRequiredService<ApplicationDbContext>())
            .CreateAsync(new CreateClienteRequest("", null, null, null, null, null, null, TipoDocumentoCliente.DNI, "46027897",
                Nombres: "Roxana Karina", ApellidoPaterno: "Delgado", ApellidoMaterno: "Huamani")));

        Assert.True(registrado.IsSuccess, registrado.Error);
        Assert.Equal(("Roxana Karina Delgado Huamani", "Roxana Karina", "Delgado", "Huamani"),
            (registrado.Data!.NombreCompleto, registrado.Data.Nombres, registrado.Data.ApellidoPaterno, registrado.Data.ApellidoMaterno));

        // Un cliente de antes tenía todo en el nombre completo: al editarlo se reparte.
        var antiguoId = await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var antiguo = new Cliente { NombreCompleto = "Luis Quispe Mamani" };
            contexto.Clientes.Add(antiguo);
            await contexto.SaveChangesAsync();
            return antiguo.Id;
        });
        var corregido = await _identity.ConAsync(sp => new ClienteService(sp.GetRequiredService<ApplicationDbContext>())
            .UpdateAsync(antiguoId, new UpdateClienteRequest("Luis Quispe Mamani", null, null, null, null, null, null,
                Nombres: "Luis", ApellidoPaterno: "Quispe", ApellidoMaterno: "Mamani")));

        Assert.True(corregido.IsSuccess, corregido.Error);
        var guardado = await _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().Clientes.SingleAsync(c => c.Id == antiguoId));
        Assert.Equal(("Luis Quispe Mamani", "Luis", "Quispe", "Mamani"),
            (guardado.NombreCompleto, guardado.Nombres, guardado.ApellidoPaterno, guardado.ApellidoMaterno));
    }
}
