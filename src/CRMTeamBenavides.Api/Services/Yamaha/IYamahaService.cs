using CRMTeamBenavides.Api.Features.Yamaha;

namespace CRMTeamBenavides.Api.Services.Yamaha;

public interface IYamahaService
{
    Task<YamahaConsultaMockResponse?> ConsultarAsync(string criterio, CancellationToken ct = default);
}
