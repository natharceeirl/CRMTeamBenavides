using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Api.Features.Vehiculos;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Cada unidad activa tiene su propia serie (VIN o número de serie): la identifica al
/// entrar y al salir del taller. La base lo asegura con un índice único filtrado; estas
/// pruebas miran el mensaje que se da antes, en el alta, la edición y la apertura de orden.
/// </summary>
public class SerieUnicaTests : IDisposable
{
    private const string Serie = "ME1RG0710K0001234";
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    private Task<(Guid ClienteId, Guid VehiculoId)> UnidadConSerieAsync() =>
        _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var cliente = new Cliente { NombreCompleto = "Rosa Mamani", NumeroDocumento = "40123456" };
            var unidad = new Vehiculo { Cliente = cliente, Marca = "Yamaha", Modelo = "FZ 2.0", Placa = "ABC-123", NumeroSerieVIN = Serie };
            contexto.AddRange(cliente, unidad);
            await contexto.SaveChangesAsync();
            return (cliente.Id, unidad.Id);
        });

    private static CreateVehiculoRequest Alta(Guid clienteId, string? serie) =>
        new(clienteId, Placa: null, "Yamaha", "MT-03", Anio: null, Kilometraje: null, Color: null, Observaciones: null, NumeroSerieVIN: serie);

    private static UpdateVehiculoRequest Edicion(Guid clienteId, string? placa, string? serie) =>
        new(clienteId, placa, "Yamaha", "MT-03", Anio: null, Kilometraje: null, Color: null, Observaciones: null, NumeroSerieVIN: serie);

    [Fact]
    public async Task Una_serie_ya_registrada_se_rechaza_aunque_cambien_mayusculas_y_espacios()
    {
        var (clienteId, _) = await UnidadConSerieAsync();

        var alta = await _identity.ConAsync(sp =>
            new VehiculoService(sp.GetRequiredService<ApplicationDbContext>()).CreateAsync(Alta(clienteId, "  me1rg0710k0001234 ")));

        Assert.False(alta.IsSuccess);
        Assert.Equal("Ya existe una unidad activa con esa serie.", alta.Error);
    }

    [Fact]
    public async Task Una_serie_nueva_se_guarda_sin_espacios_y_en_mayusculas()
    {
        var (clienteId, _) = await UnidadConSerieAsync();

        var alta = await _identity.ConAsync(sp =>
            new VehiculoService(sp.GetRequiredService<ApplicationDbContext>()).CreateAsync(Alta(clienteId, " me1rg0710k0009999 ")));

        Assert.True(alta.IsSuccess, alta.Error);
        Assert.Equal("ME1RG0710K0009999", alta.Data!.NumeroSerieVIN);
    }

    [Fact]
    public async Task Al_editar_la_unidad_conserva_su_serie_y_otra_no_puede_tomarla()
    {
        var (clienteId, vehiculoId) = await UnidadConSerieAsync();
        var otra = await _identity.ConAsync(sp =>
            new VehiculoService(sp.GetRequiredService<ApplicationDbContext>()).CreateAsync(Alta(clienteId, serie: null)));

        var misma = await _identity.ConAsync(sp =>
            new VehiculoService(sp.GetRequiredService<ApplicationDbContext>()).UpdateAsync(vehiculoId, Edicion(clienteId, "ABC-123", Serie)));
        var robada = await _identity.ConAsync(sp =>
            new VehiculoService(sp.GetRequiredService<ApplicationDbContext>()).UpdateAsync(otra.Data!.Id, Edicion(clienteId, null, Serie.ToLowerInvariant())));

        Assert.True(misma.IsSuccess, misma.Error);
        Assert.False(robada.IsSuccess);
        Assert.Equal("Ya existe otra unidad activa con esa serie.", robada.Error);
    }

    [Fact]
    public async Task Una_unidad_dada_de_baja_libera_su_serie()
    {
        var (clienteId, vehiculoId) = await UnidadConSerieAsync();
        await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            (await contexto.Vehiculos.FindAsync(vehiculoId))!.Activo = false;
            return await contexto.SaveChangesAsync();
        });

        var alta = await _identity.ConAsync(sp =>
            new VehiculoService(sp.GetRequiredService<ApplicationDbContext>()).CreateAsync(Alta(clienteId, Serie)));

        Assert.True(alta.IsSuccess, alta.Error);
    }

    [Fact]
    public async Task La_orden_con_unidad_nueva_no_repite_una_serie_registrada()
    {
        var (clienteId, _) = await UnidadConSerieAsync();

        var apertura = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().CreateAperturaAsync(
            new AperturaOrdenServicioRequest(
                VehiculoId: null, TecnicoAsignadoId: null, Observaciones: null, ClienteId: clienteId,
                UnidadNueva: new UnidadNuevaRequest(TipoUnidad.Motocicleta, TipoMedidor.Kilometraje, "Yamaha", "MT-03",
                    NumeroSerieVIN: Serie.ToLowerInvariant()))));

        Assert.False(apertura.IsSuccess);
        Assert.Contains($"la serie {Serie}", apertura.Error);
    }
}
