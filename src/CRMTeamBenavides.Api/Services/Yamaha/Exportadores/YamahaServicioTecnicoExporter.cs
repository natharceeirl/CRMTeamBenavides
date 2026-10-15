using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.Yamaha.Exportadores;

/// <summary>
/// Extractor desde PostgreSQL para el Servicio 3 — Servicio Técnico de Yamaha v3.
/// Consulta órdenes de servicio con comprobantes emitidos, vehículos y clientes,
/// aplicando la transformación y proyección mediante IYamahaMappingService.
/// </summary>
public sealed class YamahaServicioTecnicoExporter : IYamahaServicioTecnicoExporter
{
    private readonly ApplicationDbContext _context;
    private readonly IYamahaMappingService _mappingService;
    private readonly YamahaSettings _settings;

    public YamahaServicioTecnicoExporter(
        ApplicationDbContext context,
        IYamahaMappingService mappingService,
        IOptions<YamahaSettings> settings)
    {
        _context = context;
        _mappingService = mappingService;
        _settings = settings.Value;
    }

    /// <inheritdoc />
    public async Task<YamahaExportacionResultado<YamahaServicioTecnicoV3Dto>> ExportarServicioTecnicoAsync(
        DateTime? desdeUtc = null,
        DateTime? hastaUtc = null,
        CancellationToken cancellationToken = default)
    {
        hastaUtc ??= DateTime.UtcNow;

        var empresa = await _context.ConfiguracionesEmpresa
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var contexto = YamahaPuntoVentaContexto.Crear(empresa, _settings);

        var query = _context.OrdenesServicio
            .AsNoTracking()
            .Include(os => os.Cliente)
            .Include(os => os.Vehiculo)
            .Include(os => os.Comprobantes)
            .Include(os => os.Detalles)
                .ThenInclude(d => d.Producto)
            .Where(os => os.Activo && os.Vehiculo != null)
            .Where(os => os.Comprobantes.Any(c => c.Estado == "Emitido"));

        if (desdeUtc.HasValue)
        {
            query = query.Where(os => os.Comprobantes.Any(c => c.Estado == "Emitido" && c.FechaCreacion >= desdeUtc.Value));
        }

        query = query.Where(os => os.Comprobantes.Any(c => c.Estado == "Emitido" && c.FechaCreacion <= hastaUtc.Value));

        var ordenes = await query
            .OrderBy(os => os.FechaIngreso)
            .ToListAsync(cancellationToken);

        var resultadoMapeo = _mappingService.MapearLoteOrdenesServicio(ordenes, contexto);

        var observaciones = resultadoMapeo.Registros
            .SelectMany(r => r.Observaciones)
            .Distinct()
            .ToList();

        observaciones.Add("D-08: Prefijo correlativo OS- transformado a OT- de forma no destructiva en proyección.");
        observaciones.Add("D-09: Clasificación TipoAtencion traducida determinísticamente a códigos TipoServicio de Yamaha v3.");

        return new YamahaExportacionResultado<YamahaServicioTecnicoV3Dto>(
            RegistrosValidos: resultadoMapeo.DtosProyectados,
            TotalEntidadesLeidas: ordenes.Count,
            TotalRegistrosValidos: resultadoMapeo.TotalValidos,
            TotalRegistrosOmitidos: resultadoMapeo.TotalNoProyectables,
            ObservacionesYAdvertencias: observaciones,
            RangoDesdeUtc: desdeUtc,
            RangoHastaUtc: hastaUtc
        );
    }
}
