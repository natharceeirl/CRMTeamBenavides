using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
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

        var queryProductos = _context.Productos
            .AsNoTracking()
            .Where(p => p.Activo && !string.IsNullOrWhiteSpace(p.Codigo) && p.Tipo != TipoProducto.Vehiculo);

        var queryUnidades = _context.UnidadesInventario
            .AsNoTracking()
            .Include(u => u.Producto)
            .Include(u => u.Compra)
            .Include(u => u.Vehiculo)
            .Where(u => u.Activo && u.Producto.Activo && u.Estado == EstadoUnidadInventario.EnStock && !string.IsNullOrWhiteSpace(u.NumeroSerieVIN));

        if (soloMarcaYamaha)
        {
            queryProductos = queryProductos.Where(p => p.Marca != null && p.Marca.ToUpper() == "YAMAHA");
            queryUnidades = queryUnidades.Where(u => u.Producto.Marca != null && u.Producto.Marca.ToUpper() == "YAMAHA");
        }

        int tamanioLote = _settings.TamanioLoteExportacion > 0 ? _settings.TamanioLoteExportacion : 500;
        int offset = 0;
        int totalLeidos = 0;
        int totalValidos = 0;
        int totalOmitidos = 0;
        var dtosValidos = new List<YamahaStockV3Dto>();
        var observaciones = new HashSet<string>();

        // 1. Exportar Repuestos (Línea SP)
        while (true)
        {
            var paginaProductos = await queryProductos
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
            foreach (var dto in resultadoMapeo.DtosProyectados)
            {
                if (!_settings.EsLineaHabilitada(dto.LineaNegocio))
                {
                    totalOmitidos++;
                    observaciones.Add($"Línea '{dto.LineaNegocio}' deshabilitada por configuración (YamahaSettings.LineasHabilitadas).");
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

            offset += paginaProductos.Count;
            if (paginaProductos.Count < tamanioLote)
            {
                break;
            }
        }

        // 2. Exportar Unidades Físicas de Vehículos en Stock (Líneas MC y ATV)
        offset = 0;
        while (true)
        {
            var paginaUnidades = await queryUnidades
                .OrderBy(u => u.NumeroSerieVIN)
                .ThenBy(u => u.Id)
                .Skip(offset)
                .Take(tamanioLote)
                .ToListAsync(cancellationToken);

            if (paginaUnidades.Count == 0)
            {
                break;
            }

            // Hidratar Categoría de cada modelo vehicular para clasificación explícita MC / ATV / OM
            var productosVehiculos = paginaUnidades
                .Select(u => u.Producto)
                .Where(p => p is not null && p.Categoria is null && p.CategoriaId != Guid.Empty)
                .ToList();

            if (productosVehiculos.Count > 0)
            {
                var categoriaIds = productosVehiculos.Select(p => p.CategoriaId).Distinct().ToList();
                var categorias = await _context.CategoriasProducto
                    .AsNoTracking()
                    .Where(c => categoriaIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, cancellationToken);

                foreach (var prod in productosVehiculos)
                {
                    if (categorias.TryGetValue(prod.CategoriaId, out var cat))
                    {
                        prod.Categoria = cat;
                    }
                }
            }

            totalLeidos += paginaUnidades.Count;
            var resultadoMapeo = _mappingService.MapearLoteStock(Enumerable.Empty<Producto>(), contexto, paginaUnidades);
            foreach (var dto in resultadoMapeo.DtosProyectados)
            {
                if (!_settings.EsLineaHabilitada(dto.LineaNegocio))
                {
                    totalOmitidos++;
                    observaciones.Add($"Línea '{dto.LineaNegocio}' deshabilitada por configuración (YamahaSettings.LineasHabilitadas).");
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

            offset += paginaUnidades.Count;
            if (paginaUnidades.Count < tamanioLote)
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
