using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Aprobaciones;
using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Api.Features.Servicios;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// La mano de obra y los terceros se escriben con o sin IGV, a elección de quien los
/// registra; el servicio, con IGV si su catálogo lo incluye. Lo que se escribe con IGV
/// se cobra exacto y se sigue editando con IGV.
/// </summary>
public class PrecioConIgvEnLaOrdenTests : IDisposable
{
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    [Fact]
    public async Task La_mano_de_obra_con_IGV_se_cobra_lo_escrito_y_separa_la_base()
    {
        var escenario = await OrdenEnDiagnosticoAsync();

        var resultado = await AgregarManoDeObraAsync(escenario, 80.00m, incluyeIgv: true);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(80.00m, resultado.Data!.PrecioUnitarioConIgv);
        Assert.Equal(67.80m, resultado.Data.PrecioUnitario);
        Assert.Equal(80.00m, resultado.Data.Total);
        Assert.Equal(80.00m, (await LeerOrdenAsync(escenario.OrdenId)).Total);
    }

    [Fact]
    public async Task La_mano_de_obra_sin_IGV_le_suma_el_18_por_ciento_como_antes()
    {
        var escenario = await OrdenEnDiagnosticoAsync();

        var sinIgv = await AgregarManoDeObraAsync(escenario, 67.80m, incluyeIgv: false);
        var sinIndicar = await AgregarManoDeObraAsync(escenario, 67.80m, incluyeIgv: null);

        Assert.All([sinIgv, sinIndicar], resultado =>
        {
            Assert.True(resultado.IsSuccess, resultado.Error);
            Assert.Null(resultado.Data!.PrecioUnitarioConIgv);
            Assert.Equal(80.00m, resultado.Data.Total);
        });
    }

