using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Servicio de fondo (BackgroundService) para la ejecución programada de sincronizaciones con Yamaha v3.
/// Se mantiene ESTRICTAMENTE DESACTIVADO por defecto mientras YamahaSettings.Enabled == false.
/// Incluye control de concurrencia mediante SemaphoreSlim para prevenir ejecuciones superpuestas
/// y respeta la cancelación cooperativa durante el apagado de la aplicación.
/// </summary>
public sealed class YamahaSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<YamahaSettings> _settingsMonitor;
    private readonly ILogger<YamahaSyncBackgroundService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Intervalo de evaluación de agenda cuando está desactivado (ej. 30 minutos)
    private readonly TimeSpan _intervaloInactivo = TimeSpan.FromMinutes(30);

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

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = _settingsMonitor.CurrentValue;

            if (!settings.Enabled)
            {
                _logger.LogDebug(
                    "YamahaSyncBackgroundService: Integración desactivada (YamahaSettings.Enabled == false). " +
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

            // Cuando esté formalmente habilitado en producción
            await EjecutarCicloProgramadoAsync(stoppingToken);

            try
            {
                // Espera hasta el siguiente turno de 4 horas
                await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("YamahaSyncBackgroundService detenido por señal de cancelación.");
    }

    /// <summary>
    /// Ejecuta un ciclo de sincronización programado garantizando exclusión mutua.
    /// </summary>
    public async Task EjecutarCicloProgramadoAsync(CancellationToken cancellationToken = default)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            _logger.LogWarning("YamahaSyncBackgroundService: Se omitió el ciclo programado porque ya existe una sincronización en curso.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IYamahaSyncOrchestrator>();

            _logger.LogInformation("Iniciando ciclo programado de sincronizaciones con Yamaha v3...");

            // Ventas
            var resVentas = await orchestrator.SincronizarServicioAsync(
                YamahaServicioSincronizacion.Ventas,
                esSimulacion: false,
                modoEnvio: "Programado",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Sincronización programada Ventas: Exitoso={Exitoso}, Validos={Validos}, Rechazados={Rechazados}",
                resVentas.Exitoso,
                resVentas.TotalValidos,
                resVentas.TotalRechazados);

            // Servicio Técnico
            var resSte = await orchestrator.SincronizarServicioAsync(
                YamahaServicioSincronizacion.ServicioTecnico,
                esSimulacion: false,
                modoEnvio: "Programado",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Sincronización programada Servicio Técnico: Exitoso={Exitoso}, Validos={Validos}, Rechazados={Rechazados}",
                resSte.Exitoso,
                resSte.TotalValidos,
                resSte.TotalRechazados);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Ciclo de sincronización cancelado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el ciclo de sincronización en segundo plano de Yamaha.");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
