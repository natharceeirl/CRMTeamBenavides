using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.WhatsApp;

// =============================================================================
// CONTRATOS ADMINISTRATIVOS DEL MÓDULO WHATSAPP (CRM)
// =============================================================================

public record WhatsAppConversacionResumenResponse(
    Guid Id,
    string TelefonoE164,
    string? WhatsAppId,
    string? NombrePerfilWhatsApp,
    Guid? ClienteId,
    string? ClienteNombre,
    bool AsociacionClienteAmbigua,
    EstadoConversacionWhatsApp Estado,
    ModoAtencionWhatsApp ModoAtencion,
    Guid? AgenteAsignadoId,
    string? AgenteAsignadoNombre,
    DateTime FechaCreacion,
    DateTime? FechaUltimoMensaje,
    DateTime? FechaUltimoMensajeEntrante,
    DateTime? FechaModificacion,
    string? UltimoMensajePreview);

public record WhatsAppConversacionDetalleResponse(
    Guid Id,
    string TelefonoE164,
    string? WhatsAppId,
    string? NombrePerfilWhatsApp,
    Guid? ClienteId,
    string? ClienteNombre,
    string? ClienteDocumento,
    bool AsociacionClienteAmbigua,
    EstadoConversacionWhatsApp Estado,
    ModoAtencionWhatsApp ModoAtencion,
    Guid? AgenteAsignadoId,
    string? AgenteAsignadoNombre,
    DateTime FechaCreacion,
    DateTime? FechaUltimoMensaje,
    DateTime? FechaUltimoMensajeEntrante,
    DateTime? FechaCambioModoHumano,
    DateTime? FechaModificacion,
    int TotalMensajes);

public record WhatsAppConversacionesPaginadasResponse(
    List<WhatsAppConversacionResumenResponse> Items,
    int TotalRegistros,
    int Pagina,
    int TamanoPagina);

public record WhatsAppMensajeResponse(
    Guid Id,
    Guid ConversacionId,
    string? ExternalMessageId,
    string? IdempotencyKey,
    DireccionMensajeWhatsApp Direccion,
    TipoMensajeWhatsApp Tipo,
    string? ContenidoTexto,
    EstadoMensajeWhatsApp Estado,
    DateTime FechaEventoUtc,
    DateTime FechaRecepcionUtc,
    DateTime? FechaUltimoEstadoUtc,
    Guid? EnviadoPorUsuarioId,
    string? EnviadoPorUsuarioNombre,
    string? MediaId,
    string? MediaMimeType,
    string? MediaNombreArchivo,
    string? MetadataTecnicaJson,
    string? ErrorDetalle);

public record WhatsAppMensajesPaginadosResponse(
    List<WhatsAppMensajeResponse> Items,
    int TotalRegistros,
    int Pagina,
    int TamanoPagina);

public record WhatsAppAsignarAgenteRequest(
    Guid? AgenteId);

public record WhatsAppCambiarModoRequest(
    ModoAtencionWhatsApp ModoAtencion);

public record WhatsAppCambiarEstadoRequest(
    EstadoConversacionWhatsApp Estado);

public record WhatsAppVincularClienteRequest(
    Guid? ClienteId);

public record WhatsAppEnviarMensajeRequest(
    string Texto,
    string? IdempotencyKey = null);

public record WhatsAppEstadoIntegracionResponse(
    bool Enabled,
    string Provider,
    string GraphApiVersion,
    bool TieneCredencialesMetaConfiguradas,
    bool TieneSeguridadWebhookConfigurada);
