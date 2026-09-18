using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Chatbot;

// --- FAQs ---

public record FaqResponse(
    Guid Id,
    string Categoria,
    string Pregunta,
    string Respuesta,
    string? PalabrasClave,
    int Orden,
    int VecesConsultada,
    bool Activo);

public record CreateFaqRequest(
    string Categoria,
    string Pregunta,
    string Respuesta,
    string? PalabrasClave,
    int Orden);

public record UpdateFaqRequest(
    string Categoria,
    string Pregunta,
    string Respuesta,
    string? PalabrasClave,
    int Orden,
    bool Activo);

// --- Interacción Chatbot (Pública) ---

public record ConsultaChatbotRequest(
    string Mensaje,
    string? NombreContacto,
    string? TelefonoContacto,
    string? Canal);

public record ConsultaChatbotResponse(
    bool ResueltoPorFaq,
    FaqResponse? Faq,
    List<FaqResponse> Sugerencias,
    bool RequiereAgente,
    Guid ConsultaId,
    string MensajeRespuesta);

public record SolicitarAgenteRequest(
    Guid? ConsultaId,
    string? NombreContacto,
    string TelefonoContacto,
    string Motivo,
    string? Canal);

public record SolicitudAgenteResponse(
    Guid ConsultaId,
    string Estado,
    string Mensaje);

// --- Bandeja Administrativa ---

public record ConsultaBandejaResponse(
    Guid Id,
    DateTime Fecha,
    string Canal,
    Guid? ClienteId,
    string? ClienteNombre,
    string? NombreContacto,
    string? TelefonoContacto,
    string MensajeConsulta,
    Guid? FaqItemId,
    string? FaqPregunta,
    bool RequiereAtencionAgente,
    string EstadoAtencion,
    int EstadoAtencionId,
    Guid? AgenteAsignadoId,
    string? AgenteNombre,
    string? NotasAgente,
    DateTime? FechaDerivacion,
    DateTime? FechaResolucion);

public record AsignarAgenteRequest(
    Guid AgenteId);

public record ResolverConsultaRequest(
    EstadoAtencionConsulta Estado,
    string? NotasAgente);
