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

// =============================================================================
// CONTRATOS FASE 2 — IDENTIFICACIÓN, VINCULACIÓN Y CONSULTA CONTROLADA DEL CRM
// =============================================================================

public record WhatsAppClienteVinculadoBasicoResponse(
    Guid Id,
    string NombreCompleto,
    string? RazonSocial,
    TipoDocumentoCliente? TipoDocumento,
    string? DocumentoVisible,
    string? Telefono,
    int CantidadVehiculosActivos);

public record WhatsAppVinculacionClienteResponse(
    Guid ConversacionId,
    string TelefonoE164,
    bool EstaVinculada,
    bool AsociacionClienteAmbigua,
    bool ClienteVinculadoInactivo,
    bool? TelefonoCoincideConCliente,
    bool RequiereRevisionVinculacion,
    string OrigenVinculacion,
    WhatsAppClienteVinculadoBasicoResponse? Cliente);

public record WhatsAppClienteCandidatoResponse(
    Guid Id,
    string NombreCompleto,
    string? RazonSocial,
    TipoDocumentoCliente? TipoDocumento,
    string? DocumentoEnmascarado,
    string? TelefonoEnmascarado,
    int CantidadVehiculosActivos);

public record WhatsAppClientesCandidatosPaginadosResponse(
    List<WhatsAppClienteCandidatoResponse> Items,
    int TotalRegistros,
    int Pagina,
    int TamanoPagina);

public record WhatsAppResultadoIdentificacionResponse(
    Guid ConversacionId,
    string EstadoIdentificacion,
    bool VinculadaAutomaticamente,
    bool AsociacionAmbigua,
    Guid? ClienteId,
    int TotalCandidatos,
    string Motivo);

public record WhatsAppPerfilClienteResponse(
    Guid ClienteId,
    string NombreCompleto,
    string? RazonSocial,
    TipoDocumentoCliente? TipoDocumento,
    string? DocumentoVisible,
    List<string> TelefonosE164,
    bool TieneEmail,
    int CantidadVehiculosActivos,
    int CantidadOrdenesActivas,
    int CantidadCitasActivas);

public record WhatsAppVehiculoClienteResponse(
    Guid Id,
    TipoUnidad TipoUnidad,
    string Marca,
    string Modelo,
    int? Anio,
    string? Placa,
    string? Color,
    TipoMedidor TipoMedidor,
    decimal? LecturaMedidorActual,
    string? SerieIdentificacionParcial);

public record WhatsAppTrabajoOrdenResponse(
    TipoItemServicio TipoItem,
    string Descripcion,
    int Cantidad);

public record WhatsAppOrdenServicioResumenResponse(
    Guid Id,
    string? NumeroOrden,
    DateTime FechaIngreso,
    DateTime? FechaEstimadaEntrega,
    DateTime? FechaSalida,
    EstadoOrdenServicio Estado,
    string EstadoEntrega,
    Guid VehiculoId,
    string VehiculoResumen,
    TipoAtencion TipoAtencionPrincipal,
    List<TipoAtencion> TiposAtencion,
    string? ResumenTrabajos);

public record WhatsAppOrdenServicioDetalleResponse(
    Guid Id,
    string? NumeroOrden,
    DateTime FechaApertura,
    DateTime FechaIngreso,
    DateTime? FechaEstimadaEntrega,
    DateTime? FechaSalida,
    DateTime? FechaCierre,
    EstadoOrdenServicio Estado,
    string EstadoEntrega,
    EstadoPresupuestoCliente EstadoPresupuestoCliente,
    Guid VehiculoId,
    string VehiculoResumen,
    string? VehiculoPlaca,
    TipoAtencion TipoAtencionPrincipal,
    List<TipoAtencion> TiposAtencion,
    ModalidadAtencion ModalidadAtencion,
    string? MotivoFalla,
    string? DiagnosticoResumen,
    string? SolucionResumen,
    List<WhatsAppTrabajoOrdenResponse> Trabajos,
    decimal Total);

public record WhatsAppCitaClienteResponse(
    Guid Id,
    string? NumeroCita,
    DateTime FechaHoraProgramada,
    int DuracionMinutos,
    EstadoCita Estado,
    string Motivo,
    Guid? VehiculoId,
    string? VehiculoResumen,
    Guid? OrdenServicioId);

public record WhatsAppProductoCatalogoResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Marca,
    string? CategoriaNombre,
    TipoProducto Tipo,
    string Unidad,
    bool Disponible,
    int? StockDisponible,
    bool PrecioDisponible,
    decimal? PrecioBaseSinIgvSoles,
    decimal? PrecioReferencialConIgvSoles,
    bool PrecioIncluyeIgv);

public record WhatsAppServicioCatalogoResponse(
    Guid Id,
    string Nombre,
    bool PrecioDisponible,
    decimal? PrecioSugeridoSoles,
    bool PrecioIncluyeIgv,
    TipoAfectacionIgv TipoAfectacionIgv,
    decimal? PrecioReferencialConIgv);

public record WhatsAppCatalogoConsultaResponse(
    List<WhatsAppProductoCatalogoResponse> Productos,
    List<WhatsAppServicioCatalogoResponse> Servicios);

// =============================================================================
// CONTRATOS FASE 3 — ESTADO DE LA CAPA DE IA CONVERSACIONAL (SIN SECRETOS)
// =============================================================================

public record WhatsAppEstadoIaResponse(
    bool Enabled,
    string ProveedorConfigurado,
    string ProveedorEfectivo,
    bool ProveedorOperativo,
    string Model,
    bool TieneCredencialesExternasConfiguradas,
    bool VerificacionIdentidadHabilitada,
    bool WhatsAppHabilitadoParaEnvios,
    string WhatsAppProveedorEfectivo,
    bool AllowFaqTools,
    bool AllowPublicCatalogTools,
    int MaxInputChars,
    int MaxOutputChars,
    int MaxOutputTokens,
    int TimeoutSeconds);
