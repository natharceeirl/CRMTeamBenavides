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

    private const string HojaClientes = "Clientes";
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
        GenerarHojaCatalogo(workbook);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<ResultadoImportacionExcelDto> ValidarArchivoAsync(
        Stream archivoStream,
        string nombreArchivo,
        Guid? categoriaId,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        var parseo = await ProcesarYValidarLibroAsync(
            archivoStream,
            nombreArchivo,
            categoriaId,
            soloValidacion: true,
            ct);

        return parseo.Resultado;
    }

    public async Task<ResultadoImportacionExcelDto> EjecutarImportacionAsync(
        Stream archivoStream,
        string nombreArchivo,
        Guid? categoriaId,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        var parseo = await ProcesarYValidarLibroAsync(
            archivoStream,
            nombreArchivo,
            categoriaId,
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
            // Revalidación inmediatamente previa a la inserción si hay productos nuevos en Catalogo
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

            // 1. Crear únicamente clientes nuevos (sin sobrescribir existentes)
            var nuevosClientes = new List<Cliente>(parseo.ClientesValidos.Count);
            foreach (var filaCliente in parseo.ClientesValidos)
            {
                nuevosClientes.Add(new Cliente
                {
                    Id = Guid.NewGuid(),
                    TipoDocumento = filaCliente.TipoDocumento,
                    NumeroDocumento = filaCliente.NumeroDocumento,
                    DocumentoIdentidad = filaCliente.NumeroDocumento,
                    NombreCompleto = filaCliente.NombreCompleto,
                    RazonSocial = filaCliente.TipoDocumento == TipoDocumentoCliente.RUC ? filaCliente.NombreCompleto : null,
                    Telefono = filaCliente.Telefono,
                    Email = filaCliente.Email,
                    Direccion = filaCliente.Direccion,
                    FechaCreacion = ahora,
                    CreadoPorId = usuarioId,
                    Activo = true
                });
            }

            if (nuevosClientes.Count > 0)
            {
                _context.Clientes.AddRange(nuevosClientes);
            }

            // 2. Crear únicamente productos nuevos del catálogo (Codigo y Nombre)
            //    con los valores predeterminados de la entidad y sin movimientos de Kardex.
            var nuevosProductos = new List<Producto>(parseo.CatalogoValidos.Count);
            foreach (var filaCatalogo in parseo.CatalogoValidos)
            {
                nuevosProductos.Add(new Producto
                {
                    Id = Guid.NewGuid(),
                    CategoriaId = categoriaId!.Value,
                    Codigo = filaCatalogo.Codigo,
                    Nombre = filaCatalogo.Nombre,
                    Unidad = "unidad",
                    PrecioVenta = 0m,
                    Costo = 0m,
                    StockActual = 0,
                    StockMinimo = null,
                    Descripcion = null,
                    Marca = null,
                    FotoUrl = null,
                    FechaCreacion = ahora,
                    CreadoPorId = usuarioId,
                    Activo = true
                });
            }

            if (nuevosProductos.Count > 0)
            {
                _context.Productos.AddRange(nuevosProductos);
            }

            // 3. Registrar evento de auditoría dentro de la misma transacción
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
            int clientes = 0, catalogo = 0, errores = 0;

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
                    if (root.TryGetProperty("ClientesProcesados", out var pCli) && pCli.TryGetInt32(out var vCli))
                        clientes = vCli;
                    if (root.TryGetProperty("CatalogoProcesados", out var pCat) && pCat.TryGetInt32(out var vCat))
                        catalogo = vCat;
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
                errores
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
        bool soloValidacion,
        CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var errores = new List<ErrorFilaImportacionDto>();

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

            return CrearResultadoVacioConErrores(nombreArchivo, categoriaId, soloValidacion, ahora, errores);
        }

        using (workbook)
        {
            var wsClientes = BuscarHoja(workbook, HojaClientes);
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

            if (wsCatalogo is null)
            {
                errores.Add(new ErrorFilaImportacionDto(
                    Hoja: HojaCatalogo,
                    Fila: 1,
                    Columna: "Hoja",
                    ValorLeido: null,
                    Mensaje: $"No se encontró la hoja obligatoria '{HojaCatalogo}' en el archivo Excel."));
            }

            // Rechazar hojas no autorizadas (ej. Unidades) para garantizar el alcance de 2 hojas
            foreach (var sheet in workbook.Worksheets)
            {
                var claveHoja = NormalizarClaveTexto(sheet.Name);
                if (claveHoja != NormalizarClaveTexto(HojaClientes) && claveHoja != NormalizarClaveTexto(HojaCatalogo))
                {
                    errores.Add(new ErrorFilaImportacionDto(
                        Hoja: sheet.Name,
                        Fila: 1,
                        Columna: "Hoja",
                        ValorLeido: sheet.Name,
                        Mensaje: $"La hoja '{sheet.Name}' no está permitida. La plantilla admite únicamente las hojas '{HojaClientes}' y '{HojaCatalogo}'."));
                }
            }

            if (errores.Count > 0)
            {
                return CrearResultadoVacioConErrores(nombreArchivo, categoriaId, soloValidacion, ahora, errores);
            }

            // Validar cabeceras exactas de las 2 hojas
            string[] colsClientes = ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"];
            string[] colsCatalogo = ["Codigo", "Nombre"];

            var mapaColsClientes = ValidarYMapearCabeceras(wsClientes!, HojaClientes, colsClientes, permitirColumnasExtra: false, errores);
            var mapaColsCatalogo = ValidarYMapearCabeceras(wsCatalogo!, HojaCatalogo, colsCatalogo, permitirColumnasExtra: false, errores);

            if (errores.Count > 0)
            {
                return CrearResultadoVacioConErrores(nombreArchivo, categoriaId, soloValidacion, ahora, errores);
            }

            // Consultas de solo lectura (AsNoTracking) para verificar duplicados contra la BD
            var clientesBdList = await _context.Clientes
                .AsNoTracking()
                .Where(c => c.Activo && (c.NumeroDocumento != null || c.DocumentoIdentidad != null))
                .Select(c => (c.NumeroDocumento ?? c.DocumentoIdentidad)!.Trim())
                .ToListAsync(ct);

            var documentosClientesEnBd = new HashSet<string>(
                clientesBdList.Where(d => !string.IsNullOrWhiteSpace(d)),
                StringComparer.OrdinalIgnoreCase);

            var codigosProductosEnBd = new HashSet<string>(
                await _context.Productos.AsNoTracking().Select(p => p.Codigo.Trim()).ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            // -----------------------------------------------------------------
            // 1. Validar Hoja Clientes
            // -----------------------------------------------------------------
            var clientesValidos = new List<FilaClienteValidada>();
            var documentosEnHojaClientes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int filasLeidasClientes = 0;
            int filasConErrorClientes = 0;

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

                    if (documentosEnHojaClientes.TryGetValue(rawNumDoc, out int filaPrevia))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "NumeroDocumento", rawNumDoc, $"El número de documento '{rawNumDoc}' está duplicado en la hoja Clientes (ya aparece en la fila {filaPrevia})."));
                    }
                    else
                    {
                        documentosEnHojaClientes[rawNumDoc] = r;
                    }

                    if (documentosClientesEnBd.Contains(rawNumDoc))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaClientes, r, "NumeroDocumento", rawNumDoc, $"Ya existe un cliente activo en la base de datos con el número de documento '{rawNumDoc}'."));
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
                else
                {
                    clientesValidos.Add(new FilaClienteValidada(
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
            // 2. Validar Hoja Catalogo (Únicamente Codigo y Nombre)
            // -----------------------------------------------------------------
            var catalogoValidos = new List<FilaCatalogoValidada>();
            var codigosEnHojaCatalogo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int filasLeidasCatalogo = 0;
            int filasConErrorCatalogo = 0;

            int ultimaFilaCatalogo = wsCatalogo!.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= ultimaFilaCatalogo; r++)
            {
                var row = wsCatalogo.Row(r);
                if (FilaEstaVacia(row, mapaColsCatalogo.Values))
                    continue;

                filasLeidasCatalogo++;
                int erroresAntes = errores.Count;

                var rawCodigo = LeerTextoDocumentoOCodigo(row, mapaColsCatalogo["Codigo"]);
                var rawNombre = LeerTexto(row, mapaColsCatalogo["Nombre"]);

                if (string.IsNullOrWhiteSpace(rawCodigo))
                {
                    errores.Add(new ErrorFilaImportacionDto(HojaCatalogo, r, "Codigo", rawCodigo, "El Codigo del producto es obligatorio."));
                }
                else
                {
                    if (codigosEnHojaCatalogo.TryGetValue(rawCodigo, out int filaCodPrevia))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaCatalogo, r, "Codigo", rawCodigo, $"El código '{rawCodigo}' está duplicado en la hoja Catalogo (ya aparece en la fila {filaCodPrevia})."));
                    }
                    else
                    {
                        codigosEnHojaCatalogo[rawCodigo] = r;
                    }

                    if (codigosProductosEnBd.Contains(rawCodigo))
                    {
                        errores.Add(new ErrorFilaImportacionDto(HojaCatalogo, r, "Codigo", rawCodigo, $"Ya existe un producto registrado en la base de datos con el código '{rawCodigo}'."));
                    }
                }

                if (string.IsNullOrWhiteSpace(rawNombre))
                {
                    errores.Add(new ErrorFilaImportacionDto(HojaCatalogo, r, "Nombre", rawNombre, "El Nombre del producto es obligatorio."));
                }

                if (errores.Count > erroresAntes)
                {
                    filasConErrorCatalogo++;
                }
                else
                {
                    catalogoValidos.Add(new FilaCatalogoValidada(
                        r,
                        rawCodigo.Trim(),
                        rawNombre.Trim()));
                }
            }

            // Validar parámetro explícito categoriaId SOLO si la hoja Catalogo contiene productos nuevos que deban insertarse
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

            if (filasLeidasClientes == 0 && filasLeidasCatalogo == 0)
            {
                errores.Add(new ErrorFilaImportacionDto(
                    Hoja: "Archivo",
                    Fila: 2,
                    Columna: "Datos",
                    ValorLeido: null,
                    Mensaje: "El archivo no contiene ninguna fila con datos para importar en las hojas Clientes o Catalogo."));
            }

            bool exitoso = errores.Count == 0;
            bool cuentaCreados = exitoso && !soloValidacion;

            var resumenClientes = new ResumenHojaImportacionDto(
                TotalFilasLeidas: filasLeidasClientes,
                FilasValidas: clientesValidos.Count,
                FilasConError: filasConErrorClientes,
                RegistrosCreados: cuentaCreados ? clientesValidos.Count : 0);

            var resumenCatalogo = new ResumenHojaImportacionDto(
                TotalFilasLeidas: filasLeidasCatalogo,
                FilasValidas: catalogoValidos.Count,
                FilasConError: filasConErrorCatalogo,
                RegistrosCreados: cuentaCreados ? catalogoValidos.Count : 0);

            var resultadoDto = new ResultadoImportacionExcelDto(
                Exitoso: exitoso,
                SoloValidacion: soloValidacion,
                NombreArchivo: nombreArchivo,
                FechaProceso: ahora,
                CategoriaId: categoriaId,
                Clientes: resumenClientes,
                Catalogo: resumenCatalogo,
                Errores: errores);

            return new ResultadoParseoInterno(
                resultadoDto,
                clientesValidos,
                catalogoValidos);
        }
    }

    // =========================================================================
    // Generación de Hojas de la Plantilla Estandarizada (Clientes y Catalogo)
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

    private static void GenerarHojaCatalogo(XLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add(HojaCatalogo);
        string[] headers = ["Codigo", "Nombre"];
        EstilizarCabeceras(ws, headers);

        ws.Column(1).Style.NumberFormat.Format = "@";

        // Fila ejemplo 1
        ws.Cell(2, 1).Value = "90793-38046";
        ws.Cell(2, 2).Value = "Aceite Yamalube 4T 10W-40 Full Sintético";

        // Fila ejemplo 2
        ws.Cell(3, 1).Value = "5GH-13440-80";
        ws.Cell(3, 2).Value = "Filtro de Aceite Original Yamaha";

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
        string[] columnasEsperadas,
        bool permitirColumnasExtra,
        List<ErrorFilaImportacionDto> errores)
    {
        var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var filaCabecera = ws.Row(1);
        int ultimaColumna = ws.LastColumnUsed()?.ColumnNumber() ?? columnasEsperadas.Length;

        var clavesEsperadas = new HashSet<string>(
            columnasEsperadas.Select(NormalizarClaveTexto),
            StringComparer.OrdinalIgnoreCase);

        var cabecerasEnHoja = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= Math.Max(ultimaColumna, columnasEsperadas.Length); col++)
        {
            var texto = filaCabecera.Cell(col).GetFormattedString().Trim();
            if (!string.IsNullOrWhiteSpace(texto))
            {
                var clave = NormalizarClaveTexto(texto);
                if (!cabecerasEnHoja.ContainsKey(clave))
                {
                    cabecerasEnHoja[clave] = col;
                }

                if (!permitirColumnasExtra && !clavesEsperadas.Contains(clave))
                {
                    errores.Add(new ErrorFilaImportacionDto(
                        Hoja: nombreHoja,
                        Fila: 1,
                        Columna: texto,
                        ValorLeido: texto,
                        Mensaje: $"La columna '{texto}' no está permitida en la hoja '{nombreHoja}'. Columnas esperadas: {string.Join(", ", columnasEsperadas)}."));
                }
            }
        }

        foreach (var colEsperada in columnasEsperadas)
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
        bool soloValidacion,
        DateTime fechaProceso,
        List<ErrorFilaImportacionDto> errores)
    {
        var vacio = new ResumenHojaImportacionDto(0, 0, 0, 0);
        return new ResultadoParseoInterno(
            new ResultadoImportacionExcelDto(
                Exitoso: false,
                SoloValidacion: soloValidacion,
                NombreArchivo: nombreArchivo,
                FechaProceso: fechaProceso,
                CategoriaId: categoriaId,
                Clientes: vacio,
                Catalogo: vacio,
                Errores: errores),
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
            ClientesProcesados = resultado.Exitoso ? resultado.Clientes.FilasValidas : 0,
            CatalogoProcesados = resultado.Exitoso ? resultado.Catalogo.FilasValidas : 0,
            TotalErrores = resultado.Errores.Count,
            PrimerosErrores = resultado.Errores.Take(20).ToList()
        };
    }

    // =========================================================================
    // Modelos Internos de Filas Validadas
    // =========================================================================

    private sealed record FilaClienteValidada(
        int Fila,
        TipoDocumentoCliente TipoDocumento,
        string NumeroDocumento,
        string NombreCompleto,
        string? Telefono,
        string? Email,
        string? Direccion
    );

    private sealed record FilaCatalogoValidada(
        int Fila,
        string Codigo,
        string Nombre
    );

    private sealed record ResultadoParseoInterno(
        ResultadoImportacionExcelDto Resultado,
        List<FilaClienteValidada> ClientesValidos,
        List<FilaCatalogoValidada> CatalogoValidos
    );
}
