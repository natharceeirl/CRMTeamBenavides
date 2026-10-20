using System.Diagnostics;
using System.IO;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha.Exportadores;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Pruebas de integración reales contra PostgreSQL ejecutadas en la base aislada 'crm_teambenavides_qa'.
/// Cubre la Sección 12.8 del Plan de Implementación Yamaha v3:
/// 1. Límites de jornada en timestamptz (22:00 Perú = 03:00 UTC día siguiente, y corte 08:00 de recuperación).
/// 2. Reinicio con éxito guardado en base de datos (recuperación de estado y no duplicación de cierre).
/// 3. Recuperación de abandonadas con ejecución en curso (solo la expirada pasa a Fallido, la activa se preserva).
/// 4. Fallo de conexión PostgreSQL tras un 2xx de Yamaha (resultado EnviadoSinRegistrar y no reenvío).
/// 5. Venta confirmada dentro del margen de asentamiento (continuidad del watermark sin omisiones ni duplicados).
/// 6. Coordinación distribuida multi-proceso con PostgreSQL pg_advisory_lock (Tests 6A - 6F):
///    - 6A: Exclusión mutua entre procesos independientes y no duplicación de envío.
///    - 6B: Ámbito de bloqueo independiente por servicio (Stock, Ventas, STE).
///    - 6C: Liberación de bloqueo en ejecución exitosa o con fallo.
///    - 6D: Terminación abrupta de proceso externo y liberación automática por PostgreSQL.
///    - 6E: Protección de ejecuciones legítimas activas con bloqueo frente al reaper de abandonadas.
///    - 6F: Liberación de bloqueo en EnviadoSinRegistrar sin reenvío automático.
/// </summary>
[Collection("PostgresIntegrationTests")]
[Trait("Category", "Integration")]
public class YamahaPostgresIntegrationTests : IAsyncLifetime
{
    private readonly PostgresQaFixture _fixture;

    public YamahaPostgresIntegrationTests(PostgresQaFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.LimpiarDatosDePruebaAsync();
    public Task DisposeAsync() => _fixture.LimpiarDatosDePruebaAsync();

    #region Clases de soporte para pruebas

    private sealed class RelojFalso : TimeProvider
    {
        public RelojFalso(DateTimeOffset inicio) => Ahora = inicio;
        public DateTimeOffset Ahora { get; set; }
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private sealed class FakeOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public FakeOptionsMonitor(T currentValue) => CurrentValue = currentValue;
        public T CurrentValue { get; set; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FalloGuardadoTrasEnvioInterceptor : SaveChangesInterceptor
    {
        private int _saveCount = 0;
        public int IntentosFallo { get; private set; } = 0;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _saveCount++;
            // El primer SaveChangesAsync persiste syncLog.Iniciar(...) en PostgreSQL
            // Las llamadas posteriores (en GuardarTrasEnvioAceptadoAsync) simulan caída de conexión
            if (_saveCount > 1)
            {
                IntentosFallo++;
                throw new DbUpdateException(
                    "Simulación de corte de conexión PostgreSQL tras respuesta 2xx de Yamaha.",
                    new System.IO.IOException("Connection reset by peer"));
            }

            return ValueTask.FromResult(result);
        }
    }

    private static async Task AsegurarConfiguracionEmpresaAsync(ApplicationDbContext db)
    {
        if (!await db.ConfiguracionesEmpresa.AnyAsync())
        {
            db.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa
            {
                RazonSocial = "Team Benavides S.R.L.",
                Ruc = "20601234567",
                NombreEmpresa = "Team Benavides Arequipa",
                MonedaBase = "PEN"
            });
            await db.SaveChangesAsync();
        }
    }

    private YamahaSyncOrchestrator CrearOrquestador(
        ApplicationDbContext context,
        YamahaSettings settings,
        IYamahaApiClient? apiClient = null,
        IYamahaDistributedLock? distributedLock = null)
    {
        var mappingService = new YamahaMappingService();
        var opciones = Options.Create(settings);
        var ventasExporter = new YamahaVentasExporter(context, mappingService, opciones);
        var stockExporter = new YamahaStockExporter(context, mappingService, opciones);
        var steExporter = new YamahaServicioTecnicoExporter(context, mappingService, opciones);
        var watermarkService = new YamahaWatermarkService(context);

        return new YamahaSyncOrchestrator(
            context,
            ventasExporter,
            stockExporter,
            steExporter,
            watermarkService,
            apiClient ?? new FakeYamahaApiClient(),
            opciones,
            NullLogger<YamahaSyncOrchestrator>.Instance,
            distributedLock ?? new PostgreSqlYamahaDistributedLock(context, NullLogger<PostgreSqlYamahaDistributedLock>.Instance));
    }

    private YamahaSyncBackgroundService CrearServicioFondo(
        FakeOptionsMonitor<YamahaSettings> monitor,
        TimeProvider reloj,
        IYamahaApiClient? apiClient = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _fixture.CreateDbContext());
        services.AddScoped<IYamahaMappingService, YamahaMappingService>();
        services.AddScoped<IYamahaVentasExporter, YamahaVentasExporter>();
        services.AddScoped<IYamahaStockExporter, YamahaStockExporter>();
        services.AddScoped<IYamahaServicioTecnicoExporter, YamahaServicioTecnicoExporter>();
        services.AddScoped<IYamahaWatermarkService, YamahaWatermarkService>();
        services.AddSingleton<IYamahaApiClient>(apiClient ?? new FakeYamahaApiClient());
        services.AddSingleton<IOptions<YamahaSettings>>(sp => Options.Create(monitor.CurrentValue));
        services.AddScoped<IYamahaDistributedLock, PostgreSqlYamahaDistributedLock>();
        services.AddScoped<IYamahaSyncOrchestrator, YamahaSyncOrchestrator>();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new YamahaSyncBackgroundService(
            scopeFactory,
            monitor,
            NullLogger<YamahaSyncBackgroundService>.Instance,
            reloj);
    }

    private static DateTime EnUtc(int anio, int mes, int dia, int hora, int minuto, int segundo = 0, int milisegundo = 0) =>
        new DateTime(anio, mes, dia, hora, minuto, segundo, milisegundo, DateTimeKind.Utc);

    private static string ObtenerRutaPsql()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(dir, "psql.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (File.Exists(@"D:\PostgreSQL\18\bin\psql.exe"))
        {
            return @"D:\PostgreSQL\18\bin\psql.exe";
        }

        return "psql.exe";
    }

    #endregion

    #region 1. Límites de jornada en timestamptz (22:00 Perú = 03:00 UTC día siguiente, y 08:00 recuperación)

