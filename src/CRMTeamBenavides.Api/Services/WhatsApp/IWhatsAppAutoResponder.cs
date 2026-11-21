using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

/// <summary>
/// Punto de extensión para procesamiento automático de mensajes entrantes (Fase 2: IA y flujos CRM).
/// Solo se invoca cuando la conversación se encuentra en <see cref="ModoAtencionWhatsApp.Bot"/>.
/// Cuando un asesor toma el control (<see cref="ModoAtencionWhatsApp.Humano"/>), jamás es invocado.
/// </summary>
public interface IWhatsAppAutoResponder
{
    Task ProcesarMensajeEntranteAsync(
        ConversacionWhatsApp conversacion,
        MensajeWhatsApp mensaje,
        CancellationToken ct = default);
}

/// <summary>
/// Implementación pasiva de Fase 1: registra la elegibilidad de automatización sin enviar respuestas
/// automáticas hasta que se habilite la Fase 2 (chatbot conversacional conectado al CRM).
/// </summary>
public sealed class NoOpWhatsAppAutoResponder : IWhatsAppAutoResponder
{
    public Task ProcesarMensajeEntranteAsync(
        ConversacionWhatsApp conversacion,
        MensajeWhatsApp mensaje,
        CancellationToken ct = default) => Task.CompletedTask;
}
