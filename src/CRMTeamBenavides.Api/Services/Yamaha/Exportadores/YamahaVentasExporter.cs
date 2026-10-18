using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.Yamaha.Exportadores;

/// <summary>
/// Extractor desde PostgreSQL para el Servicio 1 — Ventas de Yamaha v3.
/// Consulta ventas activas y confirmadas, con comprobante emitido, dentro de una ventana temporal,
/// y las proyecta mediante IYamahaMappingService.
/// </summary>
public sealed class YamahaVentasExporter : IYamahaVentasExporter
{
    private readonly ApplicationDbContext _context;
    private readonly IYamahaMappingService _mappingService;
    private readonly YamahaSettings _settings;

    public YamahaVentasExporter(
        ApplicationDbContext context,
        IYamahaMappingService mappingService,
        IOptions<YamahaSettings> settings)
    {
        _context = context;
        _mappingService = mappingService;
        _settings = settings.Value;
    }

    /// <inheritdoc />
    public async Task<YamahaExportacionResultado<YamahaVentaV3Dto>> ExportarVentasAsync(
        DateTime? desdeUtc = null,
        DateTime? hastaUtc = null,
        bool soloVentasMostrador = true,
        CancellationToken cancellationToken = default)
    {
        hastaUtc ??= DateTime.UtcNow;

        var empresa = await _context.ConfiguracionesEmpresa
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var contexto = YamahaPuntoVentaContexto.Crear(empresa, _settings);

        var query = _context.Ventas
            .AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Comprobante)
            .Include(v => v.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(v => v.OrdenServicio)
                .ThenInclude(os => os!.Vehiculo)
            .Where(v => v.Activo && v.Estado == EstadoVenta.Confirmada)
            .Where(v => v.Comprobante != null && v.Comprobante.Estado == "Emitido");

        // desde exclusivo y hasta inclusivo: el lote siguiente parte del "hasta" de este sin repetir el borde.
        if (desdeUtc.HasValue)
        {
            query = query.Where(v => v.Comprobante!.FechaCreacion > desdeUtc.Value || v.Fecha > desdeUtc.Value);
        }

        query = query.Where(v => v.Comprobante!.FechaCreacion <= hastaUtc.Value && v.Fecha <= hastaUtc.Value);

        if (soloVentasMostrador)
        {
            query = query.Where(v => v.OrdenServicioId == null);
        }

        int tamanioLote = _settings.TamanioLoteExportacion > 0 ? _settings.TamanioLoteExportacion : 500;
        int offset = 0;
        int totalLeidas = 0;
        int totalValidos = 0;
        int totalOmitidos = 0;
        var dtosValidos = new List<YamahaVentaV3Dto>();
        var observaciones = new HashSet<string>();

        while (true)
        {
            var paginaVentas = await query
                .OrderBy(v => v.Fecha)
                .ThenBy(v => v.Id)
                .Skip(offset)
                .Take(tamanioLote)
                .ToListAsync(cancellationToken);

            if (paginaVentas.Count == 0)
            {
                break;
            }

            totalLeidas += paginaVentas.Count;
            var resultadoMapeo = _mappingService.MapearLoteVentas(paginaVentas, contexto);
            dtosValidos.AddRange(resultadoMapeo.DtosProyectados);
            totalValidos += resultadoMapeo.TotalValidos;
            totalOmitidos += resultadoMapeo.TotalNoProyectables;

            foreach (var obs in resultadoMapeo.Registros.SelectMany(r => r.Observaciones))
            {
                observaciones.Add(obs);
            }

            offset += paginaVentas.Count;
            if (paginaVentas.Count < tamanioLote)
            {
                break;
            }
        }

        if (soloVentasMostrador)
        {
            observaciones.Add("D-02: Se filtraron exclusivamente ventas directas de mostrador (OrdenServicioId == null) para prevenir duplicidad con Servicio Técnico.");
        }
        else
        {
            observaciones.Add("D-02: Se incluyeron ventas asociadas a órdenes de taller; riesgo latente de doble reporte de repuestos hasta resolución formal de YMDP.");
        }

        return new YamahaExportacionResultado<YamahaVentaV3Dto>(
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
