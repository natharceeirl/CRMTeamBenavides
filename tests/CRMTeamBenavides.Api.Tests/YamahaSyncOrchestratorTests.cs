using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha.Exportadores;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
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

    private sealed class TrackingVentasExporter : IYamahaVentasExporter
    {
        public int LlamadasExportar { get; private set; }
        public IReadOnlyList<YamahaVentaV3Dto> RegistrosRetornados { get; set; } = new List<YamahaVentaV3Dto>();

        public Task<YamahaExportacionResultado<YamahaVentaV3Dto>> ExportarVentasAsync(
            DateTime? desdeUtc = null,
            DateTime? hastaUtc = null,
            bool soloVentasMostrador = true,
            CancellationToken cancellationToken = default)
        {
            LlamadasExportar++;
            return Task.FromResult(new YamahaExportacionResultado<YamahaVentaV3Dto>(
                RegistrosValidos: RegistrosRetornados,
                TotalEntidadesLeidas: RegistrosRetornados.Count,
                TotalRegistrosValidos: RegistrosRetornados.Count,
                TotalRegistrosOmitidos: 0,
                ObservacionesYAdvertencias: new List<string>(),
                RangoDesdeUtc: desdeUtc,
                RangoHastaUtc: hastaUtc
            ));
        }
    }

    [Fact]
    public async Task SincronizarServicioAsync_ModoReal_DebeReutilizarExtraccionValidadaSinReconsultar()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var settings = Options.Create(new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S"
        });

        var dummyDto = new YamahaVentaV3Dto
        {
            RazonSocial = "Team Benavides S.R.L.",
            RUC = "20601234567",
            CodigoPuntoVenta = "TB-AQP-01",
            NombreComercialPuntoVenta = "Team Benavides Arequipa",
            TipoPuntoVenta = "3S",
            FechaFacturacion = "14/10/2026",
            TipoDocumentoVenta = "FACTURA",
            NumeroComprobante = "F001-0000123",
            Canal = "MOSTRADOR",
            LineaNegocio = "SP",
            CodigoYamaha = "90793-AJ412",
            Descripcion = "Aceite Yamalube",
            CantidadVendida = 2,
            MontoSinIGV = 50.00m,
            MontoConIGV = 59.00m,
            PrecioFinal = 59.00m,
            DescuentoPorcentaje = 0.00m,
            DescuentoSoles = 0.00m,
            PrecioUnitarioConIGV = 29.50m,
            TipoPago = "CONTADO",
            EntidadFinanciera = "CONTADO",
            TipoCliente = "CLIENTE FINAL",
            NombreCompletoCliente = "Juan Perez",
            TipoDocumento = "DNI",
            NumeroDocumento = "12345678",
            CanalContactoCliente = "PRESENCIAL",
            CelularCliente = "987654321",
            CorreoCliente = "juan@example.com",
            DireccionCliente = "Av. Ejercito 123",
            CiudadCliente = "Arequipa",
            DepartamentoCliente = "Arequipa"
        };

        var trackingExporter = new TrackingVentasExporter
        {
            RegistrosRetornados = new[] { dummyDto }
        };

        var stockExporter = new YamahaStockExporter(context, mappingService, settings);
        var steExporter = new YamahaServicioTecnicoExporter(context, mappingService, settings);
        var watermarkService = new YamahaWatermarkService(context);
        var fakeClient = new FakeYamahaApiClient
        {
            RespuestaSimulada = new YamahaHttpRespuestaDto(200, true, "{\"status\":\"ok\"}", null, TimeSpan.FromMilliseconds(10))
        };

        var orchestrator = new YamahaSyncOrchestrator(
            context,
            trackingExporter,
            stockExporter,
            steExporter,
            watermarkService,
            fakeClient,
            settings,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var res = await orchestrator.SincronizarServicioAsync(
            YamahaServicioSincronizacion.Ventas,
            esSimulacion: false,
            modoEnvio: "Programado");

        Assert.True(res.Exitoso);
        // H-01: Se verifica que el exportador solo fue consultado exactamente UNA vez
        Assert.Equal(1, trackingExporter.LlamadasExportar);
        Assert.Equal(1, fakeClient.TotalLlamadasEnvio);
        Assert.NotNull(fakeClient.UltimoLoteVentas);
        Assert.Single(fakeClient.UltimoLoteVentas);
        Assert.Same(dummyDto, fakeClient.UltimoLoteVentas[0]);

        // Verificar que el PayloadHash corresponde exactamente al lote enviado
        var jsonEsperado = YamahaJsonSerializer.SerializarVentas(new[] { dummyDto }, indented: false);
        var hashEsperado = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(jsonEsperado))).ToLowerInvariant();
        Assert.Equal(hashEsperado, res.PayloadHash);
    }

    [Fact]
    public async Task SincronizarServicioAsync_CuandoExisteEjecucionEnCurso_DebeRechazarPorConcurrencia()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var settings = Options.Create(new YamahaSettings
        {
            Enabled = false,
            ConcurrenciaEsperaSegundos = 0 // Falla inmediatamente si el semáforo está ocupado
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

        // Simulamos una ejecución activa adquiriendo el semáforo del servicio ServicioTecnico
        var field = typeof(YamahaSyncOrchestrator).GetField("_serviceSemaphores", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var semaphores = (ConcurrentDictionary<YamahaServicioSincronizacion, SemaphoreSlim>)field.GetValue(null)!;
        var sem = semaphores[YamahaServicioSincronizacion.ServicioTecnico];

        await sem.WaitAsync();
        try
        {
            var resConcurrente = await orchestrator.SincronizarServicioAsync(
                YamahaServicioSincronizacion.ServicioTecnico,
                esSimulacion: true,
                modoEnvio: "Manual");

            Assert.False(resConcurrente.Exitoso);
            Assert.Equal(YamahaResultadoSincronizacion.OmitidoPorConcurrencia, resConcurrente.Resultado);
            Assert.Equal(Guid.Empty, resConcurrente.SyncLogId);
            Assert.Contains("Concurrencia detectada", resConcurrente.ErroresOAdvertencias[0]);
            Assert.Contains("en curso", resConcurrente.Mensaje);

            // El rechazo no se registra como sincronización fallida.
            Assert.Empty(context.YamahaSyncLogs);
        }
        finally
        {
            sem.Release();
        }
    }

    private sealed class StockBloqueante : IYamahaStockExporter
    {
        public TaskCompletionSource Entro { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Liberar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Llamadas;

        public async Task<YamahaExportacionResultado<YamahaStockV3Dto>> ExportarStockAsync(
            bool soloMarcaYamaha = false,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Llamadas);
            Entro.TrySetResult();
            await Liberar.Task;
            return new YamahaExportacionResultado<YamahaStockV3Dto>(
                Array.Empty<YamahaStockV3Dto>(), 0, 0, 0, Array.Empty<string>(), null, null);
        }
    }

    [Fact]
    public async Task SincronizarServicioAsync_ProgramadoYManualEnScopesDistintos_ElSegundoSeOmiteSinCrearLog()
    {
        var dbName = Guid.NewGuid().ToString();
        var bloqueante = new StockBloqueante();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(Options.Create(new YamahaSettings { Enabled = false, ConcurrenciaEsperaSegundos = 0 }));
        services.AddSingleton<IYamahaMappingService, YamahaMappingService>();
        services.AddSingleton<IYamahaStockExporter>(bloqueante);
        services.AddScoped<IYamahaVentasExporter, YamahaVentasExporter>();
        services.AddScoped<IYamahaServicioTecnicoExporter, YamahaServicioTecnicoExporter>();
        services.AddScoped<IYamahaWatermarkService, YamahaWatermarkService>();
        services.AddSingleton<IYamahaApiClient, FakeYamahaApiClient>();
        services.AddScoped<IYamahaSyncOrchestrator, YamahaSyncOrchestrator>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scopeProgramado = provider.CreateScope();
        using var scopeManual = provider.CreateScope();
        var orquestadorProgramado = scopeProgramado.ServiceProvider.GetRequiredService<IYamahaSyncOrchestrator>();
        var orquestadorManual = scopeManual.ServiceProvider.GetRequiredService<IYamahaSyncOrchestrator>();
        Assert.NotSame(orquestadorProgramado, orquestadorManual);

        var tareaProgramada = orquestadorProgramado.SincronizarServicioAsync(
            YamahaServicioSincronizacion.Stock, esSimulacion: false, modoEnvio: "Programado");
        await bloqueante.Entro.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Mientras el programado está dentro de la exportación, el manual de otro scope se rechaza.
        var rechazado = await orquestadorManual.SincronizarServicioAsync(
            YamahaServicioSincronizacion.Stock, esSimulacion: true, modoEnvio: "Manual");

        bloqueante.Liberar.SetResult();
        await tareaProgramada;

        Assert.Equal(YamahaResultadoSincronizacion.OmitidoPorConcurrencia, rechazado.Resultado);
        Assert.Equal(Guid.Empty, rechazado.SyncLogId);
        Assert.Equal(1, bloqueante.Llamadas);

        using var scopeLectura = provider.CreateScope();
        var logs = await scopeLectura.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .YamahaSyncLogs.AsNoTracking().ToListAsync();
        var log = Assert.Single(logs);
        Assert.Equal("Programado", log.ModoEnvio);

        // Liberado el semáforo en el finally, la siguiente ejecución sí entra.
        var siguiente = await orquestadorManual.SincronizarServicioAsync(
            YamahaServicioSincronizacion.Stock, esSimulacion: true, modoEnvio: "Manual");
        Assert.NotEqual(YamahaResultadoSincronizacion.OmitidoPorConcurrencia, siguiente.Resultado);
    }

    [Theory]
    [InlineData(YamahaServicioSincronizacion.Ventas)]
    [InlineData(YamahaServicioSincronizacion.Stock)]
    [InlineData(YamahaServicioSincronizacion.ServicioTecnico)]
    public async Task SincronizarServicioAsync_ConLoteVacio_RegistraSinCambiosSinLlamarAYamaha(YamahaServicioSincronizacion servicio)
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var settings = Options.Create(new YamahaSettings
        {
            Enabled = true, // Modo real con cliente fake: aun así no debe haber envío
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S"
        });
        var fakeClient = new FakeYamahaApiClient();

        var orchestrator = new YamahaSyncOrchestrator(
            context,
            new YamahaVentasExporter(context, mappingService, settings),
            new YamahaStockExporter(context, mappingService, settings),
            new YamahaServicioTecnicoExporter(context, mappingService, settings),
            new YamahaWatermarkService(context),
            fakeClient,
            settings,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var resultado = await orchestrator.SincronizarServicioAsync(servicio, esSimulacion: false, modoEnvio: "Programado");

        Assert.Equal(YamahaResultadoSincronizacion.SinCambios, resultado.Resultado);
        Assert.True(resultado.Exitoso);
        Assert.Null(resultado.PayloadHash);
        Assert.Equal(0, fakeClient.TotalLlamadasEnvio);

        var log = await context.YamahaSyncLogs.FindAsync(resultado.SyncLogId);
        Assert.NotNull(log);
        Assert.Equal(YamahaEstadoSincronizacion.SinCambios, log.Estado);
        Assert.True(log.Exitoso);
        Assert.False(log.EsSimulacion);
        Assert.Null(log.CodigoHttp);
        log.ValidarConsistencia();
    }

    [Fact]
    public async Task SincronizarServicioAsync_StockSinProductosProyectables_RegistraSinCambiosConRechazados()
    {
        using var context = CrearDbContextMemoria();
        context.Productos.Add(new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = Guid.NewGuid(),
            Codigo = "90793-AJ412",
            Nombre = "Aceite Yamalube 4T",
            Marca = "YAMAHA",
            StockActual = -1, // Stock negativo: no proyectable al contrato v3
            Costo = 25.00m,
            PrecioVenta = 38.00m,
            Activo = true
        });
        await context.SaveChangesAsync();

        var mappingService = new YamahaMappingService();
        var settings = Options.Create(new YamahaSettings { Enabled = false, CodigoPuntoVenta = "TB-AQP-01", TipoPuntoVenta = "3S" });
        var fakeClient = new FakeYamahaApiClient();

        var orchestrator = new YamahaSyncOrchestrator(
            context,
            new YamahaVentasExporter(context, mappingService, settings),
            new YamahaStockExporter(context, mappingService, settings),
            new YamahaServicioTecnicoExporter(context, mappingService, settings),
            new YamahaWatermarkService(context),
            fakeClient,
            settings,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var resultado = await orchestrator.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: true);

        Assert.Equal(YamahaResultadoSincronizacion.SinCambios, resultado.Resultado);
        Assert.Equal(1, resultado.TotalLeidos);
        Assert.Equal(1, resultado.TotalRechazados);

        var log = await context.YamahaSyncLogs.FindAsync(resultado.SyncLogId);
        Assert.NotNull(log);
        Assert.Equal(YamahaEstadoSincronizacion.SinCambios, log.Estado);
        Assert.Equal(1, log.RegistrosProcesados);
        Assert.Equal(1, log.RegistrosRechazados);
    }

    /// <summary>Handler que nunca sale a la red: responde 200 y guarda los bytes de cada cuerpo recibido.</summary>
    private sealed class CapturaCuerposHandler : HttpMessageHandler
    {
        public List<byte[]> Cuerpos { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Cuerpos.Add(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"ok\"}", Encoding.UTF8, "application/json")
            };
        }
    }

    private static void AgregarProductoYamaha(ApplicationDbContext context)
    {
        context.Productos.Add(new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = Guid.NewGuid(),
            Codigo = "90793-AJ412",
            Nombre = "Aceite Yamalube 4T – ñandú",
            Marca = "YAMAHA",
            StockActual = 20,
            Costo = 25.00m,
            PrecioVenta = 38.00m,
            Activo = true
        });
    }

    [Fact]
    public async Task SincronizarServicioAsync_ConClienteHttpReal_ElPayloadHashDelLogEsElDelCuerpoEnviado()
    {
        using var context = CrearDbContextMemoria();
        AgregarProductoYamaha(context);
        await context.SaveChangesAsync();

        // Enabled solo en este objeto en memoria; el handler intercepta la petición y nunca sale a la red.
        var settings = Options.Create(new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            StockEndpointUrl = "https://yamaha.invalid/api/ReceiveRetailStock"
        });

        var handler = new CapturaCuerposHandler();
        using var httpClient = new HttpClient(handler);
        var apiClient = new YamahaApiClient(httpClient, NullLogger<YamahaApiClient>.Instance, settings);
        var mappingService = new YamahaMappingService();

        var orchestrator = new YamahaSyncOrchestrator(
            context,
            new YamahaVentasExporter(context, mappingService, settings),
            new YamahaStockExporter(context, mappingService, settings),
            new YamahaServicioTecnicoExporter(context, mappingService, settings),
            new YamahaWatermarkService(context),
            apiClient,
            settings,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var resultado = await orchestrator.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: false);

        Assert.Equal(YamahaResultadoSincronizacion.Exitoso, resultado.Resultado);
        var cuerpo = Assert.Single(handler.Cuerpos);
        var hashCuerpo = Convert.ToHexString(SHA256.HashData(cuerpo)).ToLowerInvariant();

        var log = await context.YamahaSyncLogs.FindAsync(resultado.SyncLogId);
        Assert.NotNull(log);
        Assert.Equal(hashCuerpo, log.PayloadHash);
        Assert.Equal(hashCuerpo, resultado.PayloadHash);
    }

    [Fact]
    public async Task SincronizarServicioAsync_SiElHashEnviadoDifiereDelValidado_RegistraElEnviadoYAdvierte()
    {
        using var context = CrearDbContextMemoria();
        AgregarProductoYamaha(context);
        await context.SaveChangesAsync();

        var settings = Options.Create(new YamahaSettings { Enabled = true, CodigoPuntoVenta = "TB-AQP-01", TipoPuntoVenta = "3S" });
        const string hashDistinto = "0000000000000000000000000000000000000000000000000000000000000000";
        var fakeClient = new FakeYamahaApiClient
        {
            RespuestaSimulada = new YamahaHttpRespuestaDto(200, true, "{\"status\":\"ok\"}", null, TimeSpan.Zero, hashDistinto)
        };
        var mappingService = new YamahaMappingService();

        var orchestrator = new YamahaSyncOrchestrator(
            context,
            new YamahaVentasExporter(context, mappingService, settings),
            new YamahaStockExporter(context, mappingService, settings),
            new YamahaServicioTecnicoExporter(context, mappingService, settings),
            new YamahaWatermarkService(context),
            fakeClient,
            settings,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var resultado = await orchestrator.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: false);

        var log = await context.YamahaSyncLogs.FindAsync(resultado.SyncLogId);
        Assert.NotNull(log);
        Assert.Equal(hashDistinto, log.PayloadHash);
        Assert.Contains(resultado.ErroresOAdvertencias, a => a.Contains("no coincide", StringComparison.Ordinal));
    }

    /// <summary>Hace fallar SaveChangesAsync a partir de la llamada indicada (1 = la primera).</summary>
    private sealed class FallarGuardadoDesde : SaveChangesInterceptor
    {
        private readonly int _desde;
        private int _llamadas;

        public FallarGuardadoDesde(int desde) => _desde = desde;

        public int Llamadas => _llamadas;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _llamadas) >= _desde)
            {
                throw new DbUpdateException("Conexión con la base perdida (simulada).");
            }

            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task SincronizarServicioAsync_EnvioAceptadoConFalloAlGuardar_DevuelveEnviadoSinRegistrarYNoLoMarcaFallido()
    {
        var raiz = new InMemoryDatabaseRoot();
        var nombreBd = Guid.NewGuid().ToString();

        await using (var semilla = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nombreBd, raiz).Options))
        {
            semilla.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa
            {
                RazonSocial = "Team Benavides S.R.L.",
                Ruc = "20601234567",
                NombreEmpresa = "Team Benavides Arequipa",
                MonedaBase = "PEN"
            });
            AgregarProductoYamaha(semilla);
            await semilla.SaveChangesAsync();
        }

        // 1.º guardado (log EnProceso) funciona; los siguientes (tras el 2xx y sus reintentos) fallan.
        var interceptor = new FallarGuardadoDesde(2);
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nombreBd, raiz)
            .AddInterceptors(interceptor)
            .Options);

        var settings = Options.Create(new YamahaSettings { Enabled = true, CodigoPuntoVenta = "TB-AQP-01", TipoPuntoVenta = "3S" });
        var fakeClient = new FakeYamahaApiClient();
        var mappingService = new YamahaMappingService();

        var orchestrator = new YamahaSyncOrchestrator(
            context,
            new YamahaVentasExporter(context, mappingService, settings),
            new YamahaStockExporter(context, mappingService, settings),
            new YamahaServicioTecnicoExporter(context, mappingService, settings),
            new YamahaWatermarkService(context),
            fakeClient,
            settings,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var resultado = await orchestrator.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: false);

        Assert.Equal(YamahaResultadoSincronizacion.EnviadoSinRegistrar, resultado.Resultado);
        Assert.True(resultado.Exitoso);
        Assert.Equal(1, fakeClient.TotalLlamadasEnvio);
        Assert.Equal(4, interceptor.Llamadas); // 1 correcto + 3 intentos tras el envío

        await using var lectura = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nombreBd, raiz).Options);
        var log = await lectura.YamahaSyncLogs.AsNoTracking().SingleAsync();

        // Queda EnProceso (el último estado que sí se guardó), nunca reescrito como Fallido.
        Assert.Equal(YamahaEstadoSincronizacion.EnProceso, log.Estado);
    }

    private static YamahaSyncOrchestrator CrearOrquestador(
        ApplicationDbContext context,
        YamahaSettings settings,
        IYamahaApiClient? apiClient = null)
    {
        var opciones = Options.Create(settings);
        var mappingService = new YamahaMappingService();

        return new YamahaSyncOrchestrator(
            context,
            new YamahaVentasExporter(context, mappingService, opciones),
            new YamahaStockExporter(context, mappingService, opciones),
            new YamahaServicioTecnicoExporter(context, mappingService, opciones),
            new YamahaWatermarkService(context),
            apiClient ?? new FakeYamahaApiClient(),
            opciones,
            NullLogger<YamahaSyncOrchestrator>.Instance);
    }

    [Theory]
    [InlineData(YamahaServicioSincronizacion.Ventas)]
    [InlineData(YamahaServicioSincronizacion.ServicioTecnico)]
    public async Task SincronizarServicioAsync_DosLotesReales_ElSegundoParteDelRangoHastaDelPrimeroConMargen(YamahaServicioSincronizacion servicio)
    {
        using var context = CrearDbContextMemoria();
        var orquestador = CrearOrquestador(context, new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            MargenAsentamientoSegundos = 120
        });

        var antes = DateTime.UtcNow;
        var primero = await orquestador.SincronizarServicioAsync(servicio, esSimulacion: false, modoEnvio: "Programado");
        var despues = DateTime.UtcNow;
        var segundo = await orquestador.SincronizarServicioAsync(servicio, esSimulacion: false, modoEnvio: "Programado");

        var log1 = await context.YamahaSyncLogs.FindAsync(primero.SyncLogId);
        var log2 = await context.YamahaSyncLogs.FindAsync(segundo.SyncLogId);
        Assert.NotNull(log1);
        Assert.NotNull(log2);

        // El corte queda 120 s antes de "ahora"
        Assert.NotNull(log1.RangoHastaUtc);
        Assert.InRange(log1.RangoHastaUtc.Value, antes.AddSeconds(-120), despues.AddSeconds(-120));

        // Sin hueco entre lotes: el segundo empieza exactamente donde terminó el primero
        Assert.Equal(log1.RangoHastaUtc, log2.RangoDesdeUtc);
    }

    [Fact]
    public async Task SincronizarServicioAsync_SiElMargenCrece_ElWatermarkNoRetrocede()
    {
        using var context = CrearDbContextMemoria();
        var settings = new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            MargenAsentamientoSegundos = 0
        };

        var primero = await CrearOrquestador(context, settings)
            .SincronizarServicioAsync(YamahaServicioSincronizacion.Ventas, esSimulacion: false);

        settings.MargenAsentamientoSegundos = 600; // el corte calculado quedaría antes del lote anterior
        var segundo = await CrearOrquestador(context, settings)
            .SincronizarServicioAsync(YamahaServicioSincronizacion.Ventas, esSimulacion: false);

        var log1 = await context.YamahaSyncLogs.FindAsync(primero.SyncLogId);
        var log2 = await context.YamahaSyncLogs.FindAsync(segundo.SyncLogId);
        Assert.NotNull(log1);
        Assert.NotNull(log2);
        Assert.Equal(log1.RangoHastaUtc, log2.RangoDesdeUtc);
        Assert.Equal(log2.RangoDesdeUtc, log2.RangoHastaUtc); // rango vacío, sin retroceder
    }

    [Fact]
    public async Task SincronizarServicioAsync_Stock_NoGuardaRangoPorqueEnviaLaFotoCompleta()
    {
        using var context = CrearDbContextMemoria();
        AgregarProductoYamaha(context);
        await context.SaveChangesAsync();

        var resultado = await CrearOrquestador(context, new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S"
        }).SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: false);

        var log = await context.YamahaSyncLogs.FindAsync(resultado.SyncLogId);
        Assert.NotNull(log);
        Assert.Null(log.RangoDesdeUtc);
        Assert.Null(log.RangoHastaUtc);
    }
}
