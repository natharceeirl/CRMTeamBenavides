using CRMTeamBenavides.Api.Features.WhatsApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

/// <summary>
/// Worker en segundo plano para procesar reintentos pendientes de <c>OutboxMensajesWhatsApp</c>.
/// Permanece inactivo mientras <c>WhatsApp:Enabled</c> sea <c>false</c>.
/// </summary>
public sealed class WhatsAppOutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<WhatsAppSettings> _settingsMonitor;
    private readonly ILogger<WhatsAppOutboxBackgroundService> _logger;

    public WhatsAppOutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<WhatsAppSettings> settingsMonitor,
        ILogger<WhatsAppOutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _settingsMonitor = settingsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = _settingsMonitor.CurrentValue;
            if (settings.Enabled)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var outboxService = scope.ServiceProvider.GetRequiredService<IWhatsAppOutboxService>();
                    while (!stoppingToken.IsCancellationRequested &&
                           await outboxService.ProcesarLotePendienteAsync(stoppingToken) > 0)
                    {
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "WhatsApp Outbox: falló una vuelta de despacho; se reintentará en el siguiente ciclo.");
                }
            }

            var intervalo = Math.Clamp(settings.OutboxPollingIntervalSeconds, 2, 300);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalo), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
