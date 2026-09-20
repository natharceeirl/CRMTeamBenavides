using CRMTeamBenavides.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CRMTeamBenavides.Api.Health;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(ApplicationDbContext context, ILogger<DatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Base de datos conectada correctamente.")
                : HealthCheckResult.Unhealthy("No se pudo establecer conexión con PostgreSQL.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo en health check de base de datos.");
            return HealthCheckResult.Unhealthy("Error al verificar conexión con la base de datos.");
        }
    }
}
