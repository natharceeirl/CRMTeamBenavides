using CRMTeamBenavides.Api.Features.Dashboard;

namespace CRMTeamBenavides.Api.Services;

public interface IDashboardService
{
    Task<DashboardResumenResponse> GetResumenAsync(DateTime? fechaDesde, DateTime? fechaHasta);
}
