using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Aprobaciones;
using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Un mantenimiento gratuito aparece en la orden con su valor y se cobra S/ 0.00. Lo
/// marca gratuito quien puede modificar precios; si lo marca otro, lo aprueba Gerencia.
/// </summary>
public class OperacionesGratuitasTests : IDisposable
{
    private const decimal Igv = 18m;
    private const decimal PrecioMantenimiento = 152.54m; // S/ 180 con IGV
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    [Fact]
    public void Las_seis_afectaciones_nuevas_son_gratuitas_y_gravadas()
    {
        var gratuitas = Enum.GetValues<TipoAfectacionIgv>().Where(a => a.EsGratuita()).ToList();

        Assert.Equal(6, gratuitas.Count);
        Assert.All(gratuitas, a => Assert.True(a.EsGravada()));
        Assert.All(AfectacionIgv.Cobradas, a => Assert.False(a.EsGratuita()));
        Assert.Equal("Gravado – Bonificaciones", TipoAfectacionIgv.GravadoBonificaciones.Nombre());
        // Los valores que ya estaban guardados no cambian.
        Assert.Equal(0, (int)TipoAfectacionIgv.Gravado);
        Assert.Equal(2, (int)TipoAfectacionIgv.Inafecto);
    }

    [Fact]
    public void Una_linea_gratuita_con_IGV_incluido_se_cobra_cero_y_vale_lo_escrito()
    {
        var precio = PrecioLista.DeEscrito(180.00m, incluyeIgv: true, TipoAfectacionIgv.GravadoBonificaciones, Igv);
        var finanzas = OrdenServicioService.CalcularFinanzasLinea(1, precio.SinIgv, TipoAfectacionIgv.GravadoBonificaciones, Igv, precio.ConIgv);

        Assert.Equal(180.00m, precio.ConIgv);
        Assert.Equal(PrecioMantenimiento, precio.SinIgv);
        Assert.Equal((0m, 0m, 0m, Igv), finanzas);
        Assert.Equal(180.00m, AfectacionIgv.ValorReferencial(
            TipoAfectacionIgv.GravadoBonificaciones, 1, precio.SinIgv, precio.ConIgv, finanzas.PorcentajeIgvAplicado));
    }

    [Fact]
    public void Una_linea_gratuita_sin_IGV_vale_lo_que_se_habria_cobrado_con_IGV()
    {
        Assert.Equal(236.00m, AfectacionIgv.ValorReferencial(TipoAfectacionIgv.GravadoRetiroPorPremio, 2, 100m, null, Igv));
        Assert.Equal(0m, AfectacionIgv.ValorReferencial(TipoAfectacionIgv.Gravado, 2, 100m, null, Igv));
    }

    [Fact]
    public void La_orden_no_suma_lo_gratuito_al_total_y_lo_muestra_aparte()
    {
        var orden = new OrdenServicio();
        orden.Detalles.Add(Linea(orden.Id, "Filtro de aceite", 50m, TipoAfectacionIgv.Gravado));
        orden.Detalles.Add(Linea(orden.Id, "Mantenimiento general", PrecioMantenimiento, TipoAfectacionIgv.GravadoBonificaciones, 180m));

        OrdenServicioService.RecalcularTotalesOrden(orden);

        Assert.Equal(50m, orden.SubtotalGravado);
        Assert.Equal(9m, orden.MontoIgv);
        Assert.Equal(59m, orden.Total);
        Assert.Equal(180m, orden.SubtotalGratuito);
    }

    [Fact]
    public void Una_linea_gratuita_no_es_ingreso()
    {
        Assert.Equal(0m, AfectacionIgv.IngresoSinIgv(TipoAfectacionIgv.GravadoRetiro, 1, PrecioMantenimiento));
        Assert.Equal(PrecioMantenimiento, AfectacionIgv.IngresoSinIgv(TipoAfectacionIgv.Gravado, 1, PrecioMantenimiento));
    }

    [Fact]
    public void Una_bonificacion_del_proveedor_no_se_paga_ni_cambia_el_costo()
    {
        var montos = CalculoCompra.CalcularLinea(10, 20m, TipoAfectacionIgv.GravadoBonificaciones, Igv, preciosIncluyenIgv: false);
        var costo = CalculoCompra.CostoUnitarioSoles(montos, 10, TipoComprobanteCompra.Factura, 1m);

        Assert.Equal(new CalculoCompra.MontosLinea(0m, 0m, 0m, 236m), montos);
        Assert.Equal(0m, costo);
        // Como cualquier línea a precio 0: el costo del repuesto nunca queda en 0.
        Assert.Equal(30m, CalculoCompra.NuevoCosto(MetodoCosteo.UltimoCosto, 5, 30m, 10, costo));
    }

