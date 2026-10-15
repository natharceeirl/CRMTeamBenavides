using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.Yamaha.Exportadores;

/// <summary>
/// Extractor desde PostgreSQL para el Servicio 2 — Stock de Yamaha v3.
/// Consulta productos activos de almacén, calcula costos e IGV, y proyecta usando IYamahaMappingService.
/// </summary>
public sealed class YamahaStockExporter : IYamahaStockExporter
{
    private readonly ApplicationDbContext _context;
    private readonly IYamahaMappingService _mappingService;
    private readonly YamahaSettings _settings;

    public YamahaStockExporter(
        ApplicationDbContext context,
        IYamahaMappingService mappingService,
        IOptions<YamahaSettings> settings)
    {
        _context = context;
        _mappingService = mappingService;
        _settings = settings.Value;
    }

    /// <inheritdoc />
    public async Task<YamahaExportacionResultado<YamahaStockV3Dto>> ExportarStockAsync(
        bool soloMarcaYamaha = false,
        CancellationToken cancellationToken = default)
    {
        var empresa = await _context.ConfiguracionesEmpresa
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var contexto = YamahaPuntoVentaContexto.Crear(empresa, _settings);

        var query = _context.Productos
            .AsNoTracking()
            .Where(p => p.Activo && !string.IsNullOrWhiteSpace(p.Codigo));

        if (soloMarcaYamaha)
        {
            query = query.Where(p => p.Marca != null && p.Marca.ToUpper() == "YAMAHA");
        }

        int tamanioLote = _settings.TamanioLoteExportacion > 0 ? _settings.TamanioLoteExportacion : 500;
        int offset = 0;
        int totalLeidos = 0;
        int totalValidos = 0;
        int totalOmitidos = 0;
        var dtosValidos = new List<YamahaStockV3Dto>();
        var observaciones = new HashSet<string>();

        while (true)
        {
            var paginaProductos = await query
                .OrderBy(p => p.Codigo)
                .ThenBy(p => p.Id)
                .Skip(offset)
                .Take(tamanioLote)
                .ToListAsync(cancellationToken);

            if (paginaProductos.Count == 0)
            {
                break;
            }

            totalLeidos += paginaProductos.Count;
            var resultadoMapeo = _mappingService.MapearLoteStock(paginaProductos, contexto);
            dtosValidos.AddRange(resultadoMapeo.DtosProyectados);
            totalValidos += resultadoMapeo.TotalValidos;
            totalOmitidos += resultadoMapeo.TotalNoProyectables;

            foreach (var obs in resultadoMapeo.Registros.SelectMany(r => r.Observaciones))
            {
                observaciones.Add(obs);
            }

            offset += paginaProductos.Count;
            if (paginaProductos.Count < tamanioLote)
            {
                break;
            }
        }

        if (soloMarcaYamaha)
        {
            observaciones.Add("D-07: Se aplicó filtro exclusivo para repuestos de marca YAMAHA.");
        }
        else
        {
            observaciones.Add("D-07: Se extrajeron todos los productos activos sin restringir por marca (sujeto a definición final de catálogo YMDP).");
        }

        return new YamahaExportacionResultado<YamahaStockV3Dto>(
            RegistrosValidos: dtosValidos,
            TotalEntidadesLeidas: totalLeidos,
            TotalRegistrosValidos: totalValidos,
            TotalRegistrosOmitidos: totalOmitidos,
            ObservacionesYAdvertencias: observaciones.ToList(),
            RangoDesdeUtc: null,
            RangoHastaUtc: DateTime.UtcNow
        );
    }
}
