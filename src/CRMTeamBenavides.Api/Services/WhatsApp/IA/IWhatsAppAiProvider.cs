using CRMTeamBenavides.Api.Features.WhatsApp;

namespace CRMTeamBenavides.Api.Services.WhatsApp.IA;

public sealed record WhatsAppAiMensajeHistorial(
    string Rol,
    string TextoSanitizado,
    DateTime FechaUtc);

/// <summary>
/// Contexto estrictamente autorizado y libre de datos sensibles que se entrega al proveedor de IA.
/// Nunca contiene teléfonos de clientes, documentos, costos, márgenes ni datos privados sin verificación.
/// </summary>
public sealed record WhatsAppAiContextoAutorizado(
    bool IdentidadVerificada,
    bool ConsultaPersonalBloqueadaPorFaltaVerificacion,
    WhatsAppInfoTallerPublica? InformacionTaller,
    IReadOnlyList<WhatsAppFaqAutorizada> FaqsCoincidentes,
    IReadOnlyList<WhatsAppProductoCatalogoResponse> ProductosPublicos,
    IReadOnlyList<WhatsAppServicioCatalogoResponse> ServiciosPublicos);

public sealed record WhatsAppAiSolicitud(
    Guid ConversacionId,
    string MensajeUsuarioNoConfiable,
    ResultadoClasificacionIntencion Clasificacion,
    IReadOnlyList<WhatsAppAiMensajeHistorial> HistorialReciente,
    WhatsAppAiContextoAutorizado ContextoAutorizado,
    int MaxOutputTokens,
    int MaxOutputChars);

public sealed record WhatsAppAiRespuesta(
    bool Exitoso,
    string? TextoRespuesta,
    bool SugiereEscalamientoHumano,
    string Proveedor,
    string Modelo,
    int TokensEstimadosEntrada,
    int TokensEstimadosSalida,
    string? CodigoError = null,
    string? ErrorSanitizado = null,
    int? TokensEntradaReales = null,
    int? TokensSalidaReales = null,
    int? MaxOutputTokensSolicitado = null);

/// <summary>
/// Abstracción desacoplada para proveedores de IA conversacional (Mock determinista o futuro LLM externo).
/// Las implementaciones deben tratar siempre <see cref="WhatsAppAiSolicitud.MensajeUsuarioNoConfiable"/>
/// como datos no confiables incapaces de modificar reglas, herramientas o permisos.
/// </summary>
public interface IWhatsAppAiProvider
{
    string NombreProveedor { get; }

    Task<WhatsAppAiRespuesta> GenerarRespuestaAsync(
        WhatsAppAiSolicitud solicitud,
        CancellationToken ct = default);
}