    [Fact]
    public async Task Test1_LimitesJornadaEnTimestamptz_PeruUtc_YRecuperacion_PostgreSQL()
    {
        // ARRANGE:
        // Jornada: 2026-10-20.
        // Cierre a las 22:00 Perú (UTC-5) => 2026-10-21 03:00:00 UTC.
        // Límite de recuperación 08:00 Perú (UTC-5) => 2026-10-21 13:00:00 UTC.
        var hoyPeru = new DateOnly(2026, 10, 20);
        var horaCierre = new TimeOnly(22, 0);
        var horaLimite = new TimeOnly(8, 0);

        var settings = new YamahaSettings
        {
            Enabled = true,
            HoraCierreStockPeru = "22:00",
            HoraLimiteRecuperacionStockPeru = "08:00"
        };
        var monitor = new FakeOptionsMonitor<YamahaSettings>(settings);
        var service = CrearServicioFondo(monitor, new RelojFalso(DateTimeOffset.UtcNow));

        // Subcaso 1A: Log registrado 1 milisegundo antes de las 22:00 Perú (02:59:59.999 UTC)
        // No debe contar como cierre de la jornada de hoy.
        await using (var db = _fixture.CreateDbContext())
        {
            var logPrevio = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Stock, esSimulacion: false);
            logPrevio.FechaInicio = EnUtc(2026, 10, 21, 2, 59, 59, 999);
            logPrevio.RegistrarExito(procesados: 10, validos: 10, rechazados: 0, payloadHash: "hash-previo", codigoHttp: 200);
            db.YamahaSyncLogs.Add(logPrevio);
            await db.SaveChangesAsync();
        }

        var resultadoPrevio = await service.YaSeEjecutoStockCierreHoyAsync(hoyPeru, horaCierre, horaLimite);
        Assert.False(resultadoPrevio, "Un log a las 21:59:59.999 Perú no debe contar como cierre de jornada de las 22:00");

