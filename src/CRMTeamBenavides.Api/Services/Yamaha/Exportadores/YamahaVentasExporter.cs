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
            .Where(v => v.Comprobante != null && v.Comprobante.Activo && v.Comprobante.Estado == "Emitido");

        // desde exclusivo y hasta inclusivo: el lote siguiente parte del "hasta" de este sin repetir el borde.
        if (desdeUtc.HasValue)
        {
            query = query.Where(v => v.Comprobante!.FechaCreacion > desdeUtc.Value || v.Fecha > desdeUtc.Value);
        }

        query = query.Where(v => v.Comprobante!.FechaCreacion <= hastaUtc.Value && v.Fecha <= hastaUtc.Value);

        // Regla estricta Yamaha v3 (confirmación YMDP): las operaciones de taller se reportan
        // exclusivamente mediante Servicio 3; el Servicio 1 filtra siempre OrdenServicioId == null.
        query = query.Where(v => v.OrdenServicioId == null);

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

            var idsUnidades = paginaVentas
                .SelectMany(v => v.Detalles)
                .SelectMany(d => d.UnidadesInventarioIds)
                .Distinct()
                .ToList();

            var ventaIds = paginaVentas.Select(v => v.Id).ToList();

            var unidades = await _context.UnidadesInventario
                .AsNoTracking()
                .Include(u => u.Producto)
                .Include(u => u.Vehiculo)
                .Where(u => idsUnidades.Contains(u.Id) || (u.VentaId.HasValue && ventaIds.Contains(u.VentaId.Value)))
                .ToListAsync(cancellationToken);

            // Hidratar Categoría por separado (evita INNER JOIN restrictivo y carga la clasificación maestra de productos)
            var productosPagina = paginaVentas
                .SelectMany(v => v.Detalles)
                .Select(d => d.Producto)
                .Concat(unidades.Select(u => u.Producto))
                .Where(p => p is not null && p.Categoria is null && p.CategoriaId != Guid.Empty)
                .Cast<Producto>()
                .ToList();

            if (productosPagina.Count > 0)
            {
                var categoriaIds = productosPagina.Select(p => p.CategoriaId).Distinct().ToList();
                var categorias = await _context.CategoriasProducto
                    .AsNoTracking()
                    .Where(c => categoriaIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, cancellationToken);

                foreach (var prod in productosPagina)
                {
                    if (categorias.TryGetValue(prod.CategoriaId, out var cat))
                    {
                        prod.Categoria = cat;
                    }
                }
            }

            var dictUnidades = unidades
                .GroupBy(u => u.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var resultadoMapeo = _mappingService.MapearLoteVentas(paginaVentas, contexto, dictUnidades);
            var fechaPorComprobante = paginaVentas
                .Where(v => v.Comprobante is not null)
                .GroupBy(v => $"{v.Comprobante!.Serie?.Trim()}-{v.Comprobante!.Numero?.Trim()}", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Max(v => v.Comprobante!.FechaCreacion), StringComparer.OrdinalIgnoreCase);

            foreach (var dto in resultadoMapeo.DtosProyectados)
            {
                if (!_settings.EsLineaHabilitada(dto.LineaNegocio))
                {
                    totalOmitidos++;
                    observaciones.Add($"Línea '{dto.LineaNegocio}' deshabilitada por configuración (YamahaSettings.LineasHabilitadas).");
                    continue;
                }

                var esVehiculoDto = dto.LineaNegocio is YamahaV3Catalogos.LineaMotocicletas
                    or YamahaV3Catalogos.LineaCuatrimotos
                    or YamahaV3Catalogos.LineaMotoresFueraDeBorda;

                if (esVehiculoDto &&
                    _settings.FechaInicioVehiculosUtc.HasValue &&
                    fechaPorComprobante.TryGetValue(dto.NumeroComprobante, out var fechaCompUtc) &&
                    fechaCompUtc < _settings.FechaInicioVehiculosUtc.Value)
                {
                    totalOmitidos++;
                    observaciones.Add($"Venta vehicular '{dto.NumeroComprobante}' ({dto.LineaNegocio}) anterior a FechaInicioVehiculosUtc omitida por configuración.");
                    continue;
                }

                dtosValidos.Add(dto);
                totalValidos++;
            }

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

        observaciones.Add("D-02: Se excluyeron estrictamente ventas vinculadas a órdenes de taller (OrdenServicioId == null) para prevenir duplicidad con Servicio Técnico conforme a la decisión final de Yamaha v3.");

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
