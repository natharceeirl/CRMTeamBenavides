using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Una orden puede llevar varios tipos de atención. El principal sale de la lista con la
/// regla confirmada el 04/11/2026: completo o preventivo es mantenimiento (Yamaha «MP»);
/// si no, garantía, luego gratuito y al final correctivo.
/// </summary>
public class TiposAtencionTests : IDisposable
{
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    [Theory]
    [InlineData(new[] { TipoAtencion.MantenimientoCompleto }, TipoAtencion.MantenimientoCompleto, "MP")]
    [InlineData(new[] { TipoAtencion.MantenimientoCorrectivo, TipoAtencion.MantenimientoCompleto }, TipoAtencion.MantenimientoCompleto, "MP")]
    [InlineData(new[] { TipoAtencion.MantenimientoCorrectivo, TipoAtencion.MantenimientoPreventivo }, TipoAtencion.MantenimientoPreventivo, "MP")]
    [InlineData(new[] { TipoAtencion.MantenimientoCorrectivo, TipoAtencion.ReclamoGarantia }, TipoAtencion.ReclamoGarantia, "G")]
    [InlineData(new[] { TipoAtencion.Gratuito, TipoAtencion.ReclamoGarantia }, TipoAtencion.ReclamoGarantia, "G")]
    [InlineData(new[] { TipoAtencion.MantenimientoCorrectivo, TipoAtencion.Gratuito }, TipoAtencion.Gratuito, "SG")]
    [InlineData(new[] { TipoAtencion.MantenimientoCorrectivo }, TipoAtencion.MantenimientoCorrectivo, "R")]
    public void El_tipo_principal_y_su_codigo_de_Yamaha_salen_de_la_lista(TipoAtencion[] tipos, TipoAtencion principal, string codigoYamaha)
    {
        var orden = new OrdenServicio();

        TiposAtencionOrden.Asignar(orden, tipos);

        Assert.Equal(principal, orden.TipoAtencion);
        Assert.Equal(principal, orden.TiposAtencion[0]);
        Assert.Equal(codigoYamaha, YamahaCatalogMapper.TraducirTipoAtencion(orden.TipoAtencion).Valor);
    }

    [Fact]
    public void La_lista_queda_sin_repetidos_y_una_orden_anterior_trae_su_tipo_unico()
    {
        var orden = new OrdenServicio();
        TiposAtencionOrden.Asignar(orden, [TipoAtencion.MantenimientoCorrectivo, TipoAtencion.MantenimientoCompleto, TipoAtencion.MantenimientoCorrectivo]);

        Assert.Equal([TipoAtencion.MantenimientoCompleto, TipoAtencion.MantenimientoCorrectivo], orden.TiposAtencion);
        Assert.Equal([TipoAtencion.Gratuito], TiposAtencionOrden.DeLaOrden(new OrdenServicio { TipoAtencion = TipoAtencion.Gratuito }));
    }

    [Fact]
    public async Task Al_editar_la_orden_guarda_los_tipos_y_su_principal()
    {
        var ordenId = await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var cliente = new Cliente { NombreCompleto = "Rosa Mamani" };
            var unidad = new Vehiculo { Cliente = cliente, Marca = "Yamaha", Modelo = "FZ 2.0", Placa = "ABC-123" };
            var orden = new OrdenServicio
            {
                Vehiculo = unidad, Cliente = cliente, NumeroOrden = "OS-000902", Estado = EstadoOrdenServicio.Abierta,
                TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            };
            contexto.AddRange(cliente, unidad, orden);
            await contexto.SaveChangesAsync();
            return orden.Id;
        });

        var editada = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().ActualizarAsync(
            ordenId, new ActualizarOrdenServicioRequest(TiposAtencion: [TipoAtencion.MantenimientoCorrectivo, TipoAtencion.MantenimientoCompleto])));

        Assert.True(editada.IsSuccess, editada.Error);
        Assert.Equal([(int)TipoAtencion.MantenimientoCompleto, (int)TipoAtencion.MantenimientoCorrectivo], editada.Data!.TiposAtencionIds);
        var guardada = await _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().OrdenesServicio.SingleAsync(o => o.Id == ordenId));
        Assert.Equal(TipoAtencion.MantenimientoCompleto, guardada.TipoAtencion);
    }

    [Fact]
    public async Task Un_tipo_de_atencion_que_no_existe_se_rechaza_al_abrir_la_orden()
    {
        var apertura = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().CreateAperturaAsync(
            new AperturaOrdenServicioRequest(
                VehiculoId: Guid.NewGuid(), TecnicoAsignadoId: null, Observaciones: null,
                TiposAtencion: [TipoAtencion.MantenimientoCompleto, (TipoAtencion)99])));

        Assert.False(apertura.IsSuccess);
        Assert.Equal("Uno de los tipos de atención no es válido.", apertura.Error);
    }
}