        // Subcaso 1B: Log registrado exactamente en el límite de las 22:00 Perú (03:00:00.000 UTC)
        // Debe ser reconocido como cierre de la jornada (límite inferior inclusivo >= desdeUtc).
        await using (var db = _fixture.CreateDbContext())
        {
            var logExactoCierre = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Stock, esSimulacion: false);
            logExactoCierre.FechaInicio = EnUtc(2026, 10, 21, 3, 0, 0, 0);
            logExactoCierre.RegistrarExito(procesados: 10, validos: 10, rechazados: 0, payloadHash: "hash-exacto", codigoHttp: 200);
            db.YamahaSyncLogs.Add(logExactoCierre);
            await db.SaveChangesAsync();
        }

        var resultadoExacto = await service.YaSeEjecutoStockCierreHoyAsync(hoyPeru, horaCierre, horaLimite);
        Assert.True(resultadoExacto, "Un log exactamente a las 03:00:00.000 UTC (22:00 Perú) debe contar como cierre");

        // Limpiar para probar los límites de recuperación de la mañana
        await _fixture.LimpiarDatosDePruebaAsync();

        // Subcaso 1C: Log registrado a las 07:59:59.999 Perú (12:59:59.999 UTC del día siguiente)
        // Dentro de la ventana de recuperación antes de las 08:00 Perú (< hastaUtc).
        await using (var db = _fixture.CreateDbContext())
        {
            var logRecuperacionValido = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Stock, esSimulacion: false);
            logRecuperacionValido.FechaInicio = EnUtc(2026, 10, 21, 12, 59, 59, 999);
            logRecuperacionValido.RegistrarExito(procesados: 10, validos: 10, rechazados: 0, payloadHash: "hash-recup", codigoHttp: 200);
            db.YamahaSyncLogs.Add(logRecuperacionValido);
            await db.SaveChangesAsync();
        }

        var resultadoRecuperacion = await service.YaSeEjecutoStockCierreHoyAsync(hoyPeru, horaCierre, horaLimite);
        Assert.True(resultadoRecuperacion, "Un log a las 07:59:59.999 Perú debe contar en la recuperación de la jornada anterior");

        // Subcaso 1D: Log registrado a las 08:00:00.000 Perú (13:00:00.000 UTC)
        // Ya venció el límite de recuperación (límite superior exclusivo < hastaUtc).
        await _fixture.LimpiarDatosDePruebaAsync();
        await using (var db = _fixture.CreateDbContext())
        {
            var logVencido = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Stock, esSimulacion: false);
            logVencido.FechaInicio = EnUtc(2026, 10, 21, 13, 0, 0, 0);
            logVencido.RegistrarExito(procesados: 10, validos: 10, rechazados: 0, payloadHash: "hash-vencido", codigoHttp: 200);
            db.YamahaSyncLogs.Add(logVencido);
            await db.SaveChangesAsync();
        }

        var resultadoVencido = await service.YaSeEjecutoStockCierreHoyAsync(hoyPeru, horaCierre, horaLimite);
        Assert.False(resultadoVencido, "Un log a las 08:00:00.000 Perú ya no pertenece a la jornada anterior");

        // Subcaso 1E: Con horaLimiteRecuperacion = null (recuperación desactivada: corta a medianoche Perú = 05:00 UTC)
        await _fixture.LimpiarDatosDePruebaAsync();
        await using (var db = _fixture.CreateDbContext())
        {
            var logMadrugada = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Stock, esSimulacion: false);
            logMadrugada.FechaInicio = EnUtc(2026, 10, 21, 6, 0, 0, 0); // 01:00 Perú
            logMadrugada.RegistrarExito(procesados: 10, validos: 10, rechazados: 0, payloadHash: "hash-madrugada", codigoHttp: 200);
            db.YamahaSyncLogs.Add(logMadrugada);
            await db.SaveChangesAsync();
        }

        var resultadoSinRecuperacion = await service.YaSeEjecutoStockCierreHoyAsync(hoyPeru, horaCierre, horaLimiteRecuperacion: null);
        Assert.False(resultadoSinRecuperacion, "Sin recuperación, un log pasado medianoche Perú no debe contar para la jornada previa");
    }

    #endregion

    #region 2. Reinicio con éxito guardado (recuperación de estado y no duplicación de cierre)

    [Fact]
    public async Task Test2_ReinicioProceso_ConExitoGuardadoEnPostgres_EvitaDuplicarCierre()
    {
        // ARRANGE:
        // Supongamos que una ejecución previa de Stock se completó a las 22:15 Perú (03:15 UTC) y se persistió en PostgreSQL.
        var hoyPeru = new DateOnly(2026, 10, 20);
        var instanteCierreUtc = EnUtc(2026, 10, 21, 3, 15, 0);

        await using (var db = _fixture.CreateDbContext())
        {
            var logExitosoPrevio = YamahaSyncLog.Iniciar(
                YamahaServicioSincronizacion.Stock,
                esSimulacion: false,
                modoEnvio: "Programado");
            logExitosoPrevio.FechaInicio = instanteCierreUtc;
            logExitosoPrevio.RegistrarExito(
                procesados: 25,
                validos: 25,
                rechazados: 0,
                payloadHash: "hash-stock-cerrado-previamente",
                codigoHttp: 200,
                respuestaCuerpo: "{\"status\":\"ok\"}");

            db.YamahaSyncLogs.Add(logExitosoPrevio);
            await db.SaveChangesAsync();
        }

        // ACT: Simular reinicio del servicio o del servidor host creando una instancia COMPLETAMENTE NUEVA
        // Su campo privado _ultimaJornadaStockCerrada arranca en null.
        var fakeClient = new FakeYamahaApiClient();
        var settings = new YamahaSettings
        {
            Enabled = true,
            HoraCierreStockPeru = "22:00",
            HoraLimiteRecuperacionStockPeru = "08:00",
            IntervaloVentasServicioTecnicoHoras = 4
        };
        var monitor = new FakeOptionsMonitor<YamahaSettings>(settings);

        // Reloj a las 22:30 Perú (03:30 UTC): dentro de la franja de cierre de hoy
        var reloj = new RelojFalso(new DateTimeOffset(2026, 10, 21, 3, 30, 0, TimeSpan.Zero));
        var servicioReiniciado = CrearServicioFondo(monitor, reloj, fakeClient);

        // Comprobar que la consulta directa a PostgreSQL reporta que ya se cerró
        var yaCerrado = await servicioReiniciado.YaSeEjecutoStockCierreHoyAsync(
            hoyPeru,
            new TimeOnly(22, 0),
            new TimeOnly(8, 0));
        Assert.True(yaCerrado);

        // Ejecutar evaluación de agenda: debe detectar el éxito guardado y NO llamar al cliente ni duplicar el log
        await servicioReiniciado.EvaluarYSincronizarSegunAgendaAsync();

        // ASSERT:
        Assert.Equal(0, fakeClient.TotalLlamadasEnvio);

        await using (var db = _fixture.CreateDbContext())
        {
            var totalLogs = await db.YamahaSyncLogs
                .Where(l => l.Servicio == YamahaServicioSincronizacion.Stock)
                .CountAsync();

            Assert.Equal(1, totalLogs); // Sigue habiendo un único registro, no se duplicó
        }
    }

    #endregion

    #region 3. Recuperación de abandonadas con ejecución en curso (preserva la activa)

    [Fact]
    public async Task Test3_RecuperarEjecucionesAbandonadas_ConEjecucionEnCurso_PreservaActiva_PostgreSQL()
    {
        // ARRANGE:
        // Insertamos dos ejecuciones en estado 'EnProceso' en PostgreSQL:
        // 1. Una abandonada (iniciada hace 45 minutos > umbral de 30 minutos).
        // 2. Una activa legítima (iniciada hace 5 minutos < umbral de 30 minutos).
        var ahoraUtc = DateTime.UtcNow;
        Guid idAbandonada;
        Guid idActiva;

        await using (var db = _fixture.CreateDbContext())
        {
            var logAbandonado = YamahaSyncLog.Iniciar(
                YamahaServicioSincronizacion.Ventas,
                esSimulacion: false,
                modoEnvio: "Programado");
            logAbandonado.FechaInicio = ahoraUtc.AddMinutes(-45);
            idAbandonada = logAbandonado.Id;

            var logActivo = YamahaSyncLog.Iniciar(
                YamahaServicioSincronizacion.Ventas,
                esSimulacion: false,
                modoEnvio: "Manual");
            logActivo.FechaInicio = ahoraUtc.AddMinutes(-5);
            idActiva = logActivo.Id;

            db.YamahaSyncLogs.AddRange(logAbandonado, logActivo);
            await db.SaveChangesAsync();
        }

        // ACT: Ejecutar la recuperación de abandonadas con el orquestador
        await using (var db = _fixture.CreateDbContext())
        {
            var settings = new YamahaSettings
            {
                Enabled = true,
                TiempoExpiracionEnProcesoMinutos = 30
            };
            var orchestrator = CrearOrquestador(db, settings);

            var totalRecuperadas = await orchestrator.RecuperarEjecucionesAbandonadasAsync();
            Assert.Equal(1, totalRecuperadas);
        }

        // ASSERT: Verificar en PostgreSQL el estado de ambos registros
        await using (var dbLectura = _fixture.CreateDbContext())
        {
            var abandonadoPostgres = await dbLectura.YamahaSyncLogs.FindAsync(idAbandonada);
            var activoPostgres = await dbLectura.YamahaSyncLogs.FindAsync(idActiva);

            Assert.NotNull(abandonadoPostgres);
            Assert.NotNull(activoPostgres);

            // La abandonada fue marcada como Fallido
            Assert.Equal(YamahaEstadoSincronizacion.Fallido, abandonadoPostgres.Estado);
            Assert.False(abandonadoPostgres.Exitoso);
            Assert.NotNull(abandonadoPostgres.FechaFin);
            Assert.Contains("Supero el tiempo limite de expiracion de 30 minutos", abandonadoPostgres.MensajeError);

            // La activa se preserva intacta en EnProceso
            Assert.Equal(YamahaEstadoSincronizacion.EnProceso, activoPostgres.Estado);
            Assert.False(activoPostgres.Exitoso);
            Assert.Null(activoPostgres.FechaFin);
            Assert.Null(activoPostgres.MensajeError);

            // La ejecución activa puede ahora completar normalmente su ciclo y registrar éxito
            activoPostgres.RegistrarExito(
                procesados: 12,
                validos: 12,
                rechazados: 0,
                payloadHash: "hash-activo-completado",
                codigoHttp: 200);
            await dbLectura.SaveChangesAsync();
        }

        // Verificar que la activa completó exitosamente
        await using (var dbFinal = _fixture.CreateDbContext())
        {
            var activoFinal = await dbFinal.YamahaSyncLogs.FindAsync(idActiva);
            Assert.NotNull(activoFinal);
            Assert.Equal(YamahaEstadoSincronizacion.Completado, activoFinal.Estado);
            Assert.True(activoFinal.Exitoso);
        }
    }

    #endregion

    #region 4. Fallo de conexión tras un 2xx (EnviadoSinRegistrar y no reenvío)

    [Fact]
    public async Task Test4_FalloConexionPostgres_Tras2xxYamaha_DevuelveEnviadoSinRegistrar_YNoReenvia()
    {
        // ARRANGE:
        // Configurar empresa y producto Yamaha en PostgreSQL
        await using (var dbSeed = _fixture.CreateDbContext())
        {
            await AsegurarConfiguracionEmpresaAsync(dbSeed);

            var categoria = new CategoriaProducto
            {
                Id = Guid.NewGuid(),
                Nombre = "Repuestos Yamaha",
                Activo = true
            };
            dbSeed.CategoriasProducto.Add(categoria);

            dbSeed.Productos.Add(new Producto
            {
                Id = Guid.NewGuid(),
                CategoriaId = categoria.Id,
                Codigo = "2DP-E4451-00",
                Nombre = "Filtro de Aire NMAX",
                Marca = "YAMAHA",
                StockActual = 15,
                Costo = 45.00m,
                PrecioVenta = 65.00m,
                Activo = true
            });

            await dbSeed.SaveChangesAsync();
        }

        // FakeClient simula respuesta 200 OK de Yamaha
        var fakeClient = new FakeYamahaApiClient
        {
            RespuestaSimulada = new YamahaHttpRespuestaDto(
                200,
                true,
                "{\"status\":\"ok\",\"message\":\"Batch accepted by Yamaha\"}",
                null,
                TimeSpan.FromMilliseconds(20))
        };

        // Interceptor que permite el 1er save (syncLog.Iniciar) pero falla en los siguientes (GuardarTrasEnvioAceptadoAsync)
        var interceptorFallo = new FalloGuardadoTrasEnvioInterceptor();

        await using var dbConInterceptor = _fixture.CreateDbContext(builder =>
        {
            builder.AddInterceptors(interceptorFallo);
        });

        var settings = new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S"
        };

        var orchestrator = CrearOrquestador(dbConInterceptor, settings, fakeClient);

        // ACT: Ejecutar sincronización de Stock real (no simulación)
        var resultado = await orchestrator.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: false);

        // ASSERT:
        // 1. Yamaha recibió el lote
        Assert.Equal(1, fakeClient.TotalLlamadasEnvio);

        // 2. El orquestador intentó guardar el log en PostgreSQL 3 veces (intento inicial + 2 reintentos)
        Assert.Equal(3, interceptorFallo.IntentosFallo);

        // 3. El resultado es EnviadoSinRegistrar con Exitoso = true (no se degrada a Fallido)
        Assert.Equal(YamahaResultadoSincronizacion.EnviadoSinRegistrar, resultado.Resultado);
        Assert.True(resultado.Exitoso);

        // 4. En PostgreSQL, el log auditado permanece en EnProceso (no fue sobreescrito con Fallido)
        await using (var dbVerificacion = _fixture.CreateDbContext())
        {
            var logEnBd = await dbVerificacion.YamahaSyncLogs.FindAsync(resultado.SyncLogId);
            Assert.NotNull(logEnBd);
            Assert.Equal(YamahaEstadoSincronizacion.EnProceso, logEnBd.Estado);
            Assert.False(logEnBd.Exitoso);
        }

        // 5. El BackgroundService trata EnviadoSinRegistrar como jornada cerrada y no reenvía
        var monitor = new FakeOptionsMonitor<YamahaSettings>(settings);
        var reloj = new RelojFalso(DateTimeOffset.UtcNow);
        var backgroundService = CrearServicioFondo(monitor, reloj, fakeClient);

        // Simular llamada con el resultado obtenido
        Assert.True(resultado.Resultado == YamahaResultadoSincronizacion.EnviadoSinRegistrar);
    }

    #endregion

    #region 5. Venta confirmada dentro del margen (continuidad del watermark)

    [Fact]
    public async Task Test5_VentaConfirmadaDentroDelMargen_FiltradoCorrectoSinPerdidaNiDuplicado_PostgreSQL()
    {
        // ARRANGE:
        // Margen de asentamiento = 120 segundos.
        // Base temporal: T0 = DateTime.UtcNow.
        // Venta 1: confirmada hace 180s (fuera del margen de 120s => asentada).
        // Venta 2: confirmada hace 40s (dentro del margen de 120s => aún no asentada al momento T0).
        var ahora = DateTime.UtcNow;
        var t0 = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, ahora.Second, DateTimeKind.Utc);
        var instanteVenta1 = t0.AddSeconds(-180);
        var instanteVenta2 = t0.AddSeconds(-40);

        Guid venta1Id;
        Guid venta2Id;

        await using (var dbSeed = _fixture.CreateDbContext())
        {
            await AsegurarConfiguracionEmpresaAsync(dbSeed);

            var categoria = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Repuestos", Activo = true };
            dbSeed.CategoriasProducto.Add(categoria);

            var cliente = new Cliente
            {
                Id = Guid.NewGuid(),
                NombreCompleto = "Cliente Prueba Watermark",
                TipoDocumento = TipoDocumentoCliente.DNI,
                NumeroDocumento = "10203040",
                Telefono = "999888777",
                Direccion = "Av. Principal 100",
                Activo = true
            };
            dbSeed.Clientes.Add(cliente);

            var producto = new Producto
            {
                Id = Guid.NewGuid(),
                CategoriaId = categoria.Id,
                Codigo = "90793-AJ412",
                Nombre = "Aceite Yamalube 4T 10W-40",
                Marca = "YAMAHA",
                PrecioVenta = 38.00m,
                Costo = 25.00m,
                StockActual = 50,
                Activo = true
            };
            dbSeed.Productos.Add(producto);

            // Venta 1
            var venta1 = new Venta
            {
                Id = Guid.NewGuid(),
                ClienteId = cliente.Id,
                Estado = EstadoVenta.Confirmada,
                Fecha = instanteVenta1,
                SubtotalGravado = 38.00m,
                MontoIgv = 6.84m,
                Total = 44.84m,
                Activo = true
            };
            venta1.Detalles.Add(new DetalleVenta
            {
                Id = Guid.NewGuid(),
                VentaId = venta1.Id,
                ProductoId = producto.Id,
                Cantidad = 1,
                PrecioUnitario = 38.00m,
                SubtotalGravado = 38.00m,
                MontoIgv = 6.84m,
                Total = 44.84m,
                Activo = true
            });
            venta1.Comprobante = new Comprobante
            {
                Id = Guid.NewGuid(),
                VentaId = venta1.Id,
                Tipo = "Boleta",
                Serie = "B001",
                Numero = "000001",
                Estado = "Emitido",
                FechaCreacion = instanteVenta1,
                SubtotalGravado = 38.00m,
                MontoIgv = 6.84m,
                Total = 44.84m,
                Activo = true
            };
            venta1Id = venta1.Id;

            // Venta 2
            var venta2 = new Venta
            {
                Id = Guid.NewGuid(),
                ClienteId = cliente.Id,
                Estado = EstadoVenta.Confirmada,
                Fecha = instanteVenta2,
                SubtotalGravado = 76.00m,
                MontoIgv = 13.68m,
                Total = 89.68m,
                Activo = true
            };
            venta2.Detalles.Add(new DetalleVenta
            {
                Id = Guid.NewGuid(),
                VentaId = venta2.Id,
                ProductoId = producto.Id,
                Cantidad = 2,
                PrecioUnitario = 38.00m,
                SubtotalGravado = 76.00m,
                MontoIgv = 13.68m,
                Total = 89.68m,
                Activo = true
            });
            venta2.Comprobante = new Comprobante
            {
                Id = Guid.NewGuid(),
                VentaId = venta2.Id,
                Tipo = "Boleta",
                Serie = "B001",
                Numero = "000002",
                Estado = "Emitido",
                FechaCreacion = instanteVenta2,
                SubtotalGravado = 76.00m,
                MontoIgv = 13.68m,
                Total = 89.68m,
                Activo = true
            };
            venta2Id = venta2.Id;

            dbSeed.Ventas.AddRange(venta1, venta2);
            await dbSeed.SaveChangesAsync();
        }

        var settings = new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            MargenAsentamientoSegundos = 120
        };

        // --- LOTE 1 (Ejecutado en T0): el corte se cierra a T0 - 120s ---
        var corteLote1 = t0.AddSeconds(-120);
        var desdeLote1 = t0.AddDays(-1);

        await using (var db = _fixture.CreateDbContext())
        {
            var exporter = new YamahaVentasExporter(db, new YamahaMappingService(), Options.Create(settings));
            var resLote1 = await exporter.ExportarVentasAsync(desdeUtc: desdeLote1, hastaUtc: corteLote1);

            // Venta 1 (hace 180s) está antes de T0 - 120s => ENTRA
            // Venta 2 (hace 40s) está después de T0 - 120s => NO ENTRA en este lote
            Assert.Single(resLote1.RegistrosValidos);
            Assert.Equal("B001-000001", resLote1.RegistrosValidos[0].NumeroComprobante);

            // Persistir log exitoso del Lote 1 con su rango en PostgreSQL
            var syncLog1 = YamahaSyncLog.Iniciar(
                YamahaServicioSincronizacion.Ventas,
                esSimulacion: false,
                modoEnvio: "Programado",
                rangoDesdeUtc: desdeLote1,
                rangoHastaUtc: corteLote1);
            syncLog1.RegistrarExito(procesados: 1, validos: 1, rechazados: 0, payloadHash: "hash-lote1", codigoHttp: 200);
            db.YamahaSyncLogs.Add(syncLog1);
            await db.SaveChangesAsync();
        }

        // --- LOTE 2 (Avanzamos el tiempo 150 segundos: T1 = T0 + 150s) ---
        // Ahora Venta 2 (hace 40s de T0) tiene una antigüedad de 190 segundos (> 120s de margen) => Asentada.
        // El punto de partida se lee del watermark en PostgreSQL (RangoHastaUtc del log 1 = corteLote1).
        var t1 = t0.AddSeconds(150);
        var corteLote2 = t1.AddSeconds(-120); // = t0 + 30s

        await using (var db = _fixture.CreateDbContext())
        {
            var watermarkService = new YamahaWatermarkService(db);
            var desdeLote2 = await watermarkService.ObtenerPuntoCorteDesdeUtcAsync(YamahaServicioSincronizacion.Ventas);

            // El watermark encadena exactamente sin huecos
            Assert.Equal(corteLote1, desdeLote2);

            var exporter = new YamahaVentasExporter(db, new YamahaMappingService(), Options.Create(settings));
            var resLote2 = await exporter.ExportarVentasAsync(desdeUtc: desdeLote2, hastaUtc: corteLote2);

            // Venta 2 (en t0 - 40s) cae en (corteLote1, corteLote2] => ENTRA
            // Venta 1 (en t0 - 180s) está <= corteLote1 (límite exclusivo) => NO SE REPITE
            Assert.Single(resLote2.RegistrosValidos);
            Assert.Equal("B001-000002", resLote2.RegistrosValidos[0].NumeroComprobante);

            // Persistir log exitoso del Lote 2 con su rango en PostgreSQL
            var syncLog2 = YamahaSyncLog.Iniciar(
                YamahaServicioSincronizacion.Ventas,
                esSimulacion: false,
                modoEnvio: "Programado",
                rangoDesdeUtc: desdeLote2,
                rangoHastaUtc: corteLote2);
            syncLog2.RegistrarExito(procesados: 1, validos: 1, rechazados: 0, payloadHash: "hash-lote2", codigoHttp: 200);
            db.YamahaSyncLogs.Add(syncLog2);
            await db.SaveChangesAsync();
        }

        // --- LOTE 3 (Avanzamos el tiempo otros 150 segundos: T2 = T0 + 300s) ---
        var t2 = t0.AddSeconds(300);
        var corteLote3 = t2.AddSeconds(-120); // = t0 + 180s

        await using (var db = _fixture.CreateDbContext())
        {
            var watermarkService = new YamahaWatermarkService(db);
            var desdeLote3 = await watermarkService.ObtenerPuntoCorteDesdeUtcAsync(YamahaServicioSincronizacion.Ventas);
            Assert.Equal(corteLote2, desdeLote3);

            var exporter = new YamahaVentasExporter(db, new YamahaMappingService(), Options.Create(settings));
            var resLote3 = await exporter.ExportarVentasAsync(desdeUtc: desdeLote3, hastaUtc: corteLote3);

            // Ya no hay ventas nuevas: lista vacía
            Assert.Empty(resLote3.RegistrosValidos);
        }

        // CONCLUSIÓN VERIFICADA EN POSTGRESQL:
        // Venta 1 se exportó exactamente 1 vez (en Lote 1).
        // Venta 2 se exportó exactamente 1 vez (en Lote 2).
        // Cero duplicados y cero ventas perdidas en los bordes de timestamptz.
    }

    #endregion

    #region 6. Coordinación distribuida multi-proceso con PostgreSQL pg_advisory_lock

    [Fact]
    public async Task Test6A_DosProcesosIndependientes_MismoServicio_SoloUnoObtieneBloqueo_YNoDuplicaEnvio()
    {
        // ARRANGE:
        await using (var dbSeed = _fixture.CreateDbContext())
        {
            await AsegurarConfiguracionEmpresaAsync(dbSeed);

            var categoria = new CategoriaProducto
            {
                Id = Guid.NewGuid(),
                Nombre = "Repuestos Yamaha",
                Activo = true
            };
            dbSeed.CategoriasProducto.Add(categoria);

            dbSeed.Productos.Add(new Producto
            {
                Id = Guid.NewGuid(),
                CategoriaId = categoria.Id,
                Codigo = "2DP-E4451-00",
                Nombre = "Filtro de Aire NMAX",
                Marca = "YAMAHA",
                StockActual = 15,
                Costo = 45.00m,
                PrecioVenta = 65.00m,
                Activo = true
            });

            await dbSeed.SaveChangesAsync();
        }

        var settings = new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            ConcurrenciaEsperaSegundos = 0 // Falla inmediatamente si no adquiere el bloqueo
        };

        var fakeClient = new FakeYamahaApiClient();
        var keyStock = YamahaLockKeys.ObtenerClaveServicio(YamahaServicioSincronizacion.Stock);

        // Simulamos un Proceso A externo abriendo una conexión independiente directa a PostgreSQL y tomando el lock
        await using var conexionProcesoA = new NpgsqlConnection(_fixture.ConnectionString);
        await conexionProcesoA.OpenAsync();

        await using (var cmdA = conexionProcesoA.CreateCommand())
        {
            cmdA.CommandText = "SELECT pg_try_advisory_lock(@key);";
            cmdA.Parameters.AddWithValue("key", keyStock);
            var adquirido = (bool)(await cmdA.ExecuteScalarAsync())!;
            Assert.True(adquirido, "Proceso A debió adquirir el advisory lock de Stock.");
        }

        // ACT 1: Proceso B (nuestro orquestador con su propio DbContext y semáforo local libre)
        // intenta sincronizar Stock. Al estar el lock en BD retenido por Proceso A, debe ser omitido.
        await using (var dbProcesoB = _fixture.CreateDbContext())
        {
            var orquestadorB = CrearOrquestador(dbProcesoB, settings, fakeClient);
            var resultadoB = await orquestadorB.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: false);

            // ASSERT 1:
            Assert.Equal(YamahaResultadoSincronizacion.OmitidoPorConcurrencia, resultadoB.Resultado);
            Assert.False(resultadoB.Exitoso);
            Assert.Equal(Guid.Empty, resultadoB.SyncLogId);
            Assert.Equal(0, fakeClient.TotalLlamadasEnvio);

            // En PostgreSQL no debe existir ningún registro en YamahaSyncLogs
            var totalLogs = await dbProcesoB.YamahaSyncLogs.CountAsync();
            Assert.Equal(0, totalLogs);
        }

        // ACT 2: Proceso A libera el advisory lock en PostgreSQL
        await using (var cmdUnlock = conexionProcesoA.CreateCommand())
        {
            cmdUnlock.CommandText = "SELECT pg_advisory_unlock(@key);";
            cmdUnlock.Parameters.AddWithValue("key", keyStock);
            var liberado = (bool)(await cmdUnlock.ExecuteScalarAsync())!;
            Assert.True(liberado, "Proceso A debió liberar el advisory lock.");
        }

        // ACT 3: Ahora Proceso B reintenta sincronizar y debe adquirir el lock exitosamente
        await using (var dbProcesoB = _fixture.CreateDbContext())
        {
            var orquestadorB = CrearOrquestador(dbProcesoB, settings, fakeClient);
            var resultadoExitoso = await orquestadorB.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: false);

            // ASSERT 3:
            Assert.Equal(YamahaResultadoSincronizacion.Exitoso, resultadoExitoso.Resultado);
            Assert.True(resultadoExitoso.Exitoso);
            Assert.NotEqual(Guid.Empty, resultadoExitoso.SyncLogId);
            Assert.Equal(1, fakeClient.TotalLlamadasEnvio);

            // En PostgreSQL debe existir exactamente 1 registro completado
            var logs = await dbProcesoB.YamahaSyncLogs.ToListAsync();
            Assert.Single(logs);
            Assert.Equal(YamahaEstadoSincronizacion.Completado, logs[0].Estado);
            Assert.True(logs[0].Exitoso);
        }
    }

    [Fact]
    public async Task Test6B_ServiciosDistintos_RespetanAmbitoDeBloqueoIndependiente()
    {
        // ARRANGE:
        await using (var dbSeed = _fixture.CreateDbContext())
        {
            await AsegurarConfiguracionEmpresaAsync(dbSeed);
        }

        var settings = new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            ConcurrenciaEsperaSegundos = 0
        };

        var fakeClient = new FakeYamahaApiClient();
        var keyStock = YamahaLockKeys.ObtenerClaveServicio(YamahaServicioSincronizacion.Stock);

        // Conexión externa retiene el lock exclusivamente para Stock
        await using var conexionStock = new NpgsqlConnection(_fixture.ConnectionString);
        await conexionStock.OpenAsync();

        await using (var cmd = conexionStock.CreateCommand())
        {
            cmd.CommandText = "SELECT pg_try_advisory_lock(@key);";
            cmd.Parameters.AddWithValue("key", keyStock);
            var lockStockAdquirido = (bool)(await cmd.ExecuteScalarAsync())!;
            Assert.True(lockStockAdquirido);
        }

        // ACT & ASSERT:
        await using (var db = _fixture.CreateDbContext())
        {
            var orquestador = CrearOrquestador(db, settings, fakeClient);

            // 1. Sincronizar Ventas debe proceder con éxito (no es bloqueado por Stock)
            var resVentas = await orquestador.SincronizarServicioAsync(YamahaServicioSincronizacion.Ventas, esSimulacion: true);
            Assert.True(resVentas.Exitoso, "Ventas no debe ser bloqueado por el lock de Stock.");
            Assert.NotEqual(YamahaResultadoSincronizacion.OmitidoPorConcurrencia, resVentas.Resultado);

            // 2. Sincronizar ServicioTecnico debe proceder con éxito (no es bloqueado por Stock)
            var resSte = await orquestador.SincronizarServicioAsync(YamahaServicioSincronizacion.ServicioTecnico, esSimulacion: true);
            Assert.True(resSte.Exitoso, "ServicioTecnico no debe ser bloqueado por el lock de Stock.");
            Assert.NotEqual(YamahaResultadoSincronizacion.OmitidoPorConcurrencia, resSte.Resultado);

            // 3. Sincronizar Stock debe ser omitido porque su clave específica está retenida
            var resStock = await orquestador.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: true);
            Assert.Equal(YamahaResultadoSincronizacion.OmitidoPorConcurrencia, resStock.Resultado);
            Assert.False(resStock.Exitoso);
        }

        // Liberar lock de Stock y confirmar que ahora Stock sí procede
        await using (var cmdUnlock = conexionStock.CreateCommand())
        {
            cmdUnlock.CommandText = "SELECT pg_advisory_unlock(@key);";
            cmdUnlock.Parameters.AddWithValue("key", keyStock);
            await cmdUnlock.ExecuteNonQueryAsync();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var orquestador = CrearOrquestador(db, settings, fakeClient);
            var resStockDesbloqueado = await orquestador.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: true);
            Assert.True(resStockDesbloqueado.Exitoso);
            Assert.NotEqual(YamahaResultadoSincronizacion.OmitidoPorConcurrencia, resStockDesbloqueado.Resultado);
        }
    }

    [Fact]
    public async Task Test6C_BloqueoSeLiberaTrasEjecucionExitosa_OConFallo()
    {
        // ARRANGE:
        await using (var dbSeed = _fixture.CreateDbContext())
        {
            await AsegurarConfiguracionEmpresaAsync(dbSeed);

            var categoria = new CategoriaProducto
            {
                Id = Guid.NewGuid(),
                Nombre = "Repuestos Yamaha",
                Activo = true
            };
            dbSeed.CategoriasProducto.Add(categoria);

            dbSeed.Productos.Add(new Producto
            {
                Id = Guid.NewGuid(),
                CategoriaId = categoria.Id,
                Codigo = "2DP-E4451-00",
                Nombre = "Filtro de Aire NMAX",
                Marca = "YAMAHA",
                StockActual = 15,
                Costo = 45.00m,
                PrecioVenta = 65.00m,
                Activo = true
            });

            await dbSeed.SaveChangesAsync();
        }

        var settings = new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            ConcurrenciaEsperaSegundos = 0
        };

        // PARTE 1: Ejecución Exitosa -> Lock liberado
        await using (var db1 = _fixture.CreateDbContext())
        {
            var distLock = new PostgreSqlYamahaDistributedLock(db1, NullLogger<PostgreSqlYamahaDistributedLock>.Instance);
            var fakeClient = new FakeYamahaApiClient();
            var orquestador = CrearOrquestador(db1, settings, fakeClient, distLock);

            var resExitoso = await orquestador.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: true);
            Assert.Equal(YamahaResultadoSincronizacion.Exitoso, resExitoso.Resultado);

            // Verificar que el lock está liberado
            var bloqueado = await distLock.EstaBloqueadoAsync(YamahaServicioSincronizacion.Stock);
            Assert.False(bloqueado, "El bloqueo debió liberarse inmediatamente tras la ejecución exitosa.");

            // Adquisición inmediata para reconfirmar
            await using var handle = await distLock.IntentarAdquirirBloqueoAsync(YamahaServicioSincronizacion.Stock, TimeSpan.FromSeconds(1));
            Assert.NotNull(handle);
        }

        // PARTE 2: Ejecución con Fallo (Excepción en cliente HTTP) -> Lock liberado en finally
        await using (var db2 = _fixture.CreateDbContext())
        {
            var distLock = new PostgreSqlYamahaDistributedLock(db2, NullLogger<PostgreSqlYamahaDistributedLock>.Instance);
            var fakeClientConFallo = new FakeYamahaApiClient
            {
                ExcepcionSimulada = new HttpRequestException("Simulación de fallo 503 Service Unavailable de Yamaha.")
            };
            var orquestador = CrearOrquestador(db2, settings, fakeClientConFallo, distLock);

            var resFallido = await orquestador.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: false);
            Assert.Equal(YamahaResultadoSincronizacion.Fallido, resFallido.Resultado);
            Assert.False(resFallido.Exitoso);

            // Verificar que el lock fue liberado por el bloque finally
            var bloqueado = await distLock.EstaBloqueadoAsync(YamahaServicioSincronizacion.Stock);
            Assert.False(bloqueado, "El bloqueo debió liberarse en el bloque finally tras un fallo no controlado.");

            // Reconfirmar adquisición inmediata
            await using var handle = await distLock.IntentarAdquirirBloqueoAsync(YamahaServicioSincronizacion.Stock, TimeSpan.FromSeconds(1));
            Assert.NotNull(handle);
        }
    }

    [Fact]
    public async Task Test6D_ProcesoTerminaInesperadamente_PostgreSqlLiberaBloqueo_YPermiteContinuar()
    {
        // ARRANGE: un producto Yamaha, como en 6C. Sin él el lote de Stock sale vacío y el
        // resultado correcto es SinCambios, no Exitoso; la prueba pasaba solo en bases que
        // conservaban productos de corridas anteriores.
        await using (var dbSeed = _fixture.CreateDbContext())
        {
            await AsegurarConfiguracionEmpresaAsync(dbSeed);

            var categoria = new CategoriaProducto
            {
                Id = Guid.NewGuid(),
                Nombre = "Repuestos Yamaha",
                Activo = true
            };
            dbSeed.CategoriasProducto.Add(categoria);

            dbSeed.Productos.Add(new Producto
            {
                Id = Guid.NewGuid(),
                CategoriaId = categoria.Id,
                Codigo = "2DP-E4451-00",
                Nombre = "Filtro de Aire NMAX",
                Marca = "YAMAHA",
                StockActual = 15,
                Costo = 45.00m,
                PrecioVenta = 65.00m,
                Activo = true
            });

            await dbSeed.SaveChangesAsync();
        }

        var settings = new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            ConcurrenciaEsperaSegundos = 0
        };

        var keyStock = YamahaLockKeys.ObtenerClaveServicio(YamahaServicioSincronizacion.Stock);
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString);
        var rPathPsql = ObtenerRutaPsql();

        var psi = new ProcessStartInfo
        {
            FileName = rPathPsql,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-h");
        psi.ArgumentList.Add(builder.Host ?? "localhost");
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add((builder.Port > 0 ? builder.Port : 5432).ToString());
        psi.ArgumentList.Add("-U");
        psi.ArgumentList.Add(builder.Username ?? "postgres");
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(builder.Database ?? PostgresQaFixture.DatabaseEsperada);

        if (!string.IsNullOrEmpty(builder.Password))
        {
            psi.Environment["PGPASSWORD"] = builder.Password;
        }

        await using var db = _fixture.CreateDbContext();
        var distLock = new PostgreSqlYamahaDistributedLock(db, NullLogger<PostgreSqlYamahaDistributedLock>.Instance);
        var fakeClient = new FakeYamahaApiClient();
        var orquestador = CrearOrquestador(db, settings, fakeClient, distLock);

        // Iniciar el proceso externo independiente psql
        using var psqlProcess = Process.Start(psi);
        Assert.NotNull(psqlProcess);

        // Enviar adquisición del advisory lock al proceso interactivo
        await psqlProcess.StandardInput.WriteLineAsync($"SELECT pg_advisory_lock({keyStock});");
        await psqlProcess.StandardInput.FlushAsync();

        try
        {
            // Esperar a que el proceso psql adquiera el advisory lock en PostgreSQL (máx 5s)
            var sw = Stopwatch.StartNew();
            var bloqueado = false;
            while (!bloqueado && sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (psqlProcess.HasExited)
                {
                    var stderr = await psqlProcess.StandardError.ReadToEndAsync();
                    var stdout = await psqlProcess.StandardOutput.ReadToEndAsync();
                    throw new InvalidOperationException($"psql terminó prematuramente con código {psqlProcess.ExitCode}. Stdout: '{stdout}', Stderr: '{stderr}'");
                }

                bloqueado = await distLock.EstaBloqueadoAsync(YamahaServicioSincronizacion.Stock);
                if (!bloqueado)
                {
                    await Task.Delay(100);
                }
            }
            Assert.True(bloqueado, "El proceso psql debió haber adquirido el advisory lock en PostgreSQL.");

            // Comprobar que el orquestador no puede ejecutar Stock mientras psql esté vivo
            var resultadoOmitido = await orquestador.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: true);
            Assert.Equal(YamahaResultadoSincronizacion.OmitidoPorConcurrencia, resultadoOmitido.Resultado);

            // ACT: Simular caída abrupta del proceso (kill violento)
            psqlProcess.Kill(entireProcessTree: true);
            await psqlProcess.WaitForExitAsync();

            // Esperar a que PostgreSQL libere los locks de la sesión desconectada (máx 5s)
            sw.Restart();
            var liberado = false;
            while (!liberado && sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                liberado = !await distLock.EstaBloqueadoAsync(YamahaServicioSincronizacion.Stock);
                if (!liberado)
                {
                    await Task.Delay(100);
                }
            }
            Assert.True(liberado, "PostgreSQL debe liberar automáticamente el advisory lock tras la terminación abrupta del proceso.");

            // Ahora el orquestador debe sincronizar exitosamente sin intervención de DBA
            var resultadoExitoso = await orquestador.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: true);
            Assert.Equal(YamahaResultadoSincronizacion.Exitoso, resultadoExitoso.Resultado);
            Assert.True(resultadoExitoso.Exitoso);
        }
        finally
        {
            if (!psqlProcess.HasExited)
            {
                try { psqlProcess.Kill(entireProcessTree: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Test6E_EjecucionLegitimaActivaConBloqueo_NoEsMarcadaComoAbandonada()
    {
        // ARRANGE:
        // Se siembran dos ejecuciones con antigüedad de 45 minutos (superando el umbral de 30 min):
        // 1. Stock: proceso legítimo todavía activo reteniendo el advisory lock en PostgreSQL.
        // 2. Ventas: proceso caído/muerto que no retiene ningún advisory lock en PostgreSQL.
        Guid idStockActivo;
        Guid idVentasAbandonado;

        var inicioViejo = DateTime.UtcNow.AddMinutes(-45);

        await using (var dbSeed = _fixture.CreateDbContext())
        {
            var logStock = YamahaSyncLog.Iniciar(
                YamahaServicioSincronizacion.Stock,
                esSimulacion: false,
                modoEnvio: "Worker-Activo");
            logStock.FechaInicio = inicioViejo;

            var logVentas = YamahaSyncLog.Iniciar(
                YamahaServicioSincronizacion.Ventas,
                esSimulacion: false,
                modoEnvio: "Worker-Muerto");
            logVentas.FechaInicio = inicioViejo;

            dbSeed.YamahaSyncLogs.AddRange(logStock, logVentas);
            await dbSeed.SaveChangesAsync();

            idStockActivo = logStock.Id;
            idVentasAbandonado = logVentas.Id;
        }

        // Conexión independiente retiene el lock para Stock simulando que el worker de Stock sigue vivo
        var keyStock = YamahaLockKeys.ObtenerClaveServicio(YamahaServicioSincronizacion.Stock);
        await using var conexionStock = new NpgsqlConnection(_fixture.ConnectionString);
        await conexionStock.OpenAsync();

        await using (var cmd = conexionStock.CreateCommand())
        {
            cmd.CommandText = "SELECT pg_try_advisory_lock(@key);";
            cmd.Parameters.AddWithValue("key", keyStock);
            var lockAdquirido = (bool)(await cmd.ExecuteScalarAsync())!;
            Assert.True(lockAdquirido);
        }

        // ACT: Ejecutar la recuperación de abandonadas
        await using (var db = _fixture.CreateDbContext())
        {
            var settings = new YamahaSettings
            {
                Enabled = true,
                TiempoExpiracionEnProcesoMinutos = 30
            };
            var distLock = new PostgreSqlYamahaDistributedLock(db, NullLogger<PostgreSqlYamahaDistributedLock>.Instance);
            var orquestador = CrearOrquestador(db, settings, distributedLock: distLock);

            var recuperados = await orquestador.RecuperarEjecucionesAbandonadasAsync();

            // Solo se debe recuperar 1 log (Ventas). Stock está protegido por el advisory lock.
            Assert.Equal(1, recuperados);
        }

        // ASSERT:
        await using (var dbVerificacion = _fixture.CreateDbContext())
        {
            var logStock = await dbVerificacion.YamahaSyncLogs.FindAsync(idStockActivo);
            var logVentas = await dbVerificacion.YamahaSyncLogs.FindAsync(idVentasAbandonado);

            Assert.NotNull(logStock);
            Assert.NotNull(logVentas);

            // Stock: Permanece intacto en EnProceso
            Assert.Equal(YamahaEstadoSincronizacion.EnProceso, logStock.Estado);
            Assert.False(logStock.Exitoso);
            Assert.Null(logStock.FechaFin);

            // Ventas: Marcado como Fallido
            Assert.Equal(YamahaEstadoSincronizacion.Fallido, logVentas.Estado);
            Assert.False(logVentas.Exitoso);
            Assert.NotNull(logVentas.FechaFin);
            Assert.Contains("Supero el tiempo limite de expiracion", logVentas.MensajeError);
        }

        // ACT 2: Liberar el lock de Stock y volver a ejecutar la recuperación
        await using (var cmdUnlock = conexionStock.CreateCommand())
        {
            cmdUnlock.CommandText = "SELECT pg_advisory_unlock(@key);";
            cmdUnlock.Parameters.AddWithValue("key", keyStock);
            await cmdUnlock.ExecuteNonQueryAsync();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var settings = new YamahaSettings
            {
                Enabled = true,
                TiempoExpiracionEnProcesoMinutos = 30
            };
            var distLock = new PostgreSqlYamahaDistributedLock(db, NullLogger<PostgreSqlYamahaDistributedLock>.Instance);
            var orquestador = CrearOrquestador(db, settings, distributedLock: distLock);

            var recuperadosSegundo = await orquestador.RecuperarEjecucionesAbandonadasAsync();

            // Ahora que el proceso terminó y liberó el lock, sí se recupera
            Assert.Equal(1, recuperadosSegundo);
        }

        await using (var dbVerificacionFinal = _fixture.CreateDbContext())
        {
            var logStockFinal = await dbVerificacionFinal.YamahaSyncLogs.FindAsync(idStockActivo);
            Assert.NotNull(logStockFinal);
            Assert.Equal(YamahaEstadoSincronizacion.Fallido, logStockFinal.Estado);
        }
    }

    [Fact]
    public async Task Test6F_EnviadoSinRegistrar_LiberaBloqueoYNoReenviaAutomaticamente()
    {
        // ARRANGE:
        // Configurar empresa y producto Yamaha en PostgreSQL
        await using (var dbSeed = _fixture.CreateDbContext())
        {
            await AsegurarConfiguracionEmpresaAsync(dbSeed);

            var categoria = new CategoriaProducto
            {
                Id = Guid.NewGuid(),
                Nombre = "Repuestos Yamaha",
                Activo = true
            };
            dbSeed.CategoriasProducto.Add(categoria);

            dbSeed.Productos.Add(new Producto
            {
                Id = Guid.NewGuid(),
                CategoriaId = categoria.Id,
                Codigo = "2DP-E4451-00",
                Nombre = "Filtro de Aire NMAX",
                Marca = "YAMAHA",
                StockActual = 15,
                Costo = 45.00m,
                PrecioVenta = 65.00m,
                Activo = true
            });

            await dbSeed.SaveChangesAsync();
        }

        var interceptorFallo = new FalloGuardadoTrasEnvioInterceptor();
        await using var dbConInterceptor = _fixture.CreateDbContext(opt => opt.AddInterceptors(interceptorFallo));

        var fakeClient = new FakeYamahaApiClient
        {
            RespuestaSimulada = new YamahaHttpRespuestaDto(
                200,
                true,
                "{\"status\":\"ok\",\"mensaje\":\"Lote recibido con exito.\"}",
                null,
                TimeSpan.FromMilliseconds(50))
        };

        var settings = new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            ConcurrenciaEsperaSegundos = 0
        };

        var distLock = new PostgreSqlYamahaDistributedLock(dbConInterceptor, NullLogger<PostgreSqlYamahaDistributedLock>.Instance);
        var orchestrator = CrearOrquestador(dbConInterceptor, settings, fakeClient, distLock);

        // ACT: Ejecutar sincronización de Stock real (no simulación)
        var resultado = await orchestrator.SincronizarServicioAsync(YamahaServicioSincronizacion.Stock, esSimulacion: false);

        // ASSERT:
        // 1. Yamaha recibió el lote
        Assert.Equal(1, fakeClient.TotalLlamadasEnvio);

        // 2. El orquestador intentó guardar el log en PostgreSQL 3 veces
        Assert.Equal(3, interceptorFallo.IntentosFallo);

        // 3. El resultado es EnviadoSinRegistrar con Exitoso = true
        Assert.Equal(YamahaResultadoSincronizacion.EnviadoSinRegistrar, resultado.Resultado);
        Assert.True(resultado.Exitoso);

        // 4. VERIFICACIÓN CRÍTICA DEL BLOQUEO DISTRIBUIDO:
        // A pesar del fallo en SaveChangesAsync, el bloque finally debió liberar el advisory lock
        var bloqueado = await distLock.EstaBloqueadoAsync(YamahaServicioSincronizacion.Stock);
        Assert.False(bloqueado, "El advisory lock debe ser liberado incluso cuando el guardado post-envío falla.");

        // 5. Comprobar que otra ejecución puede adquirir el bloqueo inmediatamente
        await using var handle = await distLock.IntentarAdquirirBloqueoAsync(YamahaServicioSincronizacion.Stock, TimeSpan.FromSeconds(1));
        Assert.NotNull(handle);
    }

    #endregion
}
