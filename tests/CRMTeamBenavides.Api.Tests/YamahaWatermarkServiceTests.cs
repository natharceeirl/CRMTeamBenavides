using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Pruebas unitarias para YamahaWatermarkService.
/// Valida que el punto de corte temporal se derive exclusivamente de ejecuciones reales y exitosas
/// y que jamás avance ante fallos, reintentos o simulaciones.
/// </summary>
public class YamahaWatermarkServiceTests
{
    private static ApplicationDbContext CrearDbContextMemoria()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ObtenerPuntoCorteDesdeUtcAsync_SinLogsPrevios_DebeRetornarTresMesesAtras()
    {
        using var context = CrearDbContextMemoria();
        var service = new YamahaWatermarkService(context);

        var antes = DateTime.UtcNow.AddMonths(-3).AddSeconds(-5);
        var puntoCorte = await service.ObtenerPuntoCorteDesdeUtcAsync(YamahaServicioSincronizacion.Ventas);
        var despues = DateTime.UtcNow.AddMonths(-3).AddSeconds(5);

        Assert.True(puntoCorte >= antes && puntoCorte <= despues);
    }

    [Fact]
    public async Task ObtenerPuntoCorteDesdeUtcAsync_ConLogExitosoReal_DebeRetornarFechaInicioDeEseLog()
    {
        using var context = CrearDbContextMemoria();
        var fechaLog = DateTime.UtcNow.AddDays(-2);

        var log = YamahaSyncLog.Iniciar(
            YamahaServicioSincronizacion.Ventas,
            esSimulacion: false,
            modoEnvio: "Manual");
        log.FechaInicio = fechaLog;
        log.RegistrarExito(procesados: 10, validos: 10, rechazados: 0, payloadHash: "hash-123", codigoHttp: 200);

        context.YamahaSyncLogs.Add(log);
        await context.SaveChangesAsync();

        var service = new YamahaWatermarkService(context);
        var puntoCorte = await service.ObtenerPuntoCorteDesdeUtcAsync(YamahaServicioSincronizacion.Ventas);

        Assert.Equal(fechaLog, puntoCorte);
    }

    [Fact]
    public async Task ObtenerPuntoCorteDesdeUtcAsync_ConLogSimuladoOFallido_NoDebeAvanzarWatermark()
    {
        using var context = CrearDbContextMemoria();

        // 1. Log simulado (EsSimulacion = true)
        var logSimulado = YamahaSyncLog.Iniciar(
            YamahaServicioSincronizacion.ServicioTecnico,
            esSimulacion: true);
        logSimulado.FechaInicio = DateTime.UtcNow.AddHours(-1);
        logSimulado.RegistrarExito(5, 5, 0, "hash-sim", 200, "{\"simulado\":true}");

        // 2. Log fallido (Exitoso = false)
        var logFallido = YamahaSyncLog.Iniciar(
            YamahaServicioSincronizacion.ServicioTecnico,
            esSimulacion: false);
        logFallido.FechaInicio = DateTime.UtcNow.AddMinutes(-30);
        logFallido.RegistrarFallo("Error HTTP 500 de Azure", codigoHttp: 500);

        context.YamahaSyncLogs.AddRange(logSimulado, logFallido);
        await context.SaveChangesAsync();

        var service = new YamahaWatermarkService(context);
        var puntoCorte = await service.ObtenerPuntoCorteDesdeUtcAsync(YamahaServicioSincronizacion.ServicioTecnico);

        // Como ninguno es real y exitoso, debe seguir en los 3 meses reglamentarios
        var esperado = DateTime.UtcNow.AddMonths(-3);
        Assert.True(puntoCorte <= esperado.AddSeconds(5) && puntoCorte >= esperado.AddSeconds(-5));
    }

    [Fact]
    public async Task ObtenerDiagnosticoWatermarksAsync_DebeDiagnosticarLosTresServicios()
    {
        using var context = CrearDbContextMemoria();
        var service = new YamahaWatermarkService(context);

        var diagnostico = await service.ObtenerDiagnosticoWatermarksAsync();

        Assert.Equal(3, diagnostico.Count);
        Assert.Contains(YamahaServicioSincronizacion.Ventas, diagnostico.Keys);
        Assert.Contains(YamahaServicioSincronizacion.Stock, diagnostico.Keys);
        Assert.Contains(YamahaServicioSincronizacion.ServicioTecnico, diagnostico.Keys);
        Assert.All(diagnostico.Values, d => Assert.True(d.EsCargaInicialTresMeses));
    }
}
