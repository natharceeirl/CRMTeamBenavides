using CRMTeamBenavides.Api.Features.WhatsApp;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.WhatsApp.IA;

/// <summary>
/// Adaptador del punto de extensión <see cref="IWhatsAppAutoResponder"/> (Fase 1) hacia
/// el orquestador conversacional de Fase 3.
/// Garantiza que:
/// - Cuando <c>WhatsAppAi.Enabled == false</c> (por defecto), actúa como no-op sin afectar Fase 1 ni Fase 2.
/// - Ninguna excepción del pipeline de IA bloquea ni hace fallar la recepción del webhook de Meta.
/// </summary>
public sealed class WhatsAppAiAutoResponder : IWhatsAppAutoResponder
{
    private readonly IWhatsAppConversacionOrquestador _orquestador;
    private readonly IOptionsMonitor<WhatsAppAiSettings> _aiSettingsMonitor;
    private readonly IOptionsMonitor<WhatsAppSettings> _waSettingsMonitor;
    private readonly ILogger<WhatsAppAiAutoResponder> _logger;

    public WhatsAppAiAutoResponder(
        IWhatsAppConversacionOrquestador orquestador,
        IOptionsMonitor<WhatsAppAiSettings> aiSettingsMonitor,
        IOptionsMonitor<WhatsAppSettings> waSettingsMonitor,
        ILogger<WhatsAppAiAutoResponder> logger)
    {
        _orquestador = orquestador;
        _aiSettingsMonitor = aiSettingsMonitor;
        _waSettingsMonitor = waSettingsMonitor;
        _logger = logger;
    }

    public async Task ProcesarMensajeEntranteAsync(
        ConversacionWhatsApp conversacion,
        MensajeWhatsApp mensaje,
        CancellationToken ct = default)
    {
        if (!_aiSettingsMonitor.CurrentValue.Enabled
            || _waSettingsMonitor.CurrentValue.ValidarConfiguracionEnvio() is not null)
        {
            return;
        }

        try
        {
            await _orquestador.ProcesarMensajeEntranteAsync(conversacion.Id, mensaje.Id, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancelación externa del request/shutdown: no escalar ni escribir estado parcial.
        }
        catch (Exception)
        {
            // Jamás propagar excepciones de IA hacia el webhook de Meta (sin registrar PII ni secretos).
            _logger.LogWarning(
                "WhatsApp IA auto-responder capturó un error no fatal al procesar mensaje entrante. ConversacionId={ConversacionId}, MensajeId={MensajeId}",
                conversacion.Id,
                mensaje.Id);
        }
    }
}
