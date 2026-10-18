using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Implementación de gestión de marcas de agua para Yamaha v3.
/// Deriva el punto de corte temporal a partir de los registros auditados en YamahaSyncLog,
/// asegurando que jamás se avance el corte si la última ejecución no fue exitosa en todas sus fases.
/// </summary>
public sealed class YamahaWatermarkService : IYamahaWatermarkService
{
    private readonly ApplicationDbContext _context;

    public YamahaWatermarkService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<DateTime> ObtenerPuntoCorteDesdeUtcAsync(
        YamahaServicioSincronizacion servicio,
        CancellationToken cancellationToken = default)
    {
        var ultimoExitoso = await ObtenerUltimoLogExitosoAsync(servicio, cancellationToken);
        if (ultimoExitoso is not null)
        {
            // El siguiente lote parte exactamente del fin del rango enviado; los logs anteriores
            // a que existiera el rango usan la fecha de inicio.
            return ultimoExitoso.RangoHastaUtc ?? ultimoExitoso.FechaInicio;
        }

        // Sección 3.1: Primer envío reglamentario abarca los últimos 3 meses
        return DateTime.UtcNow.AddMonths(-3);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<YamahaServicioSincronizacion, YamahaWatermarkEstadoDto>> ObtenerDiagnosticoWatermarksAsync(
        CancellationToken cancellationToken = default)
    {
        var servicios = new[]
        {
            YamahaServicioSincronizacion.Ventas,
            YamahaServicioSincronizacion.Stock,
            YamahaServicioSincronizacion.ServicioTecnico
        };

        var resultado = new Dictionary<YamahaServicioSincronizacion, YamahaWatermarkEstadoDto>();

        foreach (var servicio in servicios)
        {
            var ultimoExitoso = await ObtenerUltimoLogExitosoAsync(servicio, cancellationToken);
            var esInicial = ultimoExitoso is null;
            var puntoCorte = ultimoExitoso?.RangoHastaUtc ?? ultimoExitoso?.FechaInicio ?? DateTime.UtcNow.AddMonths(-3);

            resultado[servicio] = new YamahaWatermarkEstadoDto(
                Servicio: servicio,
                PuntoCorteUtc: puntoCorte,
                EsCargaInicialTresMeses: esInicial,
                UltimaEjecucionExitosaUtc: ultimoExitoso?.FechaFin ?? ultimoExitoso?.FechaInicio,
                UltimoSyncLogId: ultimoExitoso?.Id
            );
        }

        return resultado;
    }

    private Task<YamahaSyncLog?> ObtenerUltimoLogExitosoAsync(
        YamahaServicioSincronizacion servicio,
        CancellationToken cancellationToken)
    {
        return _context.YamahaSyncLogs
            .AsNoTracking()
            .Where(l => l.Servicio == servicio &&
                        l.Exitoso &&
                        !l.EsSimulacion &&
                        (l.Estado == YamahaEstadoSincronizacion.Completado ||
                         l.Estado == YamahaEstadoSincronizacion.SinCambios))
            .OrderByDescending(l => l.FechaInicio)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
