using CRMTeamBenavides.Api.Features.Chatbot;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Services;

public interface IChatbotService
{
    // Public
    Task<List<FaqResponse>> GetFaqsPublicasAsync(string? categoria, string? busqueda);
    Task<ServiceResult<ConsultaChatbotResponse>> ConsultarAsync(ConsultaChatbotRequest request, Guid? usuarioAutenticadoId);
    Task<ServiceResult<SolicitudAgenteResponse>> SolicitarAgenteAsync(SolicitarAgenteRequest request, Guid? usuarioAutenticadoId);

    // Admin FAQs
    Task<List<FaqResponse>> GetFaqsAdminAsync(string? categoria, bool? soloActivos);
    Task<ServiceResult<FaqResponse>> CreateFaqAsync(CreateFaqRequest request, Guid? usuarioId);
    Task<ServiceResult<FaqResponse>> UpdateFaqAsync(Guid id, UpdateFaqRequest request, Guid? usuarioId);
    Task<ServiceResult<bool>> DeleteFaqAsync(Guid id, Guid? usuarioId);

    // Admin Consultas (Bandeja)
    Task<List<ConsultaBandejaResponse>> GetConsultasAdminAsync(
        bool? requiereAtencion,
        EstadoAtencionConsulta? estado,
        string? canal,
        DateTime? fechaDesde,
        DateTime? fechaHasta);

    Task<ServiceResult<ConsultaBandejaResponse>> AsignarAgenteAsync(Guid id, AsignarAgenteRequest request, Guid? usuarioId);
    Task<ServiceResult<ConsultaBandejaResponse>> ResolverConsultaAsync(Guid id, ResolverConsultaRequest request, Guid? usuarioId);

    // Autorización por roles
    Task<bool> TieneRolAsync(Guid usuarioId, params string[] rolesPermitidos);
}