    [Fact]
    public async Task Terceros_exonerado_no_lleva_IGV_aunque_se_marque_con_IGV()
    {
        var escenario = await OrdenEnDiagnosticoAsync();

        var resultado = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            escenario.OrdenId,
            new AgregarDetalleServicioRequest(
                Descripcion: "Tornería del eje", PrecioUnitario: 50m, TipoItem: TipoItemServicio.Terceros,
                TipoAfectacionIgv: TipoAfectacionIgv.Exonerado, PrecioIncluyeIgv: true),
            puedeModificarPrecios: true, null, escenario.GerenciaId));

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Null(resultado.Data!.PrecioUnitarioConIgv);
        Assert.Equal(50m, resultado.Data.Total);
    }

    [Fact]
    public async Task La_mano_de_obra_con_IGV_se_edita_con_IGV_sin_pedir_aprobacion()
    {
        var escenario = await OrdenEnDiagnosticoAsync();
        var agregada = await AgregarManoDeObraAsync(escenario, 80.00m, incluyeIgv: true);

        var resultado = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().ActualizarDetalleAsync(
            escenario.OrdenId, agregada.Data!.Id, new ActualizarDetalleServicioRequest(PrecioUnitario: 100.00m, Cantidad: 2),
            puedeModificarPrecios: true, null, escenario.GerenciaId));

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(100.00m, resultado.Data!.PrecioUnitarioConIgv);
        Assert.Equal(200.00m, resultado.Data.Total);
        var orden = await LeerOrdenAsync(escenario.OrdenId);
        Assert.Equal(200.00m, orden.Total);
        Assert.Equal(EstadoAprobacionGerencia.NoAplica, orden.EstadoAprobacionGerencia);
    }

    [Fact]
    public async Task El_tecnico_agrega_un_servicio_con_IGV_a_su_precio_sin_pedir_aprobacion()
    {
        var escenario = await OrdenEnDiagnosticoAsync();

        var resultado = await AgregarServicioAsync(escenario, precio: null);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(180.00m, resultado.Data!.PrecioUnitarioConIgv);
        Assert.Equal(152.54m, resultado.Data.PrecioUnitario);
        Assert.Equal(180.00m, resultado.Data.Total);
        Assert.Equal(EstadoAprobacionGerencia.NoAplica, (await LeerOrdenAsync(escenario.OrdenId)).EstadoAprobacionGerencia);
    }

    [Fact]
    public async Task Otro_precio_del_servicio_con_IGV_se_pide_y_al_rechazarlo_vuelve_al_de_lista()
    {
        var escenario = await OrdenEnDiagnosticoAsync();
        await AgregarServicioAsync(escenario, precio: 150.00m);
        var solicitud = await _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().SolicitudesAprobacion
            .AsNoTracking().SingleAsync(s => s.EntidadId == escenario.OrdenId.ToString() && s.Activo));
        Assert.Equal((180.00m, 150.00m), (solicitud.ValorAnterior, solicitud.ValorSolicitado));

        var resuelta = await _identity.ConAsync(sp => sp.GetRequiredService<AprobacionService>().ResolverSolicitudAsync(
            solicitud.Id, new ResolverSolicitudAprobacionRequest(EstadoAprobacionGerencia.Rechazado, "Es el de lista"), escenario.GerenciaId));

        Assert.True(resuelta.IsSuccess, resuelta.Error);
        var orden = await LeerOrdenAsync(escenario.OrdenId);
        var linea = Assert.Single(orden.Detalles);
        Assert.Equal(180.00m, linea.PrecioUnitarioConIgv);
        Assert.Equal(180.00m, orden.Total);
        Assert.Equal(EstadoAprobacionGerencia.NoAplica, orden.EstadoAprobacionGerencia);
    }

    [Fact]
    public async Task Un_servicio_nuevo_va_con_IGV_y_al_editarlo_sin_indicarlo_lo_conserva()
    {
        var servicios = await _identity.ConAsync(async sp =>
        {
            var servicio = new ServicioService(sp.GetRequiredService<ApplicationDbContext>());
            var creado = await servicio.CreateAsync(new CrearServicioRequest("Cambio de aceite", 40m));
            var editado = await servicio.UpdateAsync(creado.Data!.Id, new ActualizarServicioRequest("Cambio de aceite y filtro", 55m));
            var sinIgv = await servicio.UpdateAsync(creado.Data.Id, new ActualizarServicioRequest("Cambio de aceite y filtro", 55m, PrecioIncluyeIgv: false));
            return (creado.Data, editado.Data!, sinIgv.Data!);
        });

        Assert.True(servicios.Item1.PrecioIncluyeIgv);
        Assert.True(servicios.Item2.PrecioIncluyeIgv);
        Assert.False(servicios.Item3.PrecioIncluyeIgv);
    }

    private sealed record Escenario(Guid TecnicoId, Guid GerenciaId, Guid OrdenId, Guid ServicioId);

    /// <summary>Una orden en diagnóstico, del técnico, y «Mantenimiento general» a S/ 180 con IGV.</summary>
    private async Task<Escenario> OrdenEnDiagnosticoAsync()
    {
        await _identity.SembrarRolesAsync();
        var tecnico = await _identity.CrearUsuarioAsync("tecnico@teambenavides.pe", RolesDefinidos.Tecnico);
        var gerencia = await _identity.CrearUsuarioAsync("gerencia@teambenavides.pe", RolesDefinidos.GerenciaAdmin);

        return await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var cliente = new Cliente { NombreCompleto = "Rosa Quispe", NumeroDocumento = "40123456", Activo = true };
            var vehiculo = new Vehiculo { ClienteId = cliente.Id, Marca = "Yamaha", Modelo = "FZ 2.0", Placa = "ABC-123", Activo = true };
            var servicio = new Servicio { Nombre = "Mantenimiento general", PrecioSugerido = 180.00m, PrecioIncluyeIgv = true, Activo = true };
            var orden = new OrdenServicio
            {
                NumeroOrden = "OS-000923",
                ClienteId = cliente.Id,
                VehiculoId = vehiculo.Id,
                TecnicoAsignadoId = tecnico.Id,
                Estado = EstadoOrdenServicio.Diagnostico,
                Activo = true
            };
            contexto.AddRange(cliente, vehiculo, servicio, orden);
            contexto.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa { PorcentajeIgv = 18m });
            await contexto.SaveChangesAsync();
            return new Escenario(tecnico.Id, gerencia.Id, orden.Id, servicio.Id);
        });
    }

    private Task<ServiceResult<DetalleServicioResponse>> AgregarManoDeObraAsync(Escenario escenario, decimal precio, bool? incluyeIgv) =>
        _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            escenario.OrdenId,
            new AgregarDetalleServicioRequest(
                Descripcion: "Desmontar cubremanos, limpiar y lubricar cadena", PrecioUnitario: precio,
                TipoItem: TipoItemServicio.ManoDeObra, PrecioIncluyeIgv: incluyeIgv),
            puedeModificarPrecios: true, null, escenario.GerenciaId));

    /// <summary>Lo agrega el técnico: un precio distinto al de lista espera a Gerencia.</summary>
    private Task<ServiceResult<DetalleServicioResponse>> AgregarServicioAsync(Escenario escenario, decimal? precio) =>
        _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            escenario.OrdenId,
            new AgregarDetalleServicioRequest(ServicioId: escenario.ServicioId, TipoItem: TipoItemServicio.Servicio, PrecioUnitario: precio),
            puedeModificarPrecios: false, escenario.TecnicoId, escenario.TecnicoId));

    private Task<OrdenServicio> LeerOrdenAsync(Guid id) =>
        _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().OrdenesServicio
            .AsNoTracking().Include(o => o.Detalles).SingleAsync(o => o.Id == id));
}
