using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Servicio de fondo (BackgroundService) para la ejecucion programada de sincronizaciones con Yamaha v3.
/// Reglas operativas:
/// 1. Permanece ESTRICTAMENTE DESACTIVADO mientras YamahaSettings.Enabled == false.
/// 2. Ventas y Servicio Tecnico se ejecutan periodicamente cada 4 horas (Seccion 3.1).
/// 3. Stock se ejecuta diariamente al cierre de operaciones (hora local de Peru America/Lima, configurable).
/// 4. Control de concurrencia mediante SemaphoreSlim para prevenir ejecuciones superpuestas.
/// 5. Recuperacion segura de ejecuciones 'EnProceso' abandonadas (mayores a 30 min) al inicio y periodicamente.
/// 6. Prevencion de duplicados: verifica en base de datos si el cierre de Stock de hoy ya se ejecuto.
/// </summary>
public sealed class YamahaSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<YamahaSettings> _settingsMonitor;
    private readonly ILogger<YamahaSyncBackgroundService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly TimeSpan _intervaloInactivo = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _intervaloEvaluacionActivo = TimeSpan.FromMinutes(1);

    private DateTime? _ultimaEjecucionVentasSteUtc;
    private DateOnly? _ultimoDiaStockEjecutadoPeru;
    private DateTime? _ultimaRecuperacionAbandonadasUtc;

    public YamahaSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<YamahaSettings> settingsMonitor,
        ILogger<YamahaSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _settingsMonitor = settingsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("YamahaSyncBackgroundService iniciado. Estado Enabled: {Enabled}", _settingsMonitor.CurrentValue.Enabled);

        // Recuperacion inicial de posibles ejecuciones abandonadas por caidas previas
        await IntentarRecuperarAbandonadasAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = _settingsMonitor.CurrentValue;

            if (!settings.Enabled)
            {
                _logger.LogDebug(
                    "YamahaSyncBackgroundService: Integracion desactivada (YamahaSettings.Enabled == false). " +
                    "Permaneciendo en reposo.");

                try
                {
                    await Task.Delay(_intervaloInactivo, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                continue;
            }

            // Cuando este formalmente habilitado en produccion, evaluar agenda cada minuto
            try
            {
                await EvaluarYSincronizarSegunAgendaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado durante la evaluacion de agenda en YamahaSyncBackgroundService.");
            }

            try
            {
                await Task.Delay(_intervaloEvaluacionActivo, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("YamahaSyncBackgroundService detenido por senal de cancelacion.");
    }

    /// <summary>
    /// Evalua la agenda de sincronizaciones y ejecuta los servicios que correspondan segun el tiempo transcurrido
    /// y la hora local de Peru.
    /// </summary>
    public async Task EvaluarYSincronizarSegunAgendaAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsMonitor.CurrentValue;
        if (!settings.Enabled)
        {
            return;
        }

        // 1. Recuperacion periodica de abandonadas (cada 1 hora)
        if (!_ultimaRecuperacionAbandonadasUtc.HasValue ||
            DateTime.UtcNow - _ultimaRecuperacionAbandonadasUtc.Value >= TimeSpan.FromHours(1))
        {
            await IntentarRecuperarAbandonadasAsync(cancellationToken);
            _ultimaRecuperacionAbandonadasUtc = DateTime.UtcNow;
        }

        // 2. Evaluacion de Ventas y Servicio Tecnico (cada 4 horas)
        var intervaloHoras = settings.IntervaloVentasServicioTecnicoHoras > 0
            ? settings.IntervaloVentasServicioTecnicoHoras
            : 4;

        if (!_ultimaEjecucionVentasSteUtc.HasValue ||
            DateTime.UtcNow - _ultimaEjecucionVentasSteUtc.Value >= TimeSpan.FromHours(intervaloHoras))
        {
            await EjecutarCicloProgramadoAsync(cancellationToken);
            _ultimaEjecucionVentasSteUtc = DateTime.UtcNow;
        }

        // 3. Evaluacion diaria de Stock (cierre de operaciones hora Peru)
        var ahoraPeru = HoraPeru.DesdeUtc(DateTime.UtcNow);
        var hoyPeru = DateOnly.FromDateTime(ahoraPeru);
        var horaActualPeru = TimeOnly.FromDateTime(ahoraPeru);
        var horaCierre = settings.ObtenerHoraCierreStock();

        if (horaActualPeru >= horaCierre && _ultimoDiaStockEjecutadoPeru != hoyPeru)
        {
            var yaEjecutado = await YaSeEjecutoStockCierreHoyAsync(hoyPeru, horaCierre, cancellationToken);
            if (!yaEjecutado)
            {
                await EjecutarStockProgramadoAsync(cancellationToken);
            }
            _ultimoDiaStockEjecutadoPeru = hoyPeru;
        }
    }

    /// <summary>
    /// Ejecuta el ciclo programado de Ventas y Servicio Tecnico garantizando exclusion mutua.
    /// </summary>
    public async Task EjecutarCicloProgramadoAsync(CancellationToken cancellationToken = default)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            _logger.LogWarning("YamahaSyncBackgroundService: Se omitio el ciclo programado de Ventas/STE porque ya existe una sincronizacion en curso.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IYamahaSyncOrchestrator>();

            _logger.LogInformation("Iniciando ciclo programado de Ventas y Servicio Tecnico con Yamaha v3...");

            // Ventas
            var resVentas = await orchestrator.SincronizarServicioAsync(
                YamahaServicioSincronizacion.Ventas,
                esSimulacion: false,
                modoEnvio: "Programado",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Sincronizacion programada Ventas: Exitoso={Exitoso}, Validos={Validos}, Rechazados={Rechazados}",
                resVentas.Exitoso,
                resVentas.TotalValidos,
                resVentas.TotalRechazados);

            // Servicio Tecnico
            var resSte = await orchestrator.SincronizarServicioAsync(
                YamahaServicioSincronizacion.ServicioTecnico,
                esSimulacion: false,
                modoEnvio: "Programado",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Sincronizacion programada Servicio Tecnico: Exitoso={Exitoso}, Validos={Validos}, Rechazados={Rechazados}",
                resSte.Exitoso,
                resSte.TotalValidos,
                resSte.TotalRechazados);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Ciclo de sincronizacion cancelado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el ciclo de sincronizacion de Ventas/STE en segundo plano.");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Ejecuta el envio diario de Stock garantizando exclusion mutua.
    /// </summary>
    public async Task EjecutarStockProgramadoAsync(CancellationToken cancellationToken = default)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            _logger.LogWarning("YamahaSyncBackgroundService: Se omitio el envio de Stock porque ya existe una sincronizacion en curso.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IYamahaSyncOrchestrator>();

            _logger.LogInformation("Iniciando envio diario programado de Stock con Yamaha v3...");

            var resStock = await orchestrator.SincronizarServicioAsync(
                YamahaServicioSincronizacion.Stock,
                esSimulacion: false,
                modoEnvio: "Programado",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Sincronizacion programada Stock: Exitoso={Exitoso}, Validos={Validos}, Rechazados={Rechazados}",
                resStock.Exitoso,
                resStock.TotalValidos,
                resStock.TotalRechazados);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Envio de Stock cancelado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el envio diario de Stock en segundo plano.");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Comprueba si ya existe un registro exitoso no simulado de Stock para el dia actual de Peru
    /// ejecutado a partir de la hora de cierre.
    /// </summary>
    public async Task<bool> YaSeEjecutoStockCierreHoyAsync(
        DateOnly hoyPeru,
        TimeOnly horaCierre,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var inicioDiaUtc = HoraPeru.InicioDelDiaUtc(hoyPeru.ToDateTime(TimeOnly.MinValue));
            var horaCierreUtcHoy = inicioDiaUtc.Add(horaCierre.ToTimeSpan());

            return await dbContext.YamahaSyncLogs
                .AsNoTracking()
                .AnyAsync(l => l.Servicio == YamahaServicioSincronizacion.Stock &&
                               l.Exitoso &&
                               !l.EsSimulacion &&
                               l.FechaInicio >= horaCierreUtcHoy, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar si Stock ya fue ejecutado hoy en YamahaSyncLogs.");
            return false;
        }
    }

    private async Task IntentarRecuperarAbandonadasAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IYamahaSyncOrchestrator>();
            await orchestrator.RecuperarEjecucionesAbandonadasAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Apagado normal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al recuperar ejecuciones abandonadas en YamahaSyncBackgroundService.");
        }
    }
}
