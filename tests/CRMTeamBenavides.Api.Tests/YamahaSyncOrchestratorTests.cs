using System.Text.RegularExpressions;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha.Exportadores;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Pruebas unitarias para YamahaSyncOrchestrator.
/// Valida el ciclo de vida completo de sincronización, auditoría append-only,
/// cálculo determinista de hash SHA-256, manejo de errores y simulación segura.
/// </summary>
public class YamahaSyncOrchestratorTests
{
    private static ApplicationDbContext CrearDbContextMemoria()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);

        context.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa
        {
            RazonSocial = "Team Benavides S.R.L.",
            Ruc = "20601234567",
            NombreEmpresa = "Team Benavides Arequipa",
            MonedaBase = "PEN"
        });

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task SincronizarServicioAsync_ModoSimulacion_DebeCompletarAuditoriaYGenerarHashSha256()
    {
        using var context = CrearDbContextMemoria();

        // Seed de producto
        context.Productos.Add(new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = Guid.NewGuid(),
            Codigo = "2DP-E4451-00",
            Nombre = "Filtro de Aire NMAX",
            Marca = "YAMAHA",
            StockActual = 12,
            Costo = 40.00m,
            PrecioVenta = 60.00m,
            Activo = true
        });
        await context.SaveChangesAsync();

        var mappingService = new YamahaMappingService();
        var settings = Options.Create(new YamahaSettings
        {
            Enabled = false,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S"
        });

        var stockExporter = new YamahaStockExporter(context, mappingService, settings);
        var ventasExporter = new YamahaVentasExporter(context, mappingService, settings);
        var steExporter = new YamahaServicioTecnicoExporter(context, mappingService, settings);
        var watermarkService = new YamahaWatermarkService(context);
        var fakeClient = new FakeYamahaApiClient();

        var orchestrator = new YamahaSyncOrchestrator(
            context,
            ventasExporter,
            stockExporter,
            steExporter,
            watermarkService,
            fakeClient,
            settings,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var usuarioId = Guid.NewGuid();
        var resultado = await orchestrator.SincronizarServicioAsync(
            YamahaServicioSincronizacion.Stock,
            esSimulacion: true,
            modoEnvio: "ManualTest",
            usuarioId: usuarioId);

        Assert.True(resultado.Exitoso);
        Assert.True(resultado.EsSimulacion);
        Assert.Equal(YamahaServicioSincronizacion.Stock, resultado.Servicio);
        Assert.Equal(1, resultado.TotalValidos);
        Assert.Equal(0, resultado.TotalRechazados);
        Assert.NotNull(resultado.PayloadHash);
        Assert.Matches("^[a-f0-9]{64}$", resultado.PayloadHash); // Hash SHA-256 de 64 caracteres hex

        // Verificar persistencia en base de datos
        var log = await context.YamahaSyncLogs.FindAsync(resultado.SyncLogId);
        Assert.NotNull(log);
        Assert.Equal(YamahaEstadoSincronizacion.Completado, log.Estado);
        Assert.True(log.Exitoso);
        Assert.True(log.EsSimulacion);
        Assert.NotNull(log.FechaFin);
        Assert.Equal("ManualTest", log.ModoEnvio);
        Assert.Equal(usuarioId, log.UsuarioId);
        Assert.Equal(200, log.CodigoHttp);
        Assert.Contains("simulacion", log.RespuestaCuerpo, StringComparison.OrdinalIgnoreCase);

        // La API real de Yamaha no debe haber recibido peticiones
        Assert.Equal(0, fakeClient.TotalLlamadasEnvio);
    }

    [Fact]
    public async Task SincronizarServicioAsync_ModoRealConFakeClient_DebeDespacharYRegistrarExito()
    {
        using var context = CrearDbContextMemoria();

        context.Productos.Add(new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = Guid.NewGuid(),
            Codigo = "90793-AJ412",
            Nombre = "Aceite Yamalube 4T",
            Marca = "YAMAHA",
            StockActual = 20,
            Costo = 25.00m,
            PrecioVenta = 38.00m,
            Activo = true
        });
        await context.SaveChangesAsync();

        var mappingService = new YamahaMappingService();
        var settings = Options.Create(new YamahaSettings
        {
            Enabled = true, // Habilitado para fake client en QA
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S"
        });

        var stockExporter = new YamahaStockExporter(context, mappingService, settings);
        var ventasExporter = new YamahaVentasExporter(context, mappingService, settings);
        var steExporter = new YamahaServicioTecnicoExporter(context, mappingService, settings);
        var watermarkService = new YamahaWatermarkService(context);
        var fakeClient = new FakeYamahaApiClient
        {
            RespuestaSimulada = new YamahaHttpRespuestaDto(201, true, "{\"status\":\"created\"}", null, TimeSpan.FromMilliseconds(50))
        };

        var orchestrator = new YamahaSyncOrchestrator(
            context,
            ventasExporter,
            stockExporter,
            steExporter,
            watermarkService,
            fakeClient,
            settings,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var resultado = await orchestrator.SincronizarServicioAsync(
            YamahaServicioSincronizacion.Stock,
            esSimulacion: false, // Envío real a través de la abstracción
            modoEnvio: "Programado");

        Assert.True(resultado.Exitoso);
        Assert.False(resultado.EsSimulacion);
        Assert.Equal(201, resultado.CodigoHttp);
        Assert.Equal(1, fakeClient.TotalLlamadasEnvio);
        Assert.NotNull(fakeClient.UltimoLoteStock);

        var log = await context.YamahaSyncLogs.FindAsync(resultado.SyncLogId);
        Assert.NotNull(log);
        Assert.Equal(YamahaEstadoSincronizacion.Completado, log.Estado);
        Assert.False(log.EsSimulacion);
        Assert.Equal(201, log.CodigoHttp);
    }

    [Fact]
    public async Task SincronizarServicioAsync_CuandoApiRespondeError_DebeRegistrarFalloEnAuditoria()
    {
        using var context = CrearDbContextMemoria();

        context.Productos.Add(new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = Guid.NewGuid(),
            Codigo = "90793-AJ412",
            Nombre = "Aceite Yamalube 4T",
            Marca = "YAMAHA",
            StockActual = 20,
            Costo = 25.00m,
            PrecioVenta = 38.00m,
            Activo = true
        });
        await context.SaveChangesAsync();

        var mappingService = new YamahaMappingService();
        var settings = Options.Create(new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S"
        });

        var stockExporter = new YamahaStockExporter(context, mappingService, settings);
        var ventasExporter = new YamahaVentasExporter(context, mappingService, settings);
        var steExporter = new YamahaServicioTecnicoExporter(context, mappingService, settings);
        var watermarkService = new YamahaWatermarkService(context);
        var fakeClient = new FakeYamahaApiClient
        {
            RespuestaSimulada = new YamahaHttpRespuestaDto(400, false, "{\"error\":\"Bad request\"}", "Error de contrato", TimeSpan.FromMilliseconds(20))
        };

        var orchestrator = new YamahaSyncOrchestrator(
            context,
            ventasExporter,
            stockExporter,
            steExporter,
            watermarkService,
            fakeClient,
            settings,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var resultado = await orchestrator.SincronizarServicioAsync(
            YamahaServicioSincronizacion.Stock,
            esSimulacion: false);

        Assert.False(resultado.Exitoso);
        Assert.Equal(400, resultado.CodigoHttp);

        var log = await context.YamahaSyncLogs.FindAsync(resultado.SyncLogId);
        Assert.NotNull(log);
        Assert.Equal(YamahaEstadoSincronizacion.Fallido, log.Estado);
        Assert.False(log.Exitoso);
        Assert.Equal(400, log.CodigoHttp);
        Assert.NotNull(log.MensajeError);
    }

    [Fact]
    public async Task RecuperarEjecucionesAbandonadasAsync_DebeRecuperarSoloLogsExpiradosYPreservarRecientes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);

        // 1. Log abandonado (> 30 min)
        var logAbandonado = new YamahaSyncLog
        {
            Id = Guid.NewGuid(),
            Servicio = YamahaServicioSincronizacion.Ventas,
            Estado = YamahaEstadoSincronizacion.EnProceso,
            FechaInicio = DateTime.UtcNow.AddMinutes(-45),
            Exitoso = false,
            EsSimulacion = false
        };

        // 2. Log reciente en curso (< 30 min) - NO debe ser tocado
        var logReciente = new YamahaSyncLog
        {
            Id = Guid.NewGuid(),
            Servicio = YamahaServicioSincronizacion.Ventas,
            Estado = YamahaEstadoSincronizacion.EnProceso,
            FechaInicio = DateTime.UtcNow.AddMinutes(-5),
            Exitoso = false,
            EsSimulacion = false
        };

        // 3. Log completado previo
        var logPrevioCompletado = new YamahaSyncLog
        {
            Id = Guid.NewGuid(),
            Servicio = YamahaServicioSincronizacion.Ventas,
            Estado = YamahaEstadoSincronizacion.Completado,
            FechaInicio = DateTime.UtcNow.AddHours(-2),
            FechaFin = DateTime.UtcNow.AddHours(-2).AddMinutes(1),
            Exitoso = true,
            EsSimulacion = false
        };

        context.YamahaSyncLogs.AddRange(logAbandonado, logReciente, logPrevioCompletado);
        await context.SaveChangesAsync();

        var settings = Options.Create(new YamahaSettings
        {
            TiempoExpiracionEnProcesoMinutos = 30
        });

        var mappingService = new YamahaMappingService();
        var stockExporter = new YamahaStockExporter(context, mappingService, settings);
        var ventasExporter = new YamahaVentasExporter(context, mappingService, settings);
        var steExporter = new YamahaServicioTecnicoExporter(context, mappingService, settings);
        var watermarkService = new YamahaWatermarkService(context);
        var fakeClient = new FakeYamahaApiClient();

        var orchestrator = new YamahaSyncOrchestrator(
            context,
            ventasExporter,
            stockExporter,
            steExporter,
            watermarkService,
            fakeClient,
            settings,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var totalRecuperadas = await orchestrator.RecuperarEjecucionesAbandonadasAsync();

        Assert.Equal(1, totalRecuperadas);

        var log1Recuperado = await context.YamahaSyncLogs.FindAsync(logAbandonado.Id);
        Assert.NotNull(log1Recuperado);
        Assert.Equal(YamahaEstadoSincronizacion.Fallido, log1Recuperado.Estado);
        Assert.False(log1Recuperado.Exitoso);
        Assert.NotNull(log1Recuperado.FechaFin);
        Assert.Contains("expiracion", log1Recuperado.MensajeError, StringComparison.OrdinalIgnoreCase);

        var log2NoTocado = await context.YamahaSyncLogs.FindAsync(logReciente.Id);
        Assert.NotNull(log2NoTocado);
        Assert.Equal(YamahaEstadoSincronizacion.EnProceso, log2NoTocado.Estado);
        Assert.Null(log2NoTocado.FechaFin);

        // Comprueba que el watermark ignora el log fallido y toma el log completado
        var puntoCorte = await watermarkService.ObtenerPuntoCorteDesdeUtcAsync(YamahaServicioSincronizacion.Ventas);
        Assert.Equal(logPrevioCompletado.FechaInicio, puntoCorte);
    }
}