    [Fact]
    public async Task Si_el_tecnico_marca_un_servicio_gratuito_lo_aprueba_Gerencia()
    {
        var escenario = await OrdenEnDiagnosticoAsync();

        var resultado = await AgregarMantenimientoGratuitoAsync(escenario, puedeModificarPrecios: false, escenario.TecnicoId);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(0m, resultado.Data!.Total);
        Assert.Equal(180m, resultado.Data.ValorReferencial);
        var orden = await LeerOrdenAsync(escenario.OrdenId);
        Assert.Equal(EstadoAprobacionGerencia.Pendiente, orden.EstadoAprobacionGerencia);
        Assert.Equal(0m, orden.Total);
        Assert.Equal(180m, orden.SubtotalGratuito);
        var solicitud = await SolicitudDeAsync(escenario.OrdenId);
        Assert.Equal(AprobacionService.TipoLineaGratuita, solicitud.Tipo);
        Assert.Equal(PrecioMantenimiento, solicitud.ValorAnterior);
        Assert.Equal(0m, solicitud.ValorSolicitado);
    }

    [Fact]
    public async Task Si_Gerencia_rechaza_la_linea_gratuita_vuelve_a_cobrarse_a_su_precio()
    {
        var escenario = await OrdenEnDiagnosticoAsync();
        await AgregarMantenimientoGratuitoAsync(escenario, puedeModificarPrecios: false, escenario.TecnicoId);
        var solicitud = await SolicitudDeAsync(escenario.OrdenId);

        var resultado = await _identity.ConAsync(sp => sp.GetRequiredService<AprobacionService>().ResolverSolicitudAsync(
            solicitud.Id, new ResolverSolicitudAprobacionRequest(EstadoAprobacionGerencia.Rechazado, "Este no aplica"), escenario.GerenciaId));

        Assert.True(resultado.IsSuccess, resultado.Error);
        var orden = await LeerOrdenAsync(escenario.OrdenId);
        var linea = Assert.Single(orden.Detalles);
        Assert.Equal(TipoAfectacionIgv.Gravado, linea.TipoAfectacionIgv);
        Assert.Equal(180m, linea.Total);
        Assert.Equal(180m, orden.Total);
        Assert.Equal(0m, orden.SubtotalGratuito);
        Assert.Equal(EstadoAprobacionGerencia.NoAplica, orden.EstadoAprobacionGerencia);
    }

    [Fact]
    public async Task Si_Gerencia_aprueba_la_linea_sigue_gratuita()
    {
        var escenario = await OrdenEnDiagnosticoAsync();
        await AgregarMantenimientoGratuitoAsync(escenario, puedeModificarPrecios: false, escenario.TecnicoId);
        var solicitud = await SolicitudDeAsync(escenario.OrdenId);

        await _identity.ConAsync(sp => sp.GetRequiredService<AprobacionService>().ResolverSolicitudAsync(
            solicitud.Id, new ResolverSolicitudAprobacionRequest(EstadoAprobacionGerencia.Aprobado, null), escenario.GerenciaId));

        var orden = await LeerOrdenAsync(escenario.OrdenId);
        Assert.Equal(TipoAfectacionIgv.GravadoBonificaciones, Assert.Single(orden.Detalles).TipoAfectacionIgv);
        Assert.Equal(0m, orden.Total);
        Assert.Equal(EstadoAprobacionGerencia.Aprobado, orden.EstadoAprobacionGerencia);
    }

    [Fact]
    public async Task Quien_puede_modificar_precios_marca_la_linea_gratuita_sin_pedir_aprobacion()
    {
        var escenario = await OrdenEnDiagnosticoAsync();

        var resultado = await AgregarMantenimientoGratuitoAsync(escenario, puedeModificarPrecios: true, escenario.GerenciaId);

        Assert.True(resultado.IsSuccess, resultado.Error);
        var orden = await LeerOrdenAsync(escenario.OrdenId);
        Assert.Equal(EstadoAprobacionGerencia.NoAplica, orden.EstadoAprobacionGerencia);
        Assert.Equal(0m, orden.Total);
        Assert.Equal(180m, orden.SubtotalGratuito);
        Assert.False(await _identity.ConAsync(sp =>
            sp.GetRequiredService<ApplicationDbContext>().SolicitudesAprobacion.AnyAsync()));
    }

