using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Pruebas unitarias para YamahaSyncBackgroundService.
/// Valida agenda de Ventas/STE cada 4h, ejecucion diaria de Stock a hora de cierre Peru,
/// prevencion de ejecuciones duplicadas de Stock en el mismo dia y recuperacion de abandonadas al inicio.
/// </summary>
public class YamahaBackgroundServiceTests
{
    private sealed class FakeOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public FakeOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; set; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FakeSyncOrchestrator : IYamahaSyncOrchestrator
    {
        public List<YamahaServicioSincronizacion> ServiciosEjecutados { get; } = new();
        public int TotalRecuperacionesLlamadas { get; private set; }
        public bool DebeFallarStock { get; set; } = false;

        public Task<YamahaSyncEjecucionResultado> SincronizarServicioAsync(
            YamahaServicioSincronizacion servicio,
            bool esSimulacion = false,
            string modoEnvio = "Manual",
            Guid? usuarioId = null,
            DateTime? rangoDesdeUtc = null,
            DateTime? rangoHastaUtc = null,
            CancellationToken cancellationToken = default)
        {
            ServiciosEjecutados.Add(servicio);
            bool exitoso = !(servicio == YamahaServicioSincronizacion.Stock && DebeFallarStock);
            return Task.FromResult(new YamahaSyncEjecucionResultado(
                SyncLogId: Guid.NewGuid(),
                Servicio: servicio,
                Exitoso: exitoso,
                EsSimulacion: esSimulacion,
                TotalLeidos: exitoso ? 5 : 0,
                TotalValidos: exitoso ? 5 : 0,
                TotalRechazados: exitoso ? 0 : 5,
                PayloadHash: exitoso ? "abc" : null,
                CodigoHttp: exitoso ? 200 : 500,
                Mensaje: exitoso ? "OK" : "Fallo simulado",
                ErroresOAdvertencias: exitoso ? Array.Empty<string>() : new[] { "Error de red" },
                Duracion: TimeSpan.FromMilliseconds(10)));
        }

        public Task<int> RecuperarEjecucionesAbandonadasAsync(CancellationToken cancellationToken = default)
        {
            TotalRecuperacionesLlamadas++;
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task BackgroundService_CuandoEnabledEsFalse_NoDebeEjecutarSincronizaciones()
    {
        var services = new ServiceCollection();
        var fakeOrchestrator = new FakeSyncOrchestrator();
        services.AddSingleton<IYamahaSyncOrchestrator>(fakeOrchestrator);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var optionsMonitor = new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings
        {
            Enabled = false
        });

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            optionsMonitor,
            NullLogger<YamahaSyncBackgroundService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await backgroundService.StartAsync(cts.Token);
        await Task.Delay(20);
        await backgroundService.StopAsync(CancellationToken.None);

        Assert.Empty(fakeOrchestrator.ServiciosEjecutados);
    }

    [Fact]
    public async Task EjecutarCicloProgramadoAsync_DebeEjecutarVentasYServicioTecnico()
    {
        var services = new ServiceCollection();
        var fakeOrchestrator = new FakeSyncOrchestrator();
        services.AddSingleton<IYamahaSyncOrchestrator>(fakeOrchestrator);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var optionsMonitor = new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings
        {
            Enabled = true
        });

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            optionsMonitor,
            NullLogger<YamahaSyncBackgroundService>.Instance);

        await backgroundService.EjecutarCicloProgramadoAsync(CancellationToken.None);

