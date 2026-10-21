using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CRMTeamBenavides.Api.Features.Importaciones;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services.Importacion;

public class ImportadorExcelService : IImportadorExcelService
{
    public const string EntidadAuditoria = "ImportacionExcel";
    public const string AccionValidar = "ValidarExcel";
    public const string AccionEjecutar = "ImportarExcel";
    public const string AccionEjecutarFallido = "ImportarExcelFallido";

    public const string ModoErrorSiExiste = "ErrorSiExiste";
    public const string ModoOmitirExistentes = "OmitirExistentes";

    private const string HojaClientes = "Clientes";
    private const string HojaUnidades = "Unidades";
    private const string HojaRepuestos = "Repuestos";
    private const string HojaCatalogo = "Catalogo";

    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public ImportadorExcelService(ApplicationDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    public byte[] GenerarPlantillaExcel()
    {
        using var workbook = new XLWorkbook();

        GenerarHojaClientes(workbook);
        GenerarHojaUnidades(workbook);
        GenerarHojaRepuestos(workbook);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<ResultadoImportacionExcelDto> ValidarArchivoAsync(
        Stream archivoStream,
        string nombreArchivo,
        Guid? categoriaId,
        Guid? usuarioId,
        string modoDuplicados = ModoErrorSiExiste,
        CancellationToken ct = default)
    {
        var parseo = await ProcesarYValidarLibroAsync(
            archivoStream,
            nombreArchivo,
            categoriaId,
            modoDuplicados,
            soloValidacion: true,
            ct);

        return parseo.Resultado;
    }

    public async Task<ResultadoImportacionExcelDto> EjecutarImportacionAsync(
        Stream archivoStream,
        string nombreArchivo,
        Guid? categoriaId,
        Guid? usuarioId,
        string modoDuplicados = ModoErrorSiExiste,
        CancellationToken ct = default)
    {
        var parseo = await ProcesarYValidarLibroAsync(
            archivoStream,
            nombreArchivo,
            categoriaId,
            modoDuplicados,
            soloValidacion: false,
            ct);

        if (!parseo.Resultado.Exitoso)
        {
            await RegistrarAuditoriaAsync(
                usuarioId,
                AccionEjecutarFallido,
                nombreArchivo,
                parseo.Resultado,
                ct);

            return parseo.Resultado;
        }

        var ahora = DateTime.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Revalidación para hoja legada Catalogo si tiene productos nuevos
            if (parseo.CatalogoValidos.Count > 0)
            {
                bool categoriaSigueActiva = categoriaId.HasValue
                    && categoriaId.Value != Guid.Empty
                    && await _context.CategoriasProducto
                        .AsNoTracking()
                        .AnyAsync(c => c.Id == categoriaId.Value && c.Activo, ct);

                if (!categoriaSigueActiva)
                {
                    await transaction.RollbackAsync(ct);

                    var erroresCat = new List<ErrorFilaImportacionDto>
                    {
                        new(
                            Hoja: HojaCatalogo,
                            Fila: 0,
                            Columna: "CategoriaId",
                            ValorLeido: categoriaId?.ToString(),
                            Mensaje: "La categoría indicada para los productos nuevos del catálogo no existe o no se encuentra activa.")
                    };

                    var resultadoCatInvalida = parseo.Resultado with
                    {
                        Exitoso = false,
                        Clientes = parseo.Resultado.Clientes with { RegistrosCreados = 0 },
                        Unidades = parseo.Resultado.Unidades is not null ? parseo.Resultado.Unidades with { RegistrosCreados = 0 } : null,
                        Repuestos = parseo.Resultado.Repuestos is not null ? parseo.Resultado.Repuestos with { RegistrosCreados = 0 } : null,
                        Catalogo = parseo.Resultado.Catalogo with { RegistrosCreados = 0 },
                        Errores = erroresCat
                    };

                    await RegistrarAuditoriaAsync(
                        usuarioId,
                        AccionEjecutarFallido,
                        nombreArchivo,
                        resultadoCatInvalida,
                        ct);

                    return resultadoCatInvalida;
                }
            }

            // 1. Crear dinámicamente las categorías de Repuestos que no existan en la base de datos
            var categoriasActualesBd = await _context.CategoriasProducto
                .Where(c => c.Activo)
                .Select(c => new { c.Id, Nombre = c.Nombre != null ? c.Nombre.Trim() : string.Empty })
                .ToListAsync(ct);

            var mapaCategoriasActual = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var categoriasActualesAmbiguas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var grupo in categoriasActualesBd.Where(c => !string.IsNullOrWhiteSpace(c.Nombre)).GroupBy(c => c.Nombre, StringComparer.OrdinalIgnoreCase))
            {
                var ids = grupo.Select(x => x.Id).Distinct().ToList();
                if (ids.Count > 1)
                {
                    categoriasActualesAmbiguas.Add(grupo.Key);
                }
                else
                {
                    mapaCategoriasActual[grupo.Key] = ids[0];
                }
            }

            var repuestoConCategoriaAmbigua = parseo.RepuestosValidos.FirstOrDefault(r => categoriasActualesAmbiguas.Contains(r.Categoria));
            if (repuestoConCategoriaAmbigua is not null)
            {
                await transaction.RollbackAsync(ct);

                var errorAmb = new ErrorFilaImportacionDto(
                    HojaRepuestos,
                    repuestoConCategoriaAmbigua.Fila,
                    "Categoria",
                    repuestoConCategoriaAmbigua.Categoria,
                    $"Existe ambigüedad en el catálogo del sistema: se encontraron múltiples categorías activas con el nombre '{repuestoConCategoriaAmbigua.Categoria}'. Resuelva la duplicidad en el sistema antes de importar.");

                var resultadoAmb = parseo.Resultado with
                {
                    Exitoso = false,
                    Clientes = parseo.Resultado.Clientes with { RegistrosCreados = 0 },
                    Unidades = parseo.Resultado.Unidades is not null ? parseo.Resultado.Unidades with { RegistrosCreados = 0 } : null,
                    Repuestos = parseo.Resultado.Repuestos is not null ? parseo.Resultado.Repuestos with { RegistrosCreados = 0 } : null,
                    Catalogo = parseo.Resultado.Catalogo with { RegistrosCreados = 0 },
                    Errores = new List<ErrorFilaImportacionDto> { errorAmb }
                };

                await RegistrarAuditoriaAsync(
                    usuarioId,
                    AccionEjecutarFallido,
                    nombreArchivo,
                    resultadoAmb,
                    ct);

                return resultadoAmb;
            }

            foreach (var nombreCat in parseo.CategoriasNuevas)
            {
                if (!mapaCategoriasActual.TryGetValue(nombreCat, out var catExistenteId))
                {
                    var nuevaCat = new CategoriaProducto
                    {
                        Id = Guid.NewGuid(),
                        Nombre = nombreCat,
                        StockMinimoDefault = 4,
                        FechaCreacion = ahora,
                        CreadoPorId = usuarioId,
                        Activo = true
                    };
                    _context.CategoriasProducto.Add(nuevaCat);
                    mapaCategoriasActual[nombreCat] = nuevaCat.Id;
                }
            }

            // 2. Crear Clientes nuevos (sin sobrescribir existentes)
            var nuevosClientes = new List<Cliente>(parseo.ClientesValidos.Count);
            foreach (var fc in parseo.ClientesValidos)
            {
                nuevosClientes.Add(new Cliente
                {
                    Id = fc.Id,
                    TipoDocumento = fc.TipoDocumento,
                    NumeroDocumento = fc.NumeroDocumento,
                    DocumentoIdentidad = fc.NumeroDocumento,
                    NombreCompleto = fc.NombreCompleto,
                    RazonSocial = fc.TipoDocumento == TipoDocumentoCliente.RUC ? fc.NombreCompleto : null,
                    Telefono = fc.Telefono,
                    Email = fc.Email,
                    Direccion = fc.Direccion,
                    FechaCreacion = ahora,
                    CreadoPorId = usuarioId,
                    Activo = true
                });
            }

            if (nuevosClientes.Count > 0)
            {
                _context.Clientes.AddRange(nuevosClientes);
            }

            // 3. Crear Repuestos / Catalogo y registrar exactamente un MovimientoInventario inicial en Kardex si StockActual > 0
            var nuevosProductos = new List<Producto>(parseo.RepuestosValidos.Count + parseo.CatalogoValidos.Count);
            var movimientosKardex = new List<MovimientoInventario>();

            foreach (var fr in parseo.RepuestosValidos)
            {
                Guid catId = mapaCategoriasActual.TryGetValue(fr.Categoria, out var cId)
                    ? cId
                    : (categoriaId ?? Guid.Empty);

                var prod = new Producto
                {
                    Id = Guid.NewGuid(),
                    CategoriaId = catId,
                    Codigo = fr.Codigo,
                    Nombre = fr.Nombre,
                    Unidad = fr.UnidadMedida,
                    Marca = fr.Marca,
                    Costo = fr.Costo,
                    PrecioVenta = fr.PrecioVenta,
                    StockActual = fr.StockActual,
                    StockMinimo = fr.StockMinimo,
                    FechaCreacion = ahora,
                    CreadoPorId = usuarioId,
                    Activo = true
                };
                nuevosProductos.Add(prod);

                if (fr.StockActual > 0)
                {
                    movimientosKardex.Add(new MovimientoInventario
                    {
                        Id = Guid.NewGuid(),
                        ProductoId = prod.Id,
                        Tipo = TipoMovimientoInventario.Entrada,
                        Cantidad = fr.StockActual,
                        CostoUnitario = fr.Costo,
                        Motivo = "Carga inicial de inventario",
                        FechaCreacion = ahora,
                        CreadoPorId = usuarioId,
                        Activo = true
                    });
                }
            }

            foreach (var fcat in parseo.CatalogoValidos)
            {
                var prod = new Producto
                {
                    Id = Guid.NewGuid(),
                    CategoriaId = categoriaId!.Value,
                    Codigo = fcat.Codigo,
                    Nombre = fcat.Nombre,
                    Unidad = "unidad",
                    PrecioVenta = 0m,
                    Costo = 0m,
                    StockActual = 0,
                    StockMinimo = null,
                    FechaCreacion = ahora,
                    CreadoPorId = usuarioId,
                    Activo = true
                };
                nuevosProductos.Add(prod);
            }

            if (nuevosProductos.Count > 0)
            {
                _context.Productos.AddRange(nuevosProductos);
            }

            if (movimientosKardex.Count > 0)
            {
                _context.MovimientosInventario.AddRange(movimientosKardex);
            }

            // 4. Crear Unidades vinculadas a su propietario (cliente nuevo o preexistente)
            var nuevasUnidades = new List<Vehiculo>(parseo.UnidadesValidas.Count);
            foreach (var fu in parseo.UnidadesValidas)
            {
                nuevasUnidades.Add(new Vehiculo
                {
                    Id = Guid.NewGuid(),
                    ClienteId = fu.ClienteId,
                    TipoUnidad = fu.TipoUnidad,
                    Placa = fu.Placa,
                    NumeroSerieVIN = fu.NumeroSerieVIN,
                    NumeroMotor = fu.NumeroMotor,
                    Marca = fu.Marca,
                    Modelo = fu.Modelo,
                    Anio = fu.Anio,
                    TipoMedidor = fu.TipoMedidor,
                    LecturaMedidorActual = fu.LecturaMedidor,
                    Kilometraje = fu.TipoMedidor == TipoMedidor.Kilometraje ? (int?)Math.Round(fu.LecturaMedidor) : null,
                    HorasUso = fu.TipoMedidor == TipoMedidor.Horas ? fu.LecturaMedidor : null,
                    FechaCreacion = ahora,
                    CreadoPorId = usuarioId,
                    Activo = true
                });
            }

            if (nuevasUnidades.Count > 0)
            {
                _context.Vehiculos.AddRange(nuevasUnidades);
            }

            // 5. Registrar evento de auditoría dentro de la misma transacción
            var eventoAuditoria = ConstruirEventoAuditoria(
                usuarioId,
                AccionEjecutar,
                nombreArchivo,
                parseo.Resultado,
                ahora);

            _context.EventosAuditoria.Add(eventoAuditoria);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return parseo.Resultado;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);

            var errores = new List<ErrorFilaImportacionDto>
            {
                new(
                    Hoja: "Archivo",
                    Fila: 0,
                    Columna: "Transaccion",
                    ValorLeido: null,
                    Mensaje: $"La importación fue revertida por un conflicto al guardar en base de datos: {ex.GetBaseException().Message}")
            };

            var resultadoFallido = parseo.Resultado with
            {
                Exitoso = false,
                Clientes = parseo.Resultado.Clientes with { RegistrosCreados = 0 },
                Unidades = parseo.Resultado.Unidades is not null ? parseo.Resultado.Unidades with { RegistrosCreados = 0 } : null,
                Repuestos = parseo.Resultado.Repuestos is not null ? parseo.Resultado.Repuestos with { RegistrosCreados = 0 } : null,
                Catalogo = parseo.Resultado.Catalogo with { RegistrosCreados = 0 },
                Errores = errores
            };

            return resultadoFallido;
        }
    }

    public async Task<List<HistorialImportacionItemDto>> ObtenerHistorialAsync(
        int limite = 50,
        CancellationToken ct = default)
    {
        var take = Math.Clamp(limite, 1, 200);

        var eventos = await _context.EventosAuditoria
            .AsNoTracking()
            .Where(e => e.Entidad == EntidadAuditoria)
            .OrderByDescending(e => e.Fecha)
            .Take(take)
            .ToListAsync(ct);

        var usuarioIds = eventos
            .Where(e => e.UsuarioId.HasValue)
            .Select(e => e.UsuarioId!.Value)
            .Distinct()
            .ToList();

        var usuarios = await _context.Usuarios
            .AsNoTracking()
            .Where(u => usuarioIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NombreCompleto, ct);

        var resultado = new List<HistorialImportacionItemDto>(eventos.Count);
        foreach (var ev in eventos)
        {
            bool exitoso = ev.Accion == AccionEjecutar;
            bool soloValidacion = ev.Accion == AccionValidar;
            Guid? categoriaId = null;
            int clientes = 0, catalogo = 0, unidades = 0, repuestos = 0, errores = 0;
            string? modoDuplicados = null;

            if (!string.IsNullOrWhiteSpace(ev.Detalle))
            {
                try
                {
                    using var doc = JsonDocument.Parse(ev.Detalle);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("Exitoso", out var pExitoso) && (pExitoso.ValueKind == JsonValueKind.True || pExitoso.ValueKind == JsonValueKind.False))
                        exitoso = pExitoso.GetBoolean();
                    if (root.TryGetProperty("SoloValidacion", out var pSoloVal) && (pSoloVal.ValueKind == JsonValueKind.True || pSoloVal.ValueKind == JsonValueKind.False))
                        soloValidacion = pSoloVal.GetBoolean();
                    if (root.TryGetProperty("CategoriaId", out var pCatId) && pCatId.ValueKind == JsonValueKind.String && Guid.TryParse(pCatId.GetString(), out var gCat))
                        categoriaId = gCat;
                    if (root.TryGetProperty("ModoDuplicados", out var pModo) && pModo.ValueKind == JsonValueKind.String)
                        modoDuplicados = pModo.GetString();
                    if (root.TryGetProperty("ClientesProcesados", out var pCli) && pCli.TryGetInt32(out var vCli))
                        clientes = vCli;
                    if (root.TryGetProperty("CatalogoProcesados", out var pCat) && pCat.TryGetInt32(out var vCat))
                        catalogo = vCat;
                    if (root.TryGetProperty("UnidadesProcesadas", out var pUni) && pUni.TryGetInt32(out var vUni))
                        unidades = vUni;
                    if (root.TryGetProperty("RepuestosProcesados", out var pRep) && pRep.TryGetInt32(out var vRep))
                        repuestos = vRep;
                    if (root.TryGetProperty("TotalErrores", out var pErr) && pErr.TryGetInt32(out var vErr))
                        errores = vErr;
                }
                catch
                {
                    // Ignorar si un detalle antiguo no tiene estructura JSON estándar
                }
            }

            resultado.Add(new HistorialImportacionItemDto(
                ev.Id,
                ev.Fecha,
                ev.UsuarioId,
                ev.UsuarioId.HasValue && usuarios.TryGetValue(ev.UsuarioId.Value, out var nombre) ? nombre : null,
                ev.Accion,
                ev.EntidadId,
                exitoso,
                soloValidacion,
                categoriaId,
                clientes,
                catalogo,
                errores,
                unidades,
                repuestos,
                modoDuplicados
            ));
        }

        return resultado;
    }

    // =========================================================================
    // Procesamiento y Validación Celda por Celda (Dry-Run Puro)
    // =========================================================================

    private async Task<ResultadoParseoInterno> ProcesarYValidarLibroAsync(
        Stream archivoStream,
        string nombreArchivo,
        Guid? categoriaId,
        string modoDuplicados,
        bool soloValidacion,
        CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var errores = new List<ErrorFilaImportacionDto>();
        bool omitirExistentes = string.Equals(modoDuplicados, ModoOmitirExistentes, StringComparison.OrdinalIgnoreCase);

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(archivoStream);
        }
        catch (Exception)
        {
            errores.Add(new ErrorFilaImportacionDto(
                Hoja: "Archivo",
                Fila: 0,
                Columna: "Formato",
                ValorLeido: nombreArchivo,
                Mensaje: "El archivo proporcionado no es un libro de Excel (.xlsx) válido o está dañado."));

            return CrearResultadoVacioConErrores(nombreArchivo, categoriaId, modoDuplicados, soloValidacion, ahora, errores);
        }

        using (workbook)
        {
            var wsClientes = BuscarHoja(workbook, HojaClientes);
            var wsUnidades = BuscarHoja(workbook, HojaUnidades);
            var wsRepuestos = BuscarHoja(workbook, HojaRepuestos);
            var wsCatalogo = BuscarHoja(workbook, HojaCatalogo);

            if (wsClientes is null)
            {
                errores.Add(new ErrorFilaImportacionDto(
                    Hoja: HojaClientes,
                    Fila: 1,
                    Columna: "Hoja",
                    ValorLeido: null,
                    Mensaje: $"No se encontró la hoja obligatoria '{HojaClientes}' en el archivo Excel."));
            }

            if (wsUnidades is null && wsRepuestos is null && wsCatalogo is null)
            {
                errores.Add(new ErrorFilaImportacionDto(
                    Hoja: "Archivo",
                    Fila: 1,
                    Columna: "Hoja",
                    ValorLeido: null,
                    Mensaje: $"El archivo debe contener además de '{HojaClientes}' al menos una de las hojas '{HojaUnidades}' o '{HojaRepuestos}'."));
            }

            // Rechazar hojas no autorizadas
            var hojasPermitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizarClaveTexto(HojaClientes),
                NormalizarClaveTexto(HojaUnidades),
                NormalizarClaveTexto(HojaRepuestos),
                NormalizarClaveTexto(HojaCatalogo)
            };

            foreach (var sheet in workbook.Worksheets)
            {
                var claveHoja = NormalizarClaveTexto(sheet.Name);
                if (!hojasPermitidas.Contains(claveHoja))
                {
                    errores.Add(new ErrorFilaImportacionDto(
                        Hoja: sheet.Name,
                        Fila: 1,
                        Columna: "Hoja",
                        ValorLeido: sheet.Name,
                        Mensaje: $"La hoja '{sheet.Name}' no está permitida. Hojas autorizadas: {HojaClientes}, {HojaUnidades}, {HojaRepuestos}."));
                }
            }

            if (errores.Count > 0)
            {
                return CrearResultadoVacioConErrores(nombreArchivo, categoriaId, modoDuplicados, soloValidacion, ahora, errores);
            }

            // Validar cabeceras exactas de las hojas presentes
            string[] colsClientes = ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"];
            string[] colsUnidades = ["DocumentoPropietario", "TipoUnidad", "Placa", "NumeroSerieVIN", "NumeroMotor", "Marca", "Modelo", "Anio", "TipoMedidor", "LecturaMedidor"];
            string[] colsRepuestosObligatorias = ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"];
            string[] colsRepuestosOpcionales = ["Marca"];
            string[] colsCatalogo = ["Codigo", "Nombre"];

            var mapaColsClientes = ValidarYMapearCabeceras(wsClientes!, HojaClientes, colsClientes, null, permitirColumnasExtra: false, errores);

            Dictionary<string, int>? mapaColsUnidades = null;
            if (wsUnidades is not null)
            {
                mapaColsUnidades = ValidarYMapearCabeceras(wsUnidades, HojaUnidades, colsUnidades, null, permitirColumnasExtra: false, errores);
            }

            Dictionary<string, int>? mapaColsRepuestos = null;
            if (wsRepuestos is not null)
            {
                mapaColsRepuestos = ValidarYMapearCabeceras(wsRepuestos, HojaRepuestos, colsRepuestosObligatorias, colsRepuestosOpcionales, permitirColumnasExtra: false, errores);
            }

            Dictionary<string, int>? mapaColsCatalogo = null;
            if (wsCatalogo is not null && wsRepuestos is null)
            {
                mapaColsCatalogo = ValidarYMapearCabeceras(wsCatalogo, HojaCatalogo, colsCatalogo, null, permitirColumnasExtra: false, errores);
            }

            if (errores.Count > 0)
            {
                return CrearResultadoVacioConErrores(nombreArchivo, categoriaId, modoDuplicados, soloValidacion, ahora, errores);
            }

            // Consultas de solo lectura (AsNoTracking) para verificar duplicados y resolver propietarios en BD
            var clientesBdList = await _context.Clientes
                .AsNoTracking()
                .Where(c => c.Activo && (c.NumeroDocumento != null || c.DocumentoIdentidad != null))
                .Select(c => new { c.Id, Documento = (c.NumeroDocumento ?? c.DocumentoIdentidad)!.Trim() })
                .ToListAsync(ct);

            var mapaClientesBd = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in clientesBdList)
            {
                if (!string.IsNullOrWhiteSpace(c.Documento) && !mapaClientesBd.ContainsKey(c.Documento))
                {
                    mapaClientesBd[c.Documento] = c.Id;
                }
            }

            var codigosProductosEnBd = new HashSet<string>(
                await _context.Productos.AsNoTracking().Where(p => p.Activo).Select(p => p.Codigo.Trim()).ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            var vehiculosBdList = await _context.Vehiculos
                .AsNoTracking()
                .Where(v => v.Activo && ((v.Placa != null && v.Placa != "") || (v.NumeroSerieVIN != null && v.NumeroSerieVIN != "")))
                .Select(v => new { Placa = v.Placa != null ? v.Placa.Trim() : null, VIN = v.NumeroSerieVIN != null ? v.NumeroSerieVIN.Trim() : null })
                .ToListAsync(ct);

            var placasEnBd = new HashSet<string>(
                vehiculosBdList.Where(v => !string.IsNullOrWhiteSpace(v.Placa)).Select(v => v.Placa!),
                StringComparer.OrdinalIgnoreCase);

            var vinsEnBd = new HashSet<string>(
                vehiculosBdList.Where(v => !string.IsNullOrWhiteSpace(v.VIN)).Select(v => v.VIN!),
                StringComparer.OrdinalIgnoreCase);

            var categoriasBdActivas = await _context.CategoriasProducto
                .AsNoTracking()
                .Where(c => c.Activo)
                .Select(c => new { c.Id, Nombre = c.Nombre != null ? c.Nombre.Trim() : string.Empty })
                .ToListAsync(ct);

            var mapaCategoriasBd = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var categoriasBdAmbiguas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var grupo in categoriasBdActivas.Where(c => !string.IsNullOrWhiteSpace(c.Nombre)).GroupBy(c => c.Nombre, StringComparer.OrdinalIgnoreCase))
            {
                var ids = grupo.Select(x => x.Id).Distinct().ToList();
                if (ids.Count > 1)
                {
                    categoriasBdAmbiguas.Add(grupo.Key);
                }
                else
                {
                    mapaCategoriasBd[grupo.Key] = ids[0];
                }
            }

            // -----------------------------------------------------------------
            // 1. Validar Hoja Clientes
            // -----------------------------------------------------------------
            var clientesValidos = new List<FilaClienteValidada>();
            var mapaDocumentoAClienteId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var documentosEnHojaClientes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int filasLeidasClientes = 0;
            int filasConErrorClientes = 0;
            int filasOmitidasClientes = 0;

            int ultimaFilaClientes = wsClientes!.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= ultimaFilaClientes; r++)
            {
                var row = wsClientes.Row(r);
                if (FilaEstaVacia(row, mapaColsClientes.Values))
                    continue;

                filasLeidasClientes++;
                int erroresAntes = errores.Count;

                var rawTipoDoc = LeerTexto(row, mapaColsClientes["TipoDocumento"]);
                var rawNumDoc = LeerTextoDocumentoOCodigo(row, mapaColsClientes["NumeroDocumento"]);
                var rawNombre = LeerTexto(row, mapaColsClientes["NombreCompleto"]);
                var rawTelefono = LeerTextoDocumentoOCodigo(row, mapaColsClientes["Telefono"]);
                var rawEmail = LeerTexto(row, mapaColsClientes["Email"]);
                var rawDireccion = LeerTexto(row, mapaColsClientes["Direccion"]);

                TipoDocumentoCliente tipoDoc = TipoDocumentoCliente.DNI;
                var claveTipoDoc = NormalizarClaveTexto(rawTipoDoc);
                bool tipoDocValido = true;

                if (string.IsNullOrWhiteSpace(rawTipoDoc))
                {
                    tipoDocValido = false;
                    errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "TipoDocumento", rawTipoDoc, "El TipoDocumento es obligatorio (DNI, RUC o CE)."));
                }
                else if (claveTipoDoc == "DNI")
                {
                    tipoDoc = TipoDocumentoCliente.DNI;
                }
                else if (claveTipoDoc == "RUC")
                {
                    tipoDoc = TipoDocumentoCliente.RUC;
                }
                else if (claveTipoDoc is "CE" or "CARNETEXTRANJERIA" or "OTRO")
                {
                    tipoDoc = TipoDocumentoCliente.Otro;
                }
                else
                {
                    tipoDocValido = false;
                    errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "TipoDocumento", rawTipoDoc, "TipoDocumento inválido. Valores permitidos: DNI, RUC, CE."));
                }

                bool documentoEnBd = false;
                if (string.IsNullOrWhiteSpace(rawNumDoc))
                {
                    errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "NumeroDocumento", rawNumDoc, "El NumeroDocumento es obligatorio."));
                }
                else
                {
                    if (tipoDocValido)
                    {
                        if (claveTipoDoc == "DNI" && !Regex.IsMatch(rawNumDoc, @"^\d{8}$"))
                        {
                            errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "NumeroDocumento", rawNumDoc, "El DNI debe contener exactamente 8 dígitos numéricos."));
                        }
                        else if (claveTipoDoc == "RUC" && !Regex.IsMatch(rawNumDoc, @"^\d{11}$"))
                        {
                            errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "NumeroDocumento", rawNumDoc, "El RUC debe contener exactamente 11 dígitos numéricos."));
                        }
                        else if (claveTipoDoc is "CE" or "CARNETEXTRANJERIA" or "OTRO" && !Regex.IsMatch(rawNumDoc, @"^[A-Za-z0-9\-]{6,15}$"))
                        {
                            errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "NumeroDocumento", rawNumDoc, "El Carnet de Extranjería / Documento debe tener entre 6 y 15 caracteres alfanuméricos."));
                        }
                    }

                    // Duplicados dentro del mismo archivo Excel SIEMPRE son un error
                    if (documentosEnHojaClientes.TryGetValue(rawNumDoc, out int filaPrevia))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "NumeroDocumento", rawNumDoc, $"El número de documento '{rawNumDoc}' está duplicado en la hoja Clientes (ya aparece en la fila {filaPrevia})."));
                    }
                    else
                    {
                        documentosEnHojaClientes[rawNumDoc] = r;
                    }

                    // Duplicados contra la base de datos
                    if (mapaClientesBd.TryGetValue(rawNumDoc, out var idClienteExistente))
                    {
                        documentoEnBd = true;
                        if (!omitirExistentes)
                        {
                            errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "NumeroDocumento", rawNumDoc, $"Ya existe un cliente activo en la base de datos con el número de documento '{rawNumDoc}'."));
                        }
                        else
                        {
                            // En modo OmitirExistentes, vinculamos al cliente preexistente para que sus unidades puedan asociarse
                            mapaDocumentoAClienteId[rawNumDoc] = idClienteExistente;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(rawNombre))
                {
                    errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "NombreCompleto", rawNombre, "El NombreCompleto es obligatorio."));
                }
                else if (rawNombre.Length > 200)
                {
                    errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "NombreCompleto", rawNombre, "El NombreCompleto no puede exceder los 200 caracteres."));
                }

                if (!string.IsNullOrWhiteSpace(rawTelefono))
                {
                    var soloDigitos = Regex.Replace(rawTelefono, @"[\s\-\+\(\)]", "");
                    if (!Regex.IsMatch(soloDigitos, @"^\d{7,15}$"))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "Telefono", rawTelefono, "El formato de teléfono es inválido (debe contener entre 7 y 15 dígitos)."));
                    }
                }

                if (!string.IsNullOrWhiteSpace(rawEmail) && !EsEmailValido(rawEmail))
                {
                    errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "Email", rawEmail, "El formato del correo electrónico no es válido."));
                }

                if (errores.Count > erroresAntes)
                {
                    filasConErrorClientes++;
                }
                else if (documentoEnBd && omitirExistentes)
                {
                    filasOmitidasClientes++;
                }
                else
                {
                    var nuevoId = Guid.NewGuid();
                    mapaDocumentoAClienteId[rawNumDoc] = nuevoId;

                    clientesValidos.Add(new FilaClienteValidada(
                        nuevoId,
                        r,
                        tipoDoc,
                        rawNumDoc,
                        rawNombre,
                        string.IsNullOrWhiteSpace(rawTelefono) ? null : rawTelefono,
                        string.IsNullOrWhiteSpace(rawEmail) ? null : rawEmail,
                        string.IsNullOrWhiteSpace(rawDireccion) ? null : rawDireccion));
                }
            }

            // -----------------------------------------------------------------
            // 2. Validar Hoja Unidades (si existe)
            // -----------------------------------------------------------------
            var unidadesValidas = new List<FilaUnidadValidada>();
            var placasEnHojaUnidades = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var vinsEnHojaUnidades = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int filasLeidasUnidades = 0;
            int filasConErrorUnidades = 0;
            int filasOmitidasUnidades = 0;

            if (wsUnidades is not null && mapaColsUnidades is not null)
            {
                int ultimaFilaUnidades = wsUnidades.LastRowUsed()?.RowNumber() ?? 1;
                for (int r = 2; r <= ultimaFilaUnidades; r++)
                {
                    var row = wsUnidades.Row(r);
                    if (FilaEstaVacia(row, mapaColsUnidades.Values))
                        continue;

                    filasLeidasUnidades++;
                    int erroresAntes = errores.Count;

                    var rawDocProp = LeerTextoDocumentoOCodigo(row, mapaColsUnidades["DocumentoPropietario"]);
                    var rawTipoUnidad = LeerTexto(row, mapaColsUnidades["TipoUnidad"]);
                    var rawPlaca = LeerTexto(row, mapaColsUnidades["Placa"]);
                    var rawVIN = LeerTextoDocumentoOCodigo(row, mapaColsUnidades["NumeroSerieVIN"]);
                    var rawMotor = LeerTexto(row, mapaColsUnidades["NumeroMotor"]);
                    var rawMarca = LeerTexto(row, mapaColsUnidades["Marca"]);
                    var rawModelo = LeerTexto(row, mapaColsUnidades["Modelo"]);
                    var rawAnio = LeerTextoDocumentoOCodigo(row, mapaColsUnidades["Anio"]);
                    var rawTipoMedidor = LeerTexto(row, mapaColsUnidades["TipoMedidor"]);

                    // Propietario: resolver contra mapaDocumentoAClienteId o mapaClientesBd
                    Guid clienteIdResuelto = Guid.Empty;
                    if (string.IsNullOrWhiteSpace(rawDocProp))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "DocumentoPropietario", rawDocProp, "El DocumentoPropietario es obligatorio."));
                    }
                    else if (mapaDocumentoAClienteId.TryGetValue(rawDocProp, out var cId))
                    {
                        clienteIdResuelto = cId;
                    }
                    else if (mapaClientesBd.TryGetValue(rawDocProp, out var dbClienteId))
                    {
                        clienteIdResuelto = dbClienteId;
                        mapaDocumentoAClienteId[rawDocProp] = dbClienteId;
                    }
                    else
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "DocumentoPropietario", rawDocProp, $"No se encontró ningún cliente registrado con el documento '{rawDocProp}' ni en la base de datos ni en la hoja Clientes."));
                    }

                    // TipoUnidad
                    TipoUnidad tipoUnidad = TipoUnidad.Motocicleta;
                    if (string.IsNullOrWhiteSpace(rawTipoUnidad))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "TipoUnidad", rawTipoUnidad, "El TipoUnidad es obligatorio (Motocicleta, Cuatrimoto, MotoAcuatica, Generador, Otro)."));
                    }
                    else if (!TryParseTipoUnidad(rawTipoUnidad, out tipoUnidad))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "TipoUnidad", rawTipoUnidad, $"TipoUnidad inválido ('{rawTipoUnidad}'). Valores permitidos: Motocicleta, Cuatrimoto, MotoAcuatica, Generador, Otro."));
                    }

                    // Placa y Serie/VIN
                    string? placaLimpia = string.IsNullOrWhiteSpace(rawPlaca) ? null : rawPlaca.Trim().ToUpperInvariant();
                    string? vinLimpio = string.IsNullOrWhiteSpace(rawVIN) ? null : rawVIN.Trim().ToUpperInvariant();

                    if (tipoUnidad == TipoUnidad.Motocicleta && string.IsNullOrWhiteSpace(placaLimpia))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "Placa", rawPlaca, "La Placa es obligatoria para unidades de tipo Motocicleta."));
                    }
                    else if (string.IsNullOrWhiteSpace(placaLimpia) && string.IsNullOrWhiteSpace(vinLimpio))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "NumeroSerieVIN", rawVIN, "Debe especificar al menos la Placa o el NumeroSerieVIN (obligatorio para unidades sin placa)."));
                    }

                    bool unidadEnBd = false;

                    // Validación de duplicados de Placa
                    if (!string.IsNullOrWhiteSpace(placaLimpia))
                    {
                        if (placasEnHojaUnidades.TryGetValue(placaLimpia, out int filaPlacaPrev))
                        {
                            errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "Placa", placaLimpia, $"La placa '{placaLimpia}' está duplicada en la hoja Unidades (ya aparece en la fila {filaPlacaPrev})."));
                        }
                        else
                        {
                            placasEnHojaUnidades[placaLimpia] = r;
                        }

                        if (placasEnBd.Contains(placaLimpia))
                        {
                            unidadEnBd = true;
                            if (!omitirExistentes)
                            {
                                errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "Placa", placaLimpia, $"Ya existe un vehículo activo en la base de datos con la placa '{placaLimpia}'."));
                            }
                        }
                    }

                    // Validación de duplicados de VIN
                    if (!string.IsNullOrWhiteSpace(vinLimpio))
                    {
                        if (vinsEnHojaUnidades.TryGetValue(vinLimpio, out int filaVinPrev))
                        {
                            errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "NumeroSerieVIN", vinLimpio, $"El número de serie/VIN '{vinLimpio}' está duplicado en la hoja Unidades (ya aparece en la fila {filaVinPrev})."));
                        }
                        else
                        {
                            vinsEnHojaUnidades[vinLimpio] = r;
                        }

                        if (vinsEnBd.Contains(vinLimpio))
                        {
                            unidadEnBd = true;
                            if (!omitirExistentes)
                            {
                                errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "NumeroSerieVIN", vinLimpio, $"Ya existe un vehículo activo en la base de datos con el número de serie/VIN '{vinLimpio}'."));
                            }
                        }
                    }

                    // Marca y Modelo
                    if (string.IsNullOrWhiteSpace(rawMarca))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "Marca", rawMarca, "La Marca de la unidad es obligatoria."));
                    }

                    if (string.IsNullOrWhiteSpace(rawModelo))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "Modelo", rawModelo, "El Modelo de la unidad es obligatorio."));
                    }

                    // Año opcional
                    int? anio = null;
                    if (!string.IsNullOrWhiteSpace(rawAnio))
                    {
                        if (LeerEntero(row, mapaColsUnidades["Anio"], out int aVal))
                        {
                            int anioMax = DateTime.UtcNow.Year + 2;
                            if (aVal < 1950 || aVal > anioMax)
                            {
                                errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "Anio", rawAnio, $"El Año de la unidad debe encontrarse entre 1950 y {anioMax}."));
                            }
                            else
                            {
                                anio = aVal;
                            }
                        }
                        else
                        {
                            errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "Anio", rawAnio, "El Año debe ser un número entero válido."));
                        }
                    }

                    // TipoMedidor
                    TipoMedidor tipoMedidor = TipoMedidor.Kilometraje;
                    if (string.IsNullOrWhiteSpace(rawTipoMedidor))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "TipoMedidor", rawTipoMedidor, "El TipoMedidor es obligatorio (Kilometros u Horas)."));
                    }
                    else if (!TryParseTipoMedidor(rawTipoMedidor, out tipoMedidor))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "TipoMedidor", rawTipoMedidor, $"TipoMedidor inválido ('{rawTipoMedidor}'). Valores permitidos: Kilometros, Horas."));
                    }

                    // LecturaMedidor
                    decimal lectura = 0m;
                    if (!LeerDecimal(row, mapaColsUnidades["LecturaMedidor"], out lectura) || lectura < 0m)
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaUnidades, r, "LecturaMedidor", LeerTexto(row, mapaColsUnidades["LecturaMedidor"]), "La LecturaMedidor debe ser un valor numérico mayor o igual a 0."));
                    }

                    if (errores.Count > erroresAntes)
                    {
                        filasConErrorUnidades++;
                    }
                    else if (unidadEnBd && omitirExistentes)
                    {
                        filasOmitidasUnidades++;
                    }
                    else
                    {
                        unidadesValidas.Add(new FilaUnidadValidada(
                            r,
                            clienteIdResuelto,
                            rawDocProp,
                            tipoUnidad,
                            placaLimpia,
                            vinLimpio,
                            string.IsNullOrWhiteSpace(rawMotor) ? null : rawMotor.Trim(),
                            rawMarca.Trim(),
                            rawModelo.Trim(),
                            anio,
                            tipoMedidor,
                            lectura
                        ));
                    }
                }
            }

            // -----------------------------------------------------------------
            // 3. Validar Hoja Repuestos (o Hoja Catalogo legada)
            // -----------------------------------------------------------------
            var repuestosValidos = new List<FilaRepuestoValidada>();
            var catalogoValidos = new List<FilaCatalogoValidada>();
            var categoriasNuevas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codigosEnHojaRepuestos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int filasLeidasRepuestos = 0;
            int filasConErrorRepuestos = 0;
            int filasOmitidasRepuestos = 0;

            if (wsRepuestos is not null && mapaColsRepuestos is not null)
            {
                int ultimaFilaRepuestos = wsRepuestos.LastRowUsed()?.RowNumber() ?? 1;
                for (int r = 2; r <= ultimaFilaRepuestos; r++)
                {
                    var row = wsRepuestos.Row(r);
                    if (FilaEstaVacia(row, mapaColsRepuestos.Values))
                        continue;

                    filasLeidasRepuestos++;
                    int erroresAntes = errores.Count;

                    var rawCodigo = LeerTextoDocumentoOCodigo(row, mapaColsRepuestos["Codigo"]);
                    var rawNombre = LeerTexto(row, mapaColsRepuestos["Nombre"]);
                    var rawCategoria = LeerTexto(row, mapaColsRepuestos["Categoria"]);
                    var rawUnidad = LeerTexto(row, mapaColsRepuestos["UnidadMedida"]);

                    string? rawMarca = null;
                    if (mapaColsRepuestos.TryGetValue("Marca", out int colMarca))
                    {
                        rawMarca = LeerTexto(row, colMarca);
                    }

                    bool productoEnBd = false;
                    if (string.IsNullOrWhiteSpace(rawCodigo))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaRepuestos, r, "Codigo", rawCodigo, "El Codigo del repuesto es obligatorio."));
                    }
                    else
                    {
                        if (codigosEnHojaRepuestos.TryGetValue(rawCodigo, out int filaCodPrevia))
                        {
                            errores.Add(new ErrorFilaImportacionDto(HojaRepuestos, r, "Codigo", rawCodigo, $"El código '{rawCodigo}' está duplicado en la hoja Repuestos (ya aparece en la fila {filaCodPrevia})."));
                        }
                        else
                        {
                            codigosEnHojaRepuestos[rawCodigo] = r;
                        }

                        if (codigosProductosEnBd.Contains(rawCodigo))
                        {
                            productoEnBd = true;
                            if (!omitirExistentes)
                            {
                                errores.Add(new ErrorFilaImportacionDto(HojaRepuestos, r, "Codigo", rawCodigo, $"Ya existe un producto registrado en la base de datos con el código '{rawCodigo}'."));
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(rawNombre))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaRepuestos, r, "Nombre", rawNombre, "El Nombre del repuesto es obligatorio."));
                    }

                    if (string.IsNullOrWhiteSpace(rawCategoria))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaRepuestos, r, "Categoria", rawCategoria, "La Categoria del repuesto es obligatoria."));
                    }
                    else
                    {
                        var catLimpia = rawCategoria.Trim();
                        if (categoriasBdAmbiguas.Contains(catLimpia))
                        {
                            errores.Add(new ErrorFilaImportacionDto(
                                HojaRepuestos,
                                r,
                                "Categoria",
                                catLimpia,
                                $"Existe ambigüedad en el catálogo del sistema: se encontraron múltiples categorías activas con el nombre '{catLimpia}'. Resuelva la duplicidad en el sistema antes de importar."));
                        }
                        else if (!mapaCategoriasBd.ContainsKey(catLimpia))
                        {
                            categoriasNuevas.Add(catLimpia);
                        }
                    }

                    // Costo >= 0
                    decimal costo = 0m;
                    if (!LeerDecimal(row, mapaColsRepuestos["Costo"], out costo) || costo < 0m)
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaRepuestos, r, "Costo", LeerTexto(row, mapaColsRepuestos["Costo"]), "El Costo debe ser un valor numérico decimal mayor o igual a 0."));
                    }

                    // PrecioVenta >= 0
                    decimal precioVenta = 0m;
                    if (!LeerDecimal(row, mapaColsRepuestos["PrecioVenta"], out precioVenta) || precioVenta < 0m)
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaRepuestos, r, "PrecioVenta", LeerTexto(row, mapaColsRepuestos["PrecioVenta"]), "El PrecioVenta debe ser un valor numérico decimal mayor o igual a 0."));
                    }

                    // StockActual >= 0
                    int stockActual = 0;
                    if (!LeerEntero(row, mapaColsRepuestos["StockActual"], out stockActual) || stockActual < 0)
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaRepuestos, r, "StockActual", LeerTexto(row, mapaColsRepuestos["StockActual"]), "El StockActual debe ser un número entero mayor o igual a 0."));
                    }

                    // StockMinimo opcional
                    int? stockMinimo = null;
                    var cellMin = row.Cell(mapaColsRepuestos["StockMinimo"]);
                    if (!cellMin.IsEmpty() && !string.IsNullOrWhiteSpace(cellMin.GetFormattedString()))
                    {
                        if (LeerEntero(row, mapaColsRepuestos["StockMinimo"], out int sMinVal) && sMinVal >= 0)
                        {
                            stockMinimo = sMinVal;
                        }
                        else
                        {
                            errores.Add(new ErrorFilaImportacionDto(HojaRepuestos, r, "StockMinimo", LeerTexto(row, mapaColsRepuestos["StockMinimo"]), "El StockMinimo debe ser un número entero mayor o igual a 0 si se especifica."));
                        }
                    }

                    if (errores.Count > erroresAntes)
                    {
                        filasConErrorRepuestos++;
                    }
                    else if (productoEnBd && omitirExistentes)
                    {
                        filasOmitidasRepuestos++;
                    }
                    else
                    {
                        repuestosValidos.Add(new FilaRepuestoValidada(
                            r,
                            rawCodigo.Trim(),
                            rawNombre.Trim(),
                            rawCategoria.Trim(),
                            string.IsNullOrWhiteSpace(rawUnidad) ? "unidad" : rawUnidad.Trim(),
                            costo,
                            precioVenta,
                            stockActual,
                            stockMinimo,
                            string.IsNullOrWhiteSpace(rawMarca) ? null : rawMarca.Trim()
                        ));
                    }
                }
            }
            else if (wsCatalogo is not null && mapaColsCatalogo is not null)
            {
                // Soporte de hoja Catalogo legada (Codigo y Nombre)
                int ultimaFilaCatalogo = wsCatalogo.LastRowUsed()?.RowNumber() ?? 1;
                for (int r = 2; r <= ultimaFilaCatalogo; r++)
                {
                    var row = wsCatalogo.Row(r);
                    if (FilaEstaVacia(row, mapaColsCatalogo.Values))
                        continue;

                    filasLeidasRepuestos++;
                    int erroresAntes = errores.Count;

                    var rawCodigo = LeerTextoDocumentoOCodigo(row, mapaColsCatalogo["Codigo"]);
                    var rawNombre = LeerTexto(row, mapaColsCatalogo["Nombre"]);

                    bool productoEnBd = false;
                    if (string.IsNullOrWhiteSpace(rawCodigo))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaCatalogo, r, "Codigo", rawCodigo, "El Codigo del producto es obligatorio."));
                    }
                    else
                    {
                        if (codigosEnHojaRepuestos.TryGetValue(rawCodigo, out int filaCodPrevia))
                        {
                            errores.Add(new ErrorFilaImportacionDto(HojaCatalogo, r, "Codigo", rawCodigo, $"El código '{rawCodigo}' está duplicado en la hoja Catalogo (ya aparece en la fila {filaCodPrevia})."));
                        }
                        else
                        {
                            codigosEnHojaRepuestos[rawCodigo] = r;
                        }

                        if (codigosProductosEnBd.Contains(rawCodigo))
                        {
                            productoEnBd = true;
                            if (!omitirExistentes)
                            {
                                errores.Add(new ErrorFilaImportacionDto(HojaCatalogo, r, "Codigo", rawCodigo, $"Ya existe un producto registrado en la base de datos con el código '{rawCodigo}'."));
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(rawNombre))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaCatalogo, r, "Nombre", rawNombre, "El Nombre del producto es obligatorio."));
                    }

                    if (errores.Count > erroresAntes)
                    {
                        filasConErrorRepuestos++;
                    }
                    else if (productoEnBd && omitirExistentes)
                    {
                        filasOmitidasRepuestos++;
                    }
                    else
                    {
                        catalogoValidos.Add(new FilaCatalogoValidada(
                            r,
                            rawCodigo.Trim(),
                            rawNombre.Trim()));
                    }
                }

                // Para Catalogo legado, requiere categoriaId explícito si hay productos nuevos
                if (catalogoValidos.Count > 0)
                {
                    if (!categoriaId.HasValue || categoriaId.Value == Guid.Empty)
                    {
                        errores.Add(new ErrorFilaImportacionDto(
                            Hoja: HojaCatalogo,
                            Fila: 0,
                            Columna: "CategoriaId",
                            ValorLeido: null,
                            Mensaje: "Debe proporcionar un 'categoriaId' explícito de una categoría existente y activa cuando la hoja Catalogo contiene productos nuevos."));
                    }
                    else
                    {
                        bool categoriaExisteActiva = await _context.CategoriasProducto
                            .AsNoTracking()
                            .AnyAsync(c => c.Id == categoriaId.Value && c.Activo, ct);

                        if (!categoriaExisteActiva)
                        {
                            errores.Add(new ErrorFilaImportacionDto(
                                Hoja: HojaCatalogo,
                                Fila: 0,
                                Columna: "CategoriaId",
                                ValorLeido: categoriaId.Value.ToString(),
                                Mensaje: $"La categoría con Id '{categoriaId.Value}' no existe o está inactiva."));
                        }
                    }
                }
            }

            if (filasLeidasClientes == 0 && filasLeidasUnidades == 0 && filasLeidasRepuestos == 0)
            {
                errores.Add(new ErrorFilaImportacionDto(
                    Hoja: "Archivo",
                    Fila: 2,
                    Columna: "Datos",
                    ValorLeido: null,
                    Mensaje: "El archivo no contiene ninguna fila con datos para importar en las hojas Clientes, Unidades o Repuestos."));
            }

            bool exitoso = errores.Count == 0;
            bool cuentaCreados = exitoso && !soloValidacion;

            var resumenClientes = new ResumenHojaImportacionDto(
                TotalFilasLeidas: filasLeidasClientes,
                FilasValidas: clientesValidos.Count,
                FilasConError: filasConErrorClientes,
                RegistrosCreados: cuentaCreados ? clientesValidos.Count : 0,
                RegistrosOmitidos: filasOmitidasClientes);

            var resumenUnidades = wsUnidades is not null
                ? new ResumenHojaImportacionDto(
                    TotalFilasLeidas: filasLeidasUnidades,
                    FilasValidas: unidadesValidas.Count,
                    FilasConError: filasConErrorUnidades,
                    RegistrosCreados: cuentaCreados ? unidadesValidas.Count : 0,
                    RegistrosOmitidos: filasOmitidasUnidades)
                : new ResumenHojaImportacionDto(0, 0, 0, 0, 0);

            int repuestosOmitidos = filasOmitidasRepuestos;
            int repuestosCreados = cuentaCreados ? (repuestosValidos.Count + catalogoValidos.Count) : 0;
            int repuestosValidosTotal = repuestosValidos.Count + catalogoValidos.Count;

            var resumenRepuestos = new ResumenHojaImportacionDto(
                TotalFilasLeidas: filasLeidasRepuestos,
                FilasValidas: repuestosValidosTotal,
                FilasConError: filasConErrorRepuestos,
                RegistrosCreados: repuestosCreados,
                RegistrosOmitidos: repuestosOmitidos);

            var resultadoDto = new ResultadoImportacionExcelDto(
                Exitoso: exitoso,
                SoloValidacion: soloValidacion,
                NombreArchivo: nombreArchivo,
                FechaProceso: ahora,
                CategoriaId: categoriaId,
                Clientes: resumenClientes,
                Catalogo: resumenRepuestos,
                Errores: errores,
                Unidades: resumenUnidades,
                Repuestos: resumenRepuestos,
                ModoDuplicados: modoDuplicados);

            return new ResultadoParseoInterno(
                resultadoDto,
                clientesValidos,
                unidadesValidas,
                repuestosValidos,
                catalogoValidos,
                categoriasNuevas);
        }
    }

    // =========================================================================
    // Generación de Hojas de la Plantilla Estandarizada de 3 Hojas
    // =========================================================================

    private static void GenerarHojaClientes(XLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add(HojaClientes);
        string[] headers = ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"];
        EstilizarCabeceras(ws, headers);

        // Columnas de texto para conservar ceros a la izquierda
        ws.Column(2).Style.NumberFormat.Format = "@";
        ws.Column(4).Style.NumberFormat.Format = "@";

        // Fila ejemplo 1: Persona Natural (DNI)
        ws.Cell(2, 1).Value = "DNI";
        ws.Cell(2, 2).Value = "45871236";
        ws.Cell(2, 3).Value = "Carlos Alberto Mendoza Ríos";
        ws.Cell(2, 4).Value = "987654321";
        ws.Cell(2, 5).Value = "carlos.mendoza@ejemplo.pe";
        ws.Cell(2, 6).Value = "Av. Ejército 450, Cayma, Arequipa";

        // Fila ejemplo 2: Empresa (RUC)
        ws.Cell(3, 1).Value = "RUC";
        ws.Cell(3, 2).Value = "20601234567";
        ws.Cell(3, 3).Value = "Inversiones Náuticas del Sur S.A.C.";
        ws.Cell(3, 4).Value = "954112233";
        ws.Cell(3, 5).Value = "logistica@nauticassur.pe";
        ws.Cell(3, 6).Value = "Calle Misti 120, Yanahuara, Arequipa";

        ws.Columns().AdjustToContents();
    }

    private static void GenerarHojaUnidades(XLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add(HojaUnidades);
        string[] headers = ["DocumentoPropietario", "TipoUnidad", "Placa", "NumeroSerieVIN", "NumeroMotor", "Marca", "Modelo", "Anio", "TipoMedidor", "LecturaMedidor"];
        EstilizarCabeceras(ws, headers);

        ws.Column(1).Style.NumberFormat.Format = "@";
        ws.Column(3).Style.NumberFormat.Format = "@";
        ws.Column(4).Style.NumberFormat.Format = "@";
        ws.Column(5).Style.NumberFormat.Format = "@";
        ws.Column(8).Style.NumberFormat.Format = "0";
        ws.Column(10).Style.NumberFormat.Format = "#,##0.00";

        // Fila ejemplo 1: Motocicleta con placa y odómetro en Km
        ws.Cell(2, 1).Value = "45871236";
        ws.Cell(2, 2).Value = "Motocicleta";
        ws.Cell(2, 3).Value = "1234-5A";
        ws.Cell(2, 4).Value = "9C6KE0480N0001234";
        ws.Cell(2, 5).Value = "E3T4E-012345";
        ws.Cell(2, 6).Value = "Yamaha";
        ws.Cell(2, 7).Value = "YZF-R3";
        ws.Cell(2, 8).Value = 2023;
        ws.Cell(2, 9).Value = "Kilometros";
        ws.Cell(2, 10).Value = 4500;

        // Fila ejemplo 2: Moto Acuática sin placa con horómetro
        ws.Cell(3, 1).Value = "20601234567";
        ws.Cell(3, 2).Value = "MotoAcuatica";
        ws.Cell(3, 3).Value = "";
        ws.Cell(3, 4).Value = "YDV12345L223";
        ws.Cell(3, 5).Value = "6EY-1002345";
        ws.Cell(3, 6).Value = "Yamaha";
        ws.Cell(3, 7).Value = "WaveRunner VX-C";
        ws.Cell(3, 8).Value = 2023;
        ws.Cell(3, 9).Value = "Horas";
        ws.Cell(3, 10).Value = 120.5;

        // Fila ejemplo 3: Generador sin placa con horómetro
        ws.Cell(4, 1).Value = "20601234567";
        ws.Cell(4, 2).Value = "Generador";
        ws.Cell(4, 3).Value = "";
        ws.Cell(4, 4).Value = "7PB-2001456";
        ws.Cell(4, 5).Value = "MZ175-987654";
        ws.Cell(4, 6).Value = "Yamaha";
        ws.Cell(4, 7).Value = "EF2200iS";
        ws.Cell(4, 8).Value = 2022;
        ws.Cell(4, 9).Value = "Horas";
        ws.Cell(4, 10).Value = 35.0;

        ws.Columns().AdjustToContents();
    }

    private static void GenerarHojaRepuestos(XLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add(HojaRepuestos);
        string[] headers = ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"];
        EstilizarCabeceras(ws, headers);

        ws.Column(1).Style.NumberFormat.Format = "@";
        ws.Column(5).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(6).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(7).Style.NumberFormat.Format = "0";
        ws.Column(8).Style.NumberFormat.Format = "0";

        // Fila ejemplo 1
        ws.Cell(2, 1).Value = "90793-38046";
        ws.Cell(2, 2).Value = "Aceite Yamalube 4T 10W-40 Full Sintético";
        ws.Cell(2, 3).Value = "Lubricantes";
        ws.Cell(2, 4).Value = "litro";
        ws.Cell(2, 5).Value = 32.50;
        ws.Cell(2, 6).Value = 48.00;
        ws.Cell(2, 7).Value = 24;
        ws.Cell(2, 8).Value = 6;

        // Fila ejemplo 2
        ws.Cell(3, 1).Value = "5GH-13440-80";
        ws.Cell(3, 2).Value = "Filtro de Aceite Original Yamaha";
        ws.Cell(3, 3).Value = "Filtros";
        ws.Cell(3, 4).Value = "unidad";
        ws.Cell(3, 5).Value = 28.00;
        ws.Cell(3, 6).Value = 42.00;
        ws.Cell(3, 7).Value = 10;
        ws.Cell(3, 8).Value = 4;

        ws.Columns().AdjustToContents();
    }

    private static void EstilizarCabeceras(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
    }

    // =========================================================================
    // Utilidades de Lectura, Normalización y Auditoría
    // =========================================================================

    private static IXLWorksheet? BuscarHoja(XLWorkbook workbook, string nombreEsperado)
    {
        return workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(NormalizarClaveTexto(w.Name), NormalizarClaveTexto(nombreEsperado), StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, int> ValidarYMapearCabeceras(
        IXLWorksheet ws,
        string nombreHoja,
        string[] columnasObligatorias,
        string[]? columnasOpcionales,
        bool permitirColumnasExtra,
        List<ErrorFilaImportacionDto> errores)
    {
        var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var filaCabecera = ws.Row(1);
        int totalColsEsperadas = columnasObligatorias.Length + (columnasOpcionales?.Length ?? 0);
        int ultimaColumna = ws.LastColumnUsed()?.ColumnNumber() ?? totalColsEsperadas;

        var clavesPermitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columnasObligatorias)
        {
            clavesPermitidas.Add(NormalizarClaveTexto(col));
        }
        if (columnasOpcionales is not null)
        {
            foreach (var col in columnasOpcionales)
            {
                clavesPermitidas.Add(NormalizarClaveTexto(col));
            }
        }

        var cabecerasEnHoja = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= Math.Max(ultimaColumna, totalColsEsperadas); col++)
        {
            var texto = filaCabecera.Cell(col).GetFormattedString().Trim();
            if (!string.IsNullOrWhiteSpace(texto))
            {
                var clave = NormalizarClaveTexto(texto);
                if (!cabecerasEnHoja.ContainsKey(clave))
                {
                    cabecerasEnHoja[clave] = col;
                }

                if (!permitirColumnasExtra && !clavesPermitidas.Contains(clave))
                {
                    errores.Add(new ErrorFilaImportacionDto(
                        Hoja: nombreHoja,
                        Fila: 1,
                        Columna: texto,
                        ValorLeido: texto,
                        Mensaje: $"La columna '{texto}' no está permitida en la hoja '{nombreHoja}'. Columnas esperadas: {string.Join(", ", columnasObligatorias)}."));
                }
            }
        }

        foreach (var colEsperada in columnasObligatorias)
        {
            var claveEsperada = NormalizarClaveTexto(colEsperada);
            if (cabecerasEnHoja.TryGetValue(claveEsperada, out int colIndex))
            {
                mapa[colEsperada] = colIndex;
            }
            else
            {
                errores.Add(new ErrorFilaImportacionDto(
                    Hoja: nombreHoja,
                    Fila: 1,
                    Columna: colEsperada,
                    ValorLeido: null,
                    Mensaje: $"Falta la columna obligatoria '{colEsperada}' en el encabezado (fila 1) de la hoja '{nombreHoja}'."));
            }
        }

        if (columnasOpcionales is not null)
        {
            foreach (var colOpcional in columnasOpcionales)
            {
                var claveOpcional = NormalizarClaveTexto(colOpcional);
                if (cabecerasEnHoja.TryGetValue(claveOpcional, out int colIndex))
                {
                    mapa[colOpcional] = colIndex;
                }
            }
        }

        return mapa;
    }

    private static bool FilaEstaVacia(IXLRow row, IEnumerable<int> columnas)
    {
        foreach (var col in columnas)
        {
            var cell = row.Cell(col);
            if (!cell.IsEmpty() && !string.IsNullOrWhiteSpace(cell.GetFormattedString()))
            {
                return false;
            }
        }
        return true;
    }

    private static string LeerTexto(IXLRow row, int col)
    {
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return string.Empty;
        return cell.GetFormattedString().Trim();
    }

    private static string LeerTextoDocumentoOCodigo(IXLRow row, int col)
    {
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return string.Empty;

        if (cell.DataType == XLDataType.Number)
        {
            double val = cell.GetDouble();
            if (Math.Abs(val - Math.Truncate(val)) < 1e-9)
            {
                return ((long)Math.Truncate(val)).ToString(CultureInfo.InvariantCulture);
            }
            return val.ToString(CultureInfo.InvariantCulture);
        }

        return cell.GetFormattedString().Trim();
    }

    private static bool LeerDecimal(IXLRow row, int col, out decimal valor)
    {
        valor = 0m;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return false;

        if (cell.DataType == XLDataType.Number)
        {
            valor = Convert.ToDecimal(cell.GetDouble(), CultureInfo.InvariantCulture);
            return true;
        }

        var texto = cell.GetFormattedString().Trim()
            .Replace("S/", "", StringComparison.OrdinalIgnoreCase)
            .Replace("$", "")
            .Trim();

        if (string.IsNullOrWhiteSpace(texto)) return false;

        if (decimal.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out valor))
        {
            return true;
        }
        if (decimal.TryParse(texto, NumberStyles.Any, new CultureInfo("es-PE"), out valor))
        {
            return true;
        }
        return false;
    }

    private static bool LeerEntero(IXLRow row, int col, out int valor)
    {
        valor = 0;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return false;

        if (cell.DataType == XLDataType.Number)
        {
            double d = cell.GetDouble();
            if (Math.Abs(d - Math.Truncate(d)) < 1e-9)
            {
                valor = (int)Math.Truncate(d);
                return true;
            }
            return false;
        }

        var texto = cell.GetFormattedString().Trim();
        if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out valor))
        {
            return true;
        }
        return false;
    }

    private static bool TryParseTipoUnidad(string raw, out TipoUnidad tipo)
    {
        var clave = NormalizarClaveTexto(raw);
        switch (clave)
        {
            case "MOTOCICLETA":
            case "MOTO":
                tipo = TipoUnidad.Motocicleta;
                return true;
            case "CUATRIMOTO":
            case "ATV":
                tipo = TipoUnidad.Cuatrimoto;
                return true;
            case "MOTOACUATICA":
            case "WAVERUNNER":
            case "JETSKI":
                tipo = TipoUnidad.MotoAcuatica;
                return true;
            case "GENERADOR":
                tipo = TipoUnidad.Generador;
                return true;
            case "OTRO":
                tipo = TipoUnidad.Otro;
                return true;
            default:
                tipo = TipoUnidad.Otro;
                return false;
        }
    }

    private static bool TryParseTipoMedidor(string raw, out TipoMedidor tipo)
    {
        var clave = NormalizarClaveTexto(raw);
        switch (clave)
        {
            case "KILOMETROS":
            case "KILOMETRAJE":
            case "KM":
                tipo = TipoMedidor.Kilometraje;
                return true;
            case "HORAS":
            case "HORASUSO":
            case "HOROMETRO":
                tipo = TipoMedidor.Horas;
                return true;
            default:
                tipo = TipoMedidor.Kilometraje;
                return false;
        }
    }

    private static string NormalizarClaveTexto(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var normalized = input.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (char c in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark && !char.IsWhiteSpace(c) && c != '_' && c != '-')
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }
        return sb.ToString();
    }

    private static bool EsEmailValido(string email)
    {
        try
        {
            var addr = new MailAddress(email.Trim());
            return addr.Address == email.Trim() && email.Contains('.');
        }
        catch
        {
            return false;
        }
    }

    private static ResultadoParseoInterno CrearResultadoVacioConErrores(
        string nombreArchivo,
        Guid? categoriaId,
        string modoDuplicados,
        bool soloValidacion,
        DateTime fechaProceso,
        List<ErrorFilaImportacionDto> errores)
    {
        var vacio = new ResumenHojaImportacionDto(0, 0, 0, 0, 0);
        return new ResultadoParseoInterno(
            new ResultadoImportacionExcelDto(
                Exitoso: false,
                SoloValidacion: soloValidacion,
                NombreArchivo: nombreArchivo,
                FechaProceso: fechaProceso,
                CategoriaId: categoriaId,
                Clientes: vacio,
                Catalogo: vacio,
                Errores: errores,
                Unidades: vacio,
                Repuestos: vacio,
                ModoDuplicados: modoDuplicados),
            [],
            [],
            [],
            [],
            []);
    }

    private async Task RegistrarAuditoriaAsync(
        Guid? usuarioId,
        string accion,
        string nombreArchivo,
        ResultadoImportacionExcelDto resultado,
        CancellationToken ct)
    {
        var detalle = ConstruirPayloadAuditoria(resultado);
        await _auditoriaService.RegistrarEventoAsync(
            usuarioId,
            accion,
            EntidadAuditoria,
            nombreArchivo,
            detalle,
            ct);
    }

    private static EventoAuditoria ConstruirEventoAuditoria(
        Guid? usuarioId,
        string accion,
        string nombreArchivo,
        ResultadoImportacionExcelDto resultado,
        DateTime fecha)
    {
        var detalle = ConstruirPayloadAuditoria(resultado);
        return new EventoAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Fecha = fecha,
            Accion = accion,
            Entidad = EntidadAuditoria,
            EntidadId = nombreArchivo,
            Detalle = JsonSerializer.Serialize(detalle)
        };
    }

    private static object ConstruirPayloadAuditoria(ResultadoImportacionExcelDto resultado)
    {
        return new
        {
            resultado.Exitoso,
            resultado.SoloValidacion,
            resultado.NombreArchivo,
            resultado.CategoriaId,
            resultado.ModoDuplicados,
            ClientesProcesados = resultado.Exitoso ? resultado.Clientes.RegistrosCreados : 0,
            CatalogoProcesados = resultado.Exitoso ? resultado.Catalogo.RegistrosCreados : 0,
            UnidadesProcesadas = resultado.Exitoso ? (resultado.Unidades?.RegistrosCreados ?? 0) : 0,
            RepuestosProcesados = resultado.Exitoso ? (resultado.Repuestos?.RegistrosCreados ?? 0) : 0,
            ClientesOmitidos = resultado.Clientes.RegistrosOmitidos,
            UnidadesOmitidas = resultado.Unidades?.RegistrosOmitidos ?? 0,
            RepuestosOmitidos = resultado.Repuestos?.RegistrosOmitidos ?? 0,
            TotalErrores = resultado.Errores.Count,
            PrimerosErrores = resultado.Errores.Take(20).ToList()
        };
    }

    // =========================================================================
    // Modelos Internos de Filas Validadas
    // =========================================================================

    private sealed record FilaClienteValidada(
        Guid Id,
        int Fila,
        TipoDocumentoCliente TipoDocumento,
        string NumeroDocumento,
        string NombreCompleto,
        string? Telefono,
        string? Email,
        string? Direccion
    );

    private sealed record FilaUnidadValidada(
        int Fila,
        Guid ClienteId,
        string DocumentoPropietario,
        TipoUnidad TipoUnidad,
        string? Placa,
        string? NumeroSerieVIN,
        string? NumeroMotor,
        string Marca,
        string Modelo,
        int? Anio,
        TipoMedidor TipoMedidor,
        decimal LecturaMedidor
    );

    private sealed record FilaRepuestoValidada(
        int Fila,
        string Codigo,
        string Nombre,
        string Categoria,
        string UnidadMedida,
        decimal Costo,
        decimal PrecioVenta,
        int StockActual,
        int? StockMinimo,
        string? Marca
    );

    private sealed record FilaCatalogoValidada(
        int Fila,
        string Codigo,
        string Nombre
    );

    private sealed record ResultadoParseoInterno(
        ResultadoImportacionExcelDto Resultado,
        List<FilaClienteValidada> ClientesValidos,
        List<FilaUnidadValidada> UnidadesValidas,
        List<FilaRepuestoValidada> RepuestosValidos,
        List<FilaCatalogoValidada> CatalogoValidos,
        HashSet<string> CategoriasNuevas
    );
}
