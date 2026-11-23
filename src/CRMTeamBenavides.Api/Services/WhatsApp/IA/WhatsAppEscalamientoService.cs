using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRMTeamBenavides.Api.Services.WhatsApp.IA;

public sealed record ResultadoEscalamientoWhatsApp(
    bool Escalada,
    bool YaEstabaEnModoHumano,
    string MotivoCodigo);

/// <summary>
/// Gestiona la derivación controlada de una conversación de WhatsApp desde modo Bot hacia atención humana.
/// Cambia el estado de la conversación para impedir futuras respuestas automáticas y registra auditoría sin PII.
/// </summary>
public interface IWhatsAppEscalamientoService
{
    Task<ResultadoEscalamientoWhatsApp> EscalarAHumanoAsync(
        Guid conversacionId,
        string motivoCodigo,
        CancellationToken ct = default);

    /// <summary>
    /// Encola el evento <c>WHATSAPP_ESCALAR_HUMANO</c> dentro de la unidad de trabajo activa
    /// sin llamar a <c>SaveChangesAsync</c>, para que se confirme o revierta atómicamente junto con
    /// el escalamiento, mensaje saliente y Outbox (F3-M03).
    /// </summary>
    void EncolarAuditoriaEscalamientoEnTransaccion(
        Guid conversacionId,
        string motivoCodigo,
        ModoAtencionWhatsApp modoAnterior,
        EstadoConversacionWhatsApp estadoAnterior,
        DateTime fechaUtc);

    /// <summary>
    /// Emite la traza técnica sin PII una vez confirmada la transacción de escalamiento.
    /// </summary>
    void RegistrarTrazaEscalamiento(Guid conversacionId, string motivoCodigo);
}

public sealed class WhatsAppEscalamientoService : IWhatsAppEscalamientoService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<WhatsAppEscalamientoService> _logger;

    public WhatsAppEscalamientoService(
        ApplicationDbContext context,
        IAuditoriaService auditoria,
        ILogger<WhatsAppEscalamientoService> logger)
    {
        _context = context;
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task<ResultadoEscalamientoWhatsApp> EscalarAHumanoAsync(
        Guid conversacionId,
        string motivoCodigo,
        CancellationToken ct = default)
    {
        var conversacion = await _context.ConversacionesWhatsApp
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.Activo, ct);

        if (conversacion is null)
        {
            return new ResultadoEscalamientoWhatsApp(false, false, "CONVERSACION_NO_ENCONTRADA");
        }

        // F3-M04: jamás reabrir conversaciones cerradas ni modificar FechaCambioModoHumano/auditar sin cambio real.
        if (conversacion.Estado == EstadoConversacionWhatsApp.Cerrada)
        {
            return new ResultadoEscalamientoWhatsApp(
                Escalada: false,
                YaEstabaEnModoHumano: conversacion.ModoAtencion == ModoAtencionWhatsApp.Humano,
                MotivoCodigo: "CONVERSACION_CERRADA");
        }

        if (conversacion.ModoAtencion == ModoAtencionWhatsApp.Humano)
        {
            return new ResultadoEscalamientoWhatsApp(false, true, motivoCodigo);
        }

        var modoAnterior = conversacion.ModoAtencion;
        var estadoAnterior = conversacion.Estado;
        var ahora = DateTime.UtcNow;

        conversacion.ModoAtencion = ModoAtencionWhatsApp.Humano;
        conversacion.Estado = EstadoConversacionWhatsApp.EnAtencion;
        conversacion.FechaCambioModoHumano = ahora;
        conversacion.FechaModificacion = ahora;

        EncolarAuditoriaEscalamientoEnTransaccion(
            conversacion.Id,
            motivoCodigo,
            modoAnterior,
            estadoAnterior,
            ahora);

        await _context.SaveChangesAsync(ct);

        RegistrarTrazaEscalamiento(conversacion.Id, motivoCodigo);

        return new ResultadoEscalamientoWhatsApp(true, false, motivoCodigo);
    }

    public void EncolarAuditoriaEscalamientoEnTransaccion(
        Guid conversacionId,
        string motivoCodigo,
        ModoAtencionWhatsApp modoAnterior,
        EstadoConversacionWhatsApp estadoAnterior,
        DateTime fechaUtc)
    {
        _auditoria.EncolarEventoEnTransaccion(
            usuarioId: null,
            accion: "WHATSAPP_ESCALAR_HUMANO",
            entidad: "ConversacionWhatsApp",
            entidadId: conversacionId.ToString(),
            detalle: new
            {
                MotivoCodigo = motivoCodigo,
                ModoAnterior = modoAnterior.ToString(),
                EstadoAnterior = estadoAnterior.ToString(),
                ModoNuevo = ModoAtencionWhatsApp.Humano.ToString(),
                EstadoNuevo = EstadoConversacionWhatsApp.EnAtencion.ToString()
            },
            fechaUtc: fechaUtc);
    }

    public void RegistrarTrazaEscalamiento(Guid conversacionId, string motivoCodigo)
    {
        _logger.LogInformation(
            "WhatsApp IA escaló conversación a atención humana. ConversacionId={ConversacionId}, Motivo={Motivo}",
            conversacionId,
            motivoCodigo);
    }
}
