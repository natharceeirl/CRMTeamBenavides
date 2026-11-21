using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
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

        // Un único Any garantiza que un mismo comprobante activo y emitido caiga dentro de (desdeUtc, hastaUtc],
        // evitando duplicidades cuando una orden tiene múltiples comprobantes en ventanas distintas.
        var query = _context.OrdenesServicio
            .AsNoTracking()
            .Include(os => os.Cliente)
            .Include(os => os.Vehiculo)
            .Include(os => os.Comprobantes)
            .Include(os => os.Detalles)
                .ThenInclude(d => d.Producto)
            .Where(os => os.Activo && os.Estado != EstadoOrdenServicio.Cancelada && os.Vehiculo != null);

        if (desdeUtc.HasValue)
        {
            var desde = desdeUtc.Value;
            var hasta = hastaUtc.Value;
            query = query.Where(os => os.Comprobantes.Any(c =>
                c.Activo &&
                c.Estado == "Emitido" &&
                c.FechaCreacion > desde &&
                c.FechaCreacion <= hasta));
        }
        else
        {
            var hasta = hastaUtc.Value;
            query = query.Where(os => os.Comprobantes.Any(c =>
                c.Activo &&
                c.Estado == "Emitido" &&
                c.FechaCreacion <= hasta));
        }

        int tamanioLote = _settings.TamanioLoteExportacion > 0 ? _settings.TamanioLoteExportacion : 500;
        int offset = 0;
        int totalLeidas = 0;
        int totalValidos = 0;
        int totalOmitidos = 0;
        var dtosValidos = new List<YamahaServicioTecnicoV3Dto>();
        var observaciones = new HashSet<string>();

        while (true)
        {
            var paginaOrdenes = await query
                .OrderBy(os => os.FechaIngreso)
                .ThenBy(os => os.Id)
                .Skip(offset)
                .Take(tamanioLote)
                .ToListAsync(cancellationToken);

            if (paginaOrdenes.Count == 0)
            {
                break;
            }

            totalLeidas += paginaOrdenes.Count;
            var resultadoMapeo = _mappingService.MapearLoteOrdenesServicio(paginaOrdenes, contexto);
            dtosValidos.AddRange(resultadoMapeo.DtosProyectados);
            totalValidos += resultadoMapeo.TotalValidos;
            totalOmitidos += resultadoMapeo.TotalNoProyectables;

            foreach (var obs in resultadoMapeo.Registros.SelectMany(r => r.Observaciones))
            {
                observaciones.Add(obs);
            }

            offset += paginaOrdenes.Count;
            if (paginaOrdenes.Count < tamanioLote)
            {
                break;
            }
        }

        observaciones.Add("D-08: Prefijo correlativo OS- transformado a OT- de forma no destructiva en proyección.");
        observaciones.Add("D-09: Clasificación TipoAtencion traducida determinísticamente a códigos TipoServicio de Yamaha v3.");

        return new YamahaExportacionResultado<YamahaServicioTecnicoV3Dto>(
            RegistrosValidos: dtosValidos,
            TotalEntidadesLeidas: totalLeidas,
            TotalRegistrosValidos: totalValidos,
            TotalRegistrosOmitidos: totalOmitidos,
            ObservacionesYAdvertencias: observaciones.ToList(),
            RangoDesdeUtc: desdeUtc,
            RangoHastaUtc: hastaUtc
        );
    }
}