        Assert.Equal(2, fakeOrchestrator.ServiciosEjecutados.Count);
        Assert.Contains(YamahaServicioSincronizacion.Ventas, fakeOrchestrator.ServiciosEjecutados);
        Assert.Contains(YamahaServicioSincronizacion.ServicioTecnico, fakeOrchestrator.ServiciosEjecutados);
    }

    [Fact]
    public async Task EjecutarStockProgramadoAsync_DebeEjecutarServicioStock()
    {
        var services = new ServiceCollection();
        var fakeOrchestrator = new FakeSyncOrchestrator();
        services.AddSingleton<IYamahaSyncOrchestrator>(fakeOrchestrator);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var optionsMonitor = new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings
        {
            Enabled = true
        });

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            optionsMonitor,
            NullLogger<YamahaSyncBackgroundService>.Instance);

        await backgroundService.EjecutarStockProgramadoAsync(CancellationToken.None);

        Assert.Single(fakeOrchestrator.ServiciosEjecutados);
        Assert.Contains(YamahaServicioSincronizacion.Stock, fakeOrchestrator.ServiciosEjecutados);
    }

    [Fact]
    public async Task YaSeEjecutoStockCierreHoyAsync_CuandoExisteLogExitosoPosteriorACierre_DebeRetornarTrue()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);

        var hoyPeru = HoraPeru.Hoy();
        var horaCierre = new TimeOnly(22, 0);
        var inicioDiaUtc = HoraPeru.InicioDelDiaUtc(hoyPeru.ToDateTime(TimeOnly.MinValue));
        var horaCierreUtc = inicioDiaUtc.Add(horaCierre.ToTimeSpan());

        var logStockExitoso = new YamahaSyncLog
        {
            Id = Guid.NewGuid(),
            Servicio = YamahaServicioSincronizacion.Stock,
            Estado = YamahaEstadoSincronizacion.Completado,
            Exitoso = true,
            EsSimulacion = false,
            FechaInicio = horaCierreUtc.AddMinutes(5),
            FechaFin = horaCierreUtc.AddMinutes(6),
            ModoEnvio = "Programado"
        };
        context.YamahaSyncLogs.Add(logStockExitoso);
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddScoped(_ => new ApplicationDbContext(options));
        services.AddSingleton<IYamahaSyncOrchestrator>(new FakeSyncOrchestrator());
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings { Enabled = true }),
            NullLogger<YamahaSyncBackgroundService>.Instance);

        var yaEjecutado = await backgroundService.YaSeEjecutoStockCierreHoyAsync(hoyPeru, horaCierre);

        Assert.True(yaEjecutado);
    }

    [Fact]
    public async Task YaSeEjecutoStockCierreHoyAsync_CuandoNoExisteLogExitosoPosteriorACierre_DebeRetornarFalse()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);

        var hoyPeru = HoraPeru.Hoy();
        var horaCierre = new TimeOnly(22, 0);

        var services = new ServiceCollection();
        services.AddScoped(_ => new ApplicationDbContext(options));
        services.AddSingleton<IYamahaSyncOrchestrator>(new FakeSyncOrchestrator());
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings { Enabled = true }),
            NullLogger<YamahaSyncBackgroundService>.Instance);

        var yaEjecutado = await backgroundService.YaSeEjecutoStockCierreHoyAsync(hoyPeru, horaCierre);

        Assert.False(yaEjecutado);
    }

    [Fact]
    public async Task BackgroundService_AlIniciar_DebeRecuperarEjecucionesAbandonadas()
    {
        var services = new ServiceCollection();
        var fakeOrchestrator = new FakeSyncOrchestrator();
        services.AddSingleton<IYamahaSyncOrchestrator>(fakeOrchestrator);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var optionsMonitor = new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings
        {
            Enabled = false
        });

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            optionsMonitor,
            NullLogger<YamahaSyncBackgroundService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await backgroundService.StartAsync(cts.Token);
        await Task.Delay(20);
        await backgroundService.StopAsync(CancellationToken.None);

        Assert.True(fakeOrchestrator.TotalRecuperacionesLlamadas >= 1);
    }

    [Fact]
    public async Task EvaluarYSincronizarSegunAgendaAsync_CuandoStockFalla_PermiteReintentoEnLaMismaJornada()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var fakeOrchestrator = new FakeSyncOrchestrator { DebeFallarStock = true };

        var services = new ServiceCollection();
        services.AddScoped(_ => new ApplicationDbContext(options));
        services.AddSingleton<IYamahaSyncOrchestrator>(fakeOrchestrator);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var optionsMonitor = new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings
        {
            Enabled = true,
            HoraCierreStockPeru = "00:00", // Cierre a las 00:00 asegura que siempre aplique en cualquier momento del día
            IntervaloReintentoStockMinutos = 0 // Sin enfriamiento para prueba de reintento
        });

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            optionsMonitor,
            NullLogger<YamahaSyncBackgroundService>.Instance);

        // Primer intento: falla
        await backgroundService.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(1, fakeOrchestrator.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Stock));

        // Segundo intento: el error se resuelve
        fakeOrchestrator.DebeFallarStock = false;
        await backgroundService.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);

        // H-02: Se demuestra que tras el fallo previo se permitió un nuevo intento en la misma jornada
        Assert.Equal(2, fakeOrchestrator.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Stock));

        // Tercer intento: como el segundo fue exitoso, no debe volver a ejecutarse
        await backgroundService.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(2, fakeOrchestrator.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Stock));
    }

    [Fact]
    public async Task EvaluarYSincronizarSegunAgendaAsync_CuandoStockEsExitoso_NoVuelveAEjecutarseElMismoDia()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var fakeOrchestrator = new FakeSyncOrchestrator { DebeFallarStock = false };

        var services = new ServiceCollection();
        services.AddScoped(_ => new ApplicationDbContext(options));
        services.AddSingleton<IYamahaSyncOrchestrator>(fakeOrchestrator);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var optionsMonitor = new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings
        {
            Enabled = true,
            HoraCierreStockPeru = "00:00"
        });

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            optionsMonitor,
            NullLogger<YamahaSyncBackgroundService>.Instance);

        await backgroundService.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(1, fakeOrchestrator.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Stock));

        // Segunda llamada inmediata
        await backgroundService.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(1, fakeOrchestrator.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Stock));
    }

    [Fact]
    public async Task YaSeEjecutoStockCierreHoyAsync_CuandoExisteSimulacion_NoBloqueaCierreReal()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);

        var hoyPeru = HoraPeru.Hoy();
        var horaCierre = new TimeOnly(0, 0);
        var inicioDiaUtc = HoraPeru.InicioDelDiaUtc(hoyPeru.ToDateTime(TimeOnly.MinValue));
        var horaCierreUtc = inicioDiaUtc.Add(horaCierre.ToTimeSpan());

        // Log de simulación (Dry-Run)
        var logSimulacion = new YamahaSyncLog
        {
            Id = Guid.NewGuid(),
            Servicio = YamahaServicioSincronizacion.Stock,
            Estado = YamahaEstadoSincronizacion.Completado,
            Exitoso = true,
            EsSimulacion = true, // Simulación no cuenta como cierre real
            FechaInicio = horaCierreUtc.AddMinutes(5),
            FechaFin = horaCierreUtc.AddMinutes(6),
            ModoEnvio = "DryRun"
        };
        context.YamahaSyncLogs.Add(logSimulacion);
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddScoped(_ => new ApplicationDbContext(options));
        services.AddSingleton<IYamahaSyncOrchestrator>(new FakeSyncOrchestrator());
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var backgroundService = new YamahaSyncBackgroundService(
            scopeFactory,
            new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings { Enabled = true }),
            NullLogger<YamahaSyncBackgroundService>.Instance);

        var yaEjecutado = await backgroundService.YaSeEjecutoStockCierreHoyAsync(hoyPeru, horaCierre);

        // H-02: Se demuestra que una simulación NO cuenta como cierre real y no bloquea el cierre
        Assert.False(yaEjecutado);
    }
}