    [Fact]
    public async Task El_tecnico_que_cambia_una_linea_a_gratuita_tambien_pide_aprobacion()
    {
        var escenario = await OrdenEnDiagnosticoAsync();
        var cobrada = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            escenario.OrdenId,
            new AgregarDetalleServicioRequest(ServicioId: escenario.ServicioId, TipoItem: TipoItemServicio.Servicio),
            puedeModificarPrecios: false, escenario.TecnicoId, escenario.TecnicoId));
        Assert.Equal(EstadoAprobacionGerencia.NoAplica, (await LeerOrdenAsync(escenario.OrdenId)).EstadoAprobacionGerencia);

        var resultado = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().ActualizarDetalleAsync(
            escenario.OrdenId,
            cobrada.Data!.Id,
            new ActualizarDetalleServicioRequest(TipoAfectacionIgv: TipoAfectacionIgv.GravadoRetiro),
            puedeModificarPrecios: false, escenario.TecnicoId, escenario.TecnicoId));

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(0m, resultado.Data!.Total);
        Assert.Equal(EstadoAprobacionGerencia.Pendiente, (await LeerOrdenAsync(escenario.OrdenId)).EstadoAprobacionGerencia);
        Assert.Equal(AprobacionService.TipoLineaGratuita, (await SolicitudDeAsync(escenario.OrdenId)).Tipo);
    }

    private sealed record Escenario(Guid TecnicoId, Guid GerenciaId, Guid OrdenId, Guid ServicioId);

    /// <summary>Una orden en diagnóstico, del técnico, y el servicio «Mantenimiento general» de S/ 152,54 sin IGV.</summary>
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
            var servicio = new Servicio { Nombre = "Mantenimiento general", PrecioSugerido = PrecioMantenimiento, Activo = true };
            var orden = new OrdenServicio
            {
                NumeroOrden = "OS-000923",
                ClienteId = cliente.Id,
                VehiculoId = vehiculo.Id,
                TecnicoAsignadoId = tecnico.Id,
                Estado = EstadoOrdenServicio.Diagnostico,
                TipoAtencion = TipoAtencion.Gratuito,
                Activo = true
            };
            contexto.AddRange(cliente, vehiculo, servicio, orden);
            await contexto.SaveChangesAsync();
            return new Escenario(tecnico.Id, gerencia.Id, orden.Id, servicio.Id);
        });
    }

    private Task<ServiceResult<DetalleServicioResponse>> AgregarMantenimientoGratuitoAsync(
        Escenario escenario, bool puedeModificarPrecios, Guid usuarioId) =>
        _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            escenario.OrdenId,
            new AgregarDetalleServicioRequest(
                ServicioId: escenario.ServicioId,
                TipoItem: TipoItemServicio.Servicio,
                TipoAfectacionIgv: TipoAfectacionIgv.GravadoBonificaciones),
            puedeModificarPrecios,
            puedeModificarPrecios ? null : escenario.TecnicoId,
            usuarioId));

    private Task<OrdenServicio> LeerOrdenAsync(Guid id) =>
        _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().OrdenesServicio
            .AsNoTracking().Include(o => o.Detalles).SingleAsync(o => o.Id == id));

    private Task<SolicitudAprobacion> SolicitudDeAsync(Guid ordenId) =>
        _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().SolicitudesAprobacion
            .AsNoTracking().SingleAsync(s => s.EntidadId == ordenId.ToString() && s.Activo));

    private static DetalleServicio Linea(Guid ordenId, string descripcion, decimal precio, TipoAfectacionIgv afectacion, decimal? conIgv = null)
    {
        var (subtotal, igv, total, porcentaje) = OrdenServicioService.CalcularFinanzasLinea(1, precio, afectacion, Igv, conIgv);
        return new DetalleServicio
        {
            OrdenServicioId = ordenId, TipoItem = TipoItemServicio.Servicio, Descripcion = descripcion, Cantidad = 1,
            PrecioUnitario = precio, PrecioUnitarioConIgv = conIgv, TipoAfectacionIgv = afectacion,
            SubtotalGravado = subtotal, MontoIgv = igv, Total = total, PorcentajeIgvAplicado = porcentaje, Activo = true
        };
    }
}
