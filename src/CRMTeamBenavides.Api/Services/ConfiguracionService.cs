using CRMTeamBenavides.Api.Features.Configuracion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class ConfiguracionService : IConfiguracionService
{
    private readonly ApplicationDbContext _context;

    public ConfiguracionService(ApplicationDbContext context)
    {
        _context = context;
    }

    private async Task<ConfiguracionEmpresa> GetOrCreateEntityAsync(CancellationToken ct = default)
    {
        var config = await _context.ConfiguracionesEmpresa.FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = new ConfiguracionEmpresa
            {
                NombreEmpresa = "Team Benavides",
                Ruc = "20000000001",
                PorcentajeIgv = 18.00m,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            _context.ConfiguracionesEmpresa.Add(config);
            await _context.SaveChangesAsync(ct);
        }
        return config;
    }

    public async Task<ConfiguracionEmpresaResponse> ObtenerConfiguracionEmpresaAsync(CancellationToken ct = default)
    {
        var config = await GetOrCreateEntityAsync(ct);
        return new ConfiguracionEmpresaResponse(config.Id, config.NombreEmpresa, config.Ruc, config.PorcentajeIgv);
    }

    public async Task<decimal> ObtenerPorcentajeIgvVigenteAsync(CancellationToken ct = default)
    {
        var config = await GetOrCreateEntityAsync(ct);
        return config.PorcentajeIgv;
    }

    public async Task<ServiceResult<ConfiguracionEmpresaResponse>> ActualizarConfiguracionEmpresaAsync(
        ActualizarConfiguracionEmpresaRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NombreEmpresa))
            return ServiceResult<ConfiguracionEmpresaResponse>.Invalid("El nombre de la empresa es obligatorio.");

        if (request.PorcentajeIgv < 0 || request.PorcentajeIgv > 100)
            return ServiceResult<ConfiguracionEmpresaResponse>.Invalid("El porcentaje de IGV debe estar entre 0 y 100.");

        var config = await GetOrCreateEntityAsync(ct);
        config.NombreEmpresa = request.NombreEmpresa.Trim();
        config.Ruc = string.IsNullOrWhiteSpace(request.Ruc) ? null : request.Ruc.Trim();
        config.PorcentajeIgv = request.PorcentajeIgv;
        config.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return ServiceResult<ConfiguracionEmpresaResponse>.Success(
            new ConfiguracionEmpresaResponse(config.Id, config.NombreEmpresa, config.Ruc, config.PorcentajeIgv));
    }
}
