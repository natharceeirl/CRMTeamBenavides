using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        /// <summary>Resultado que devuelve cada servicio; sin entrada, Exitoso (o Fallido para Stock con DebeFallarStock).</summary>
        public Dictionary<YamahaServicioSincronizacion, YamahaResultadoSincronizacion> ResultadosForzados { get; } = new();

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
            if (!ResultadosForzados.TryGetValue(servicio, out var resultado))
            {
                resultado = servicio == YamahaServicioSincronizacion.Stock && DebeFallarStock
                    ? YamahaResultadoSincronizacion.Fallido
                    : YamahaResultadoSincronizacion.Exitoso;
            }

            bool exitoso = resultado is YamahaResultadoSincronizacion.Exitoso or YamahaResultadoSincronizacion.SinCambios;
            return Task.FromResult(new YamahaSyncEjecucionResultado(
                SyncLogId: resultado == YamahaResultadoSincronizacion.OmitidoPorConcurrencia ? Guid.Empty : Guid.NewGuid(),
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
                Duracion: TimeSpan.FromMilliseconds(10),
                Resultado: resultado));
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

    /// <summary>Reloj controlado por la prueba: la agenda avanza sin esperar en tiempo real.</summary>
    private sealed class RelojFalso : TimeProvider
    {
        public RelojFalso(DateTimeOffset inicio) => Ahora = inicio;

        public DateTimeOffset Ahora { get; set; }

        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    /// <summary>Hora local de Perú (UTC-5) expresada como instante UTC.</summary>
    private static DateTimeOffset HoraPeruEnUtc(int anio, int mes, int dia, int hora, int minuto) =>
        new DateTimeOffset(anio, mes, dia, hora, minuto, 0, TimeSpan.FromHours(-5)).ToUniversalTime();

    private sealed class LoggerCapturador<T> : ILogger<T>
    {
        public List<(LogLevel Nivel, string Mensaje, IReadOnlyDictionary<string, object?> Valores)> Entradas { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var valores = (state as IEnumerable<KeyValuePair<string, object?>>)?
                .ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, object?>();
            Entradas.Add((logLevel, formatter(state, exception), valores));
        }
    }

    private static YamahaSyncBackgroundService CrearServicioConAgenda(
        FakeSyncOrchestrator orquestador,
        YamahaSettings settings,
        TimeProvider? reloj = null,
        ILogger<YamahaSyncBackgroundService>? logger = null,
        bool registrarBaseDeDatos = true)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var services = new ServiceCollection();
        if (registrarBaseDeDatos)
        {
            services.AddScoped(_ => new ApplicationDbContext(options));
        }
        services.AddSingleton<IYamahaSyncOrchestrator>(orquestador);
        var provider = services.BuildServiceProvider();

        return new YamahaSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeOptionsMonitor<YamahaSettings>(settings),
            logger ?? NullLogger<YamahaSyncBackgroundService>.Instance,
            reloj);
    }

    private static int LlamadasStock(FakeSyncOrchestrator orquestador) =>
        orquestador.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Stock);

    [Fact]
    public async Task EvaluarYSincronizarSegunAgendaAsync_CuandoStockFalla_RespetaElEnfriamientoConfigurado()
    {
        var reloj = new RelojFalso(HoraPeruEnUtc(2026, 10, 20, 22, 5));
        var orquestador = new FakeSyncOrchestrator { DebeFallarStock = true };
        var servicio = CrearServicioConAgenda(orquestador, new YamahaSettings
        {
            Enabled = true,
            HoraCierreStockPeru = "22:00",
            IntervaloReintentoStockMinutos = 15
        }, reloj);

        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(1, LlamadasStock(orquestador));

        reloj.Ahora += TimeSpan.FromMinutes(14);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(1, LlamadasStock(orquestador)); // Aún en enfriamiento

        reloj.Ahora += TimeSpan.FromMinutes(1);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(2, LlamadasStock(orquestador)); // A los 15 minutos reintenta
    }

    [Fact]
    public async Task EvaluarYSincronizarSegunAgendaAsync_CierreFallidoAntesDeMedianoche_SeRecuperaYCuentaParaLaJornadaAnterior()
    {
        var reloj = new RelojFalso(HoraPeruEnUtc(2026, 10, 20, 23, 50));
        var orquestador = new FakeSyncOrchestrator { DebeFallarStock = true };
        var servicio = CrearServicioConAgenda(orquestador, new YamahaSettings
        {
            Enabled = true,
            HoraCierreStockPeru = "22:00",
            HoraLimiteRecuperacionStockPeru = "08:00",
            IntervaloReintentoStockMinutos = 15
        }, reloj);

        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(1, LlamadasStock(orquestador)); // Jornada del 20, falla

        // 00:10 del 21: se recupera la jornada del 20 y esta vez funciona
        orquestador.DebeFallarStock = false;
        reloj.Ahora = HoraPeruEnUtc(2026, 10, 21, 0, 10);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(2, LlamadasStock(orquestador));

        // Jornada del 20 cerrada: no hay más intentos en la madrugada ni durante el día
        reloj.Ahora = HoraPeruEnUtc(2026, 10, 21, 0, 11);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        reloj.Ahora = HoraPeruEnUtc(2026, 10, 21, 21, 59);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(2, LlamadasStock(orquestador));

        // 22:00 del 21: empieza la jornada del 21, que no se había cerrado
        reloj.Ahora = HoraPeruEnUtc(2026, 10, 21, 22, 0);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(3, LlamadasStock(orquestador));
    }

    [Fact]
    public async Task EvaluarYSincronizarSegunAgendaAsync_PasadaLaHoraLimite_NoRecuperaYReportaElCierrePerdido()
    {
        var reloj = new RelojFalso(HoraPeruEnUtc(2026, 10, 20, 23, 50));
        var orquestador = new FakeSyncOrchestrator { DebeFallarStock = true };
        var logger = new LoggerCapturador<YamahaSyncBackgroundService>();
        var servicio = CrearServicioConAgenda(orquestador, new YamahaSettings
        {
            Enabled = true,
            HoraCierreStockPeru = "22:00",
            HoraLimiteRecuperacionStockPeru = "08:00",
            IntervaloReintentoStockMinutos = 15
        }, reloj, logger);

        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(1, LlamadasStock(orquestador));

        reloj.Ahora = HoraPeruEnUtc(2026, 10, 21, 8, 1);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        reloj.Ahora = HoraPeruEnUtc(2026, 10, 21, 8, 2);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);

        Assert.Equal(1, LlamadasStock(orquestador)); // Fuera de la ventana: no se intenta
        var perdidos = logger.Entradas.Where(e => e.Nivel == LogLevel.Error && e.Mensaje.Contains("perdido", StringComparison.Ordinal)).ToList();
        var reporte = Assert.Single(perdidos); // Se reporta una sola vez
        Assert.Equal(new DateOnly(2026, 10, 20), reporte.Valores["Jornada"]);
    }

    [Fact]
    public async Task EvaluarYSincronizarSegunAgendaAsync_SiNoSePuedeConsultarElCierre_NoEnviaEnEseMinuto()
    {
        var orquestador = new FakeSyncOrchestrator();
        var servicio = CrearServicioConAgenda(orquestador, new YamahaSettings
        {
            Enabled = true,
            HoraCierreStockPeru = "00:00"
        }, registrarBaseDeDatos: false); // Sin ApplicationDbContext: la consulta del cierre falla

        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);

        Assert.Equal(0, LlamadasStock(orquestador));
    }

    [Fact]
    public void CalcularJornadaStockPendiente_SegunLaHoraDePeru_DevuelveHoyAyerONinguna()
    {
        var cierre = new TimeOnly(22, 0);
        var limite = new TimeOnly(8, 0);

        Assert.Equal(new DateOnly(2026, 10, 20),
            YamahaSyncBackgroundService.CalcularJornadaStockPendiente(HoraPeruEnUtc(2026, 10, 20, 22, 0).UtcDateTime, cierre, limite));
        Assert.Equal(new DateOnly(2026, 10, 20),
            YamahaSyncBackgroundService.CalcularJornadaStockPendiente(HoraPeruEnUtc(2026, 10, 21, 7, 59).UtcDateTime, cierre, limite));
        Assert.Null(
            YamahaSyncBackgroundService.CalcularJornadaStockPendiente(HoraPeruEnUtc(2026, 10, 21, 8, 0).UtcDateTime, cierre, limite));
        Assert.Null(
            YamahaSyncBackgroundService.CalcularJornadaStockPendiente(HoraPeruEnUtc(2026, 10, 21, 7, 59).UtcDateTime, cierre, null));
    }

    [Fact]
    public async Task YaSeEjecutoStockCierreHoyAsync_ExitoEnLaRecuperacion_CuentaParaLaJornadaAnteriorSoloAntesDelLimite()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            context.YamahaSyncLogs.Add(new YamahaSyncLog
            {
                Id = Guid.NewGuid(),
                Servicio = YamahaServicioSincronizacion.Stock,
                Estado = YamahaEstadoSincronizacion.Completado,
                Exitoso = true,
                EsSimulacion = false,
                FechaInicio = HoraPeruEnUtc(2026, 10, 21, 1, 0).UtcDateTime, // madrugada del 21
                FechaFin = HoraPeruEnUtc(2026, 10, 21, 1, 1).UtcDateTime,
                ModoEnvio = "Programado"
            });
            await context.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => new ApplicationDbContext(options));
        services.AddSingleton<IYamahaSyncOrchestrator>(new FakeSyncOrchestrator());
        var provider = services.BuildServiceProvider();

        var backgroundService = new YamahaSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeOptionsMonitor<YamahaSettings>(new YamahaSettings { Enabled = true }),
            NullLogger<YamahaSyncBackgroundService>.Instance);

        var jornada20 = new DateOnly(2026, 10, 20);
        var cierre = new TimeOnly(22, 0);

        Assert.True(await backgroundService.YaSeEjecutoStockCierreHoyAsync(jornada20, cierre, new TimeOnly(8, 0)));
        Assert.False(await backgroundService.YaSeEjecutoStockCierreHoyAsync(jornada20, cierre, horaLimiteRecuperacion: null));
        Assert.False(await backgroundService.YaSeEjecutoStockCierreHoyAsync(new DateOnly(2026, 10, 21), cierre, new TimeOnly(8, 0)));
    }

    [Fact]
    public async Task EvaluarYSincronizarSegunAgendaAsync_CuandoStockSeOmitePorConcurrencia_NoActivaEnfriamientoYReintenta()
    {
        var orquestador = new FakeSyncOrchestrator();
        orquestador.ResultadosForzados[YamahaServicioSincronizacion.Stock] = YamahaResultadoSincronizacion.OmitidoPorConcurrencia;

        var servicio = CrearServicioConAgenda(orquestador, new YamahaSettings
        {
            Enabled = true,
            HoraCierreStockPeru = "00:00",
            IntervaloReintentoStockMinutos = 15
        });

        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(1, orquestador.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Stock));

        // La otra ejecución terminó: el siguiente minuto reintenta sin esperar los 15 minutos de enfriamiento.
        orquestador.ResultadosForzados.Remove(YamahaServicioSincronizacion.Stock);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(2, orquestador.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Stock));

        // Ya cerrado: no se repite.
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(2, orquestador.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Stock));
    }

    [Theory]
    [InlineData(YamahaResultadoSincronizacion.SinCambios)]
    [InlineData(YamahaResultadoSincronizacion.EnviadoSinRegistrar)]
    public async Task EvaluarYSincronizarSegunAgendaAsync_CuandoStockNoTieneCambiosOSeEnvioSinRegistrar_CuentaComoCierreDelDia(
        YamahaResultadoSincronizacion resultadoStock)
    {
        var orquestador = new FakeSyncOrchestrator();
        orquestador.ResultadosForzados[YamahaServicioSincronizacion.Stock] = resultadoStock;

        var servicio = CrearServicioConAgenda(orquestador, new YamahaSettings
        {
            Enabled = true,
            HoraCierreStockPeru = "00:00",
            IntervaloReintentoStockMinutos = 0
        });

        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);

        Assert.Equal(1, orquestador.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Stock));
    }

    [Fact]
    public async Task EvaluarYSincronizarSegunAgendaAsync_CuandoCicloVentasSeOmitePorConcurrencia_NoConsumeElTurno()
    {
        var orquestador = new FakeSyncOrchestrator();
        orquestador.ResultadosForzados[YamahaServicioSincronizacion.Ventas] = YamahaResultadoSincronizacion.OmitidoPorConcurrencia;

        var servicio = CrearServicioConAgenda(orquestador, new YamahaSettings { Enabled = true });

        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(1, orquestador.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Ventas));

        orquestador.ResultadosForzados.Remove(YamahaServicioSincronizacion.Ventas);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(2, orquestador.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Ventas));

        // El ciclo completo sí consume el turno de 4 horas.
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        Assert.Equal(2, orquestador.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Ventas));
    }

    [Fact]
    public async Task EvaluarYSincronizarSegunAgendaAsync_CuandoCicloVentasFalla_EsperaElSiguienteTurno()
    {
        var orquestador = new FakeSyncOrchestrator();
        orquestador.ResultadosForzados[YamahaServicioSincronizacion.Ventas] = YamahaResultadoSincronizacion.Fallido;

        var servicio = CrearServicioConAgenda(orquestador, new YamahaSettings { Enabled = true });

        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);
        await servicio.EvaluarYSincronizarSegunAgendaAsync(CancellationToken.None);

        // Plan v3, sección 5.2: un fallo real queda pendiente para el siguiente turno, sin pérdida de datos.
        Assert.Equal(1, orquestador.ServiciosEjecutados.Count(s => s == YamahaServicioSincronizacion.Ventas));
    }
}
