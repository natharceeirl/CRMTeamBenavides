using ClosedXML.Excel;
using CRMTeamBenavides.Api.Features.Importaciones;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.Importacion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

public class ImportadorExcelTests
{
    private static ApplicationDbContext CrearDbContextMemoria()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private class FakeAuditoriaService : IAuditoriaService
    {
        public List<(Guid? usuarioId, string accion, string entidad, string entidadId, object? detalle)> Eventos { get; } = new();

        public Task RegistrarEventoAsync(Guid? usuarioId, string accion, string entidad, string entidadId, object? detalle = null, CancellationToken ct = default)
        {
            Eventos.Add((usuarioId, accion, entidad, entidadId, detalle));
            return Task.CompletedTask;
        }
    }

    private static MemoryStream CrearLibroExcel(
        Action<IXLWorksheet>? llenarClientes = null,
        Action<IXLWorksheet>? llenarUnidades = null,
        Action<IXLWorksheet>? llenarRepuestos = null,
        Action<IXLWorksheet>? llenarCatalogo = null)
    {
        var workbook = new XLWorkbook();
        if (llenarClientes is not null)
        {
            var ws = workbook.Worksheets.Add("Clientes");
            llenarClientes(ws);
        }
        if (llenarUnidades is not null)
        {
            var ws = workbook.Worksheets.Add("Unidades");
            llenarUnidades(ws);
        }
        if (llenarRepuestos is not null)
        {
            var ws = workbook.Worksheets.Add("Repuestos");
            llenarRepuestos(ws);
        }
        if (llenarCatalogo is not null)
        {
            var ws = workbook.Worksheets.Add("Catalogo");
            llenarCatalogo(ws);
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static void EscribirCabecera(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }
    }

    // =========================================================================
    // 1. Generación de plantilla con 3 hojas y encabezados exactos
    // =========================================================================

    [Fact]
    public void GenerarPlantillaExcel_DebeContenerTresHojasConEncabezadosExactos()
    {
        using var context = CrearDbContextMemoria();
        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        var bytes = service.GenerarPlantillaExcel();
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);

        Assert.Equal(3, workbook.Worksheets.Count);

        // Hoja Clientes
        var wsClientes = workbook.Worksheet("Clientes");
        Assert.NotNull(wsClientes);
        Assert.Equal("TipoDocumento", wsClientes.Cell(1, 1).GetString());
        Assert.Equal("NumeroDocumento", wsClientes.Cell(1, 2).GetString());
        Assert.Equal("NombreCompleto", wsClientes.Cell(1, 3).GetString());
        Assert.Equal("Telefono", wsClientes.Cell(1, 4).GetString());
        Assert.Equal("Email", wsClientes.Cell(1, 5).GetString());
        Assert.Equal("Direccion", wsClientes.Cell(1, 6).GetString());
        Assert.False(wsClientes.Cell(2, 2).IsEmpty()); // Fila ejemplo 1
        Assert.False(wsClientes.Cell(3, 2).IsEmpty()); // Fila ejemplo 2

        // Hoja Unidades
        var wsUnidades = workbook.Worksheet("Unidades");
        Assert.NotNull(wsUnidades);
        Assert.Equal("DocumentoPropietario", wsUnidades.Cell(1, 1).GetString());
        Assert.Equal("TipoUnidad", wsUnidades.Cell(1, 2).GetString());
        Assert.Equal("Placa", wsUnidades.Cell(1, 3).GetString());
        Assert.Equal("NumeroSerieVIN", wsUnidades.Cell(1, 4).GetString());
        Assert.Equal("NumeroMotor", wsUnidades.Cell(1, 5).GetString());
        Assert.Equal("Marca", wsUnidades.Cell(1, 6).GetString());
        Assert.Equal("Modelo", wsUnidades.Cell(1, 7).GetString());
        Assert.Equal("Anio", wsUnidades.Cell(1, 8).GetString());
        Assert.Equal("TipoMedidor", wsUnidades.Cell(1, 9).GetString());
        Assert.Equal("LecturaMedidor", wsUnidades.Cell(1, 10).GetString());
        Assert.False(wsUnidades.Cell(2, 1).IsEmpty()); // Moto ejemplo

        // Hoja Repuestos
        var wsRepuestos = workbook.Worksheet("Repuestos");
        Assert.NotNull(wsRepuestos);
        Assert.Equal("Codigo", wsRepuestos.Cell(1, 1).GetString());
        Assert.Equal("Nombre", wsRepuestos.Cell(1, 2).GetString());
        Assert.Equal("Categoria", wsRepuestos.Cell(1, 3).GetString());
        Assert.Equal("UnidadMedida", wsRepuestos.Cell(1, 4).GetString());
        Assert.Equal("Costo", wsRepuestos.Cell(1, 5).GetString());
        Assert.Equal("PrecioVenta", wsRepuestos.Cell(1, 6).GetString());
        Assert.Equal("StockActual", wsRepuestos.Cell(1, 7).GetString());
        Assert.Equal("StockMinimo", wsRepuestos.Cell(1, 8).GetString());
        Assert.False(wsRepuestos.Cell(2, 1).IsEmpty()); // Repuesto ejemplo
    }

    // =========================================================================
    // 2. Importación válida con 3 hojas (clientes, unidades, repuestos con Kardex)
    // =========================================================================

    [Fact]
    public async Task EjecutarImportacionAsync_TresHojasValidas_DebePersistirTodoYCrearKardexSoloSiHayStock()
    {
        using var context = CrearDbContextMemoria();
        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);
        var usuarioId = Guid.NewGuid();

        using var stream = CrearLibroExcel(
            llenarClientes: ws =>
            {
                EscribirCabecera(ws, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
                // Fila 2: DNI
                ws.Cell(2, 1).Value = "DNI";
                ws.Cell(2, 2).Value = "45871236";
                ws.Cell(2, 3).Value = "Carlos Alberto Mendoza";
                ws.Cell(2, 4).Value = "987654321";
                ws.Cell(2, 5).Value = "carlos@ejemplo.pe";
                ws.Cell(2, 6).Value = "Av. Ejercito 123";
                // Fila 3: RUC
                ws.Cell(3, 1).Value = "RUC";
                ws.Cell(3, 2).Value = "20601234567";
                ws.Cell(3, 3).Value = "Inversiones Nauticas SAC";
                ws.Cell(3, 4).Value = "954112233";
                ws.Cell(3, 5).Value = "contacto@nautica.pe";
                ws.Cell(3, 6).Value = "Calle Marina 456";
            },
            llenarUnidades: ws =>
            {
                EscribirCabecera(ws, ["DocumentoPropietario", "TipoUnidad", "Placa", "NumeroSerieVIN", "NumeroMotor", "Marca", "Modelo", "Anio", "TipoMedidor", "LecturaMedidor"]);
                // Unidad 1: Moto vinculada a Carlos con placa y Km
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
                // Unidad 2: MotoAcuatica vinculada a Nautica SAC sin placa y con horas
                ws.Cell(3, 1).Value = "20601234567";
                ws.Cell(3, 2).Value = "MotoAcuatica";
                ws.Cell(3, 3).Value = "";
                ws.Cell(3, 4).Value = "YDV12345L223";
                ws.Cell(3, 5).Value = "6EY-1002345";
                ws.Cell(3, 6).Value = "Yamaha";
                ws.Cell(3, 7).Value = "WaveRunner VX";
                ws.Cell(3, 8).Value = 2022;
                ws.Cell(3, 9).Value = "Horas";
                ws.Cell(3, 10).Value = 85.5;
            },
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                // Repuesto 1: Con stock 20 (debe crear kardex)
                ws.Cell(2, 1).Value = "90793-38046";
                ws.Cell(2, 2).Value = "Aceite Yamalube 4T";
                ws.Cell(2, 3).Value = "Lubricantes";
                ws.Cell(2, 4).Value = "litro";
                ws.Cell(2, 5).Value = 35.00;
                ws.Cell(2, 6).Value = 50.00;
                ws.Cell(2, 7).Value = 20;
                ws.Cell(2, 8).Value = 5;
                // Repuesto 2: Con stock 0 (NO debe crear kardex)
                ws.Cell(3, 1).Value = "5GH-13440-80";
                ws.Cell(3, 2).Value = "Filtro Aceite Yamaha";
                ws.Cell(3, 3).Value = "Filtros";
                ws.Cell(3, 4).Value = "unidad";
                ws.Cell(3, 5).Value = 25.00;
                ws.Cell(3, 6).Value = 40.00;
                ws.Cell(3, 7).Value = 0;
                ws.Cell(3, 8).Value = 2;
            });

        var resultado = await service.EjecutarImportacionAsync(stream, "prueba_3hojas.xlsx", null, usuarioId);

        Assert.True(resultado.Exitoso);
        Assert.False(resultado.SoloValidacion);
        Assert.Empty(resultado.Errores);

        Assert.Equal(2, resultado.Clientes.RegistrosCreados);
        Assert.Equal(2, resultado.Unidades?.RegistrosCreados);
        Assert.Equal(2, resultado.Repuestos?.RegistrosCreados);

        // Verificar persistencia en base de datos
        var clientesBd = await context.Clientes.ToListAsync();
        Assert.Equal(2, clientesBd.Count);
        var carlos = Assert.Single(clientesBd, c => c.NumeroDocumento == "45871236");
        var nautica = Assert.Single(clientesBd, c => c.NumeroDocumento == "20601234567");

        var vehiculosBd = await context.Vehiculos.ToListAsync();
        Assert.Equal(2, vehiculosBd.Count);

        var moto = Assert.Single(vehiculosBd, v => v.Placa == "1234-5A");
        Assert.Equal(carlos.Id, moto.ClienteId);
        Assert.Equal(TipoUnidad.Motocicleta, moto.TipoUnidad);
        Assert.Equal(TipoMedidor.Kilometraje, moto.TipoMedidor);
        Assert.Equal(4500, moto.Kilometraje);

        var jetSki = Assert.Single(vehiculosBd, v => v.NumeroSerieVIN == "YDV12345L223");
        Assert.Equal(nautica.Id, jetSki.ClienteId);
        Assert.Equal(TipoUnidad.MotoAcuatica, jetSki.TipoUnidad);
        Assert.Equal(TipoMedidor.Horas, jetSki.TipoMedidor);
        Assert.Equal(85.5m, jetSki.HorasUso);

        var productosBd = await context.Productos.ToListAsync();
        Assert.Equal(2, productosBd.Count);

        // Verificar categorías creadas dinámicamente
        var categoriasBd = await context.CategoriasProducto.ToListAsync();
        Assert.Contains(categoriasBd, c => c.Nombre == "Lubricantes");
        Assert.Contains(categoriasBd, c => c.Nombre == "Filtros");

        // Verificar Kardex: exactamente 1 movimiento de entrada para el repuesto con stock > 0
        var movimientos = await context.MovimientosInventario.ToListAsync();
        Assert.Single(movimientos);
        var mov = movimientos[0];
        Assert.Equal(TipoMovimientoInventario.Entrada, mov.Tipo);
        Assert.Equal(20, mov.Cantidad);
        Assert.Equal(35.00m, mov.CostoUnitario);
        Assert.Equal("Carga inicial de inventario", mov.Motivo);
        Assert.Equal(productosBd.First(p => p.Codigo == "90793-38046").Id, mov.ProductoId);

        // Verificar evento de auditoría
        Assert.Single(context.EventosAuditoria);
    }

    // =========================================================================
    // 3. Dry-Run (SoloValidacion) no debe tocar la base de datos
    // =========================================================================

    [Fact]
    public async Task ValidarArchivoAsync_DryRun_NoDebePersistirEnBaseDeDatos()
    {
        using var context = CrearDbContextMemoria();
        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        using var stream = CrearLibroExcel(
            llenarClientes: ws =>
            {
                EscribirCabecera(ws, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
                ws.Cell(2, 1).Value = "DNI";
                ws.Cell(2, 2).Value = "45871236";
                ws.Cell(2, 3).Value = "Carlos Alberto Mendoza";
            },
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                ws.Cell(2, 1).Value = "SKU-001";
                ws.Cell(2, 2).Value = "Repuesto Test";
                ws.Cell(2, 3).Value = "General";
                ws.Cell(2, 4).Value = "unidad";
                ws.Cell(2, 5).Value = 10;
                ws.Cell(2, 6).Value = 20;
                ws.Cell(2, 7).Value = 5;
            });

        var resultado = await service.ValidarArchivoAsync(stream, "dry_run.xlsx", null, null);

        Assert.True(resultado.Exitoso);
        Assert.True(resultado.SoloValidacion);
        Assert.Equal(1, resultado.Clientes.FilasValidas);
        Assert.Equal(0, resultado.Clientes.RegistrosCreados);
        Assert.Equal(1, resultado.Repuestos?.FilasValidas);
        Assert.Equal(0, resultado.Repuestos?.RegistrosCreados);

        Assert.Empty(await context.Clientes.ToListAsync());
        Assert.Empty(await context.Productos.ToListAsync());
        Assert.Empty(await context.MovimientosInventario.ToListAsync());
    }

    // =========================================================================
    // 4. Errores de validación exhaustivos
    // =========================================================================

    [Fact]
    public async Task ValidarArchivoAsync_ErroresValidacion_DebeDetectarTodasLasInconsistencias()
    {
        using var context = CrearDbContextMemoria();
        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        using var stream = CrearLibroExcel(
            llenarClientes: ws =>
            {
                EscribirCabecera(ws, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
                // Fila 2: DNI con formato inválido (solo 5 dígitos)
                ws.Cell(2, 1).Value = "DNI";
                ws.Cell(2, 2).Value = "12345";
                ws.Cell(2, 3).Value = "Cliente Invalido";
                // Fila 3 y Fila 4: Duplicado interno de DNI en archivo
                ws.Cell(3, 1).Value = "DNI";
                ws.Cell(3, 2).Value = "45871236";
                ws.Cell(3, 3).Value = "Cliente Original";
                ws.Cell(4, 1).Value = "DNI";
                ws.Cell(4, 2).Value = "45871236";
                ws.Cell(4, 3).Value = "Cliente Duplicado";
            },
            llenarUnidades: ws =>
            {
                EscribirCabecera(ws, ["DocumentoPropietario", "TipoUnidad", "Placa", "NumeroSerieVIN", "NumeroMotor", "Marca", "Modelo", "Anio", "TipoMedidor", "LecturaMedidor"]);
                // Fila 2: Propietario inexistente
                ws.Cell(2, 1).Value = "99999999";
                ws.Cell(2, 2).Value = "Motocicleta";
                ws.Cell(2, 3).Value = "1111-AA";
                ws.Cell(2, 6).Value = "Yamaha";
                ws.Cell(2, 7).Value = "R3";
                ws.Cell(2, 9).Value = "Kilometros";
                ws.Cell(2, 10).Value = 1000;
                // Fila 3: Motocicleta sin placa
                ws.Cell(3, 1).Value = "45871236";
                ws.Cell(3, 2).Value = "Motocicleta";
                ws.Cell(3, 3).Value = "";
                ws.Cell(3, 6).Value = "Yamaha";
                ws.Cell(3, 7).Value = "FZ";
                ws.Cell(3, 9).Value = "Kilometros";
                ws.Cell(3, 10).Value = 500;
                // Fila 4: TipoUnidad inválido
                ws.Cell(4, 1).Value = "45871236";
                ws.Cell(4, 2).Value = "Submarino";
                ws.Cell(4, 3).Value = "2222-BB";
                ws.Cell(4, 6).Value = "Yamaha";
                ws.Cell(4, 7).Value = "Sub1";
                ws.Cell(4, 9).Value = "Kilometros";
                ws.Cell(4, 10).Value = 500;
            },
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                // Fila 2: Costo negativo
                ws.Cell(2, 1).Value = "REP-001";
                ws.Cell(2, 2).Value = "Repuesto Negativo";
                ws.Cell(2, 3).Value = "Motor";
                ws.Cell(2, 5).Value = -10.00;
                ws.Cell(2, 6).Value = 20.00;
                ws.Cell(2, 7).Value = 5;
                // Fila 3: StockActual negativo
                ws.Cell(3, 1).Value = "REP-002";
                ws.Cell(3, 2).Value = "Repuesto Stock Negativo";
                ws.Cell(3, 3).Value = "Motor";
                ws.Cell(3, 5).Value = 10.00;
                ws.Cell(3, 6).Value = 20.00;
                ws.Cell(3, 7).Value = -5;
                // Fila 4: Duplicado interno de código
                ws.Cell(4, 1).Value = "REP-001";
                ws.Cell(4, 2).Value = "Repuesto Duplicado SKU";
                ws.Cell(4, 3).Value = "Motor";
                ws.Cell(4, 5).Value = 15.00;
                ws.Cell(4, 6).Value = 25.00;
                ws.Cell(4, 7).Value = 1;
            });

        var resultado = await service.ValidarArchivoAsync(stream, "con_errores.xlsx", null, null);

        Assert.False(resultado.Exitoso);
        Assert.NotEmpty(resultado.Errores);

        // Errores específicos detectados
        Assert.Contains(resultado.Errores, e => e.Hoja == "Clientes" && e.Columna == "NumeroDocumento" && e.Mensaje.Contains("8 dígitos"));
        Assert.Contains(resultado.Errores, e => e.Hoja == "Clientes" && e.Columna == "NumeroDocumento" && e.Mensaje.Contains("duplicado en la hoja Clientes"));
        Assert.Contains(resultado.Errores, e => e.Hoja == "Unidades" && e.Columna == "DocumentoPropietario" && e.Mensaje.Contains("No se encontró"));
        Assert.Contains(resultado.Errores, e => e.Hoja == "Unidades" && e.Columna == "Placa" && e.Mensaje.Contains("obligatoria para unidades de tipo Motocicleta"));
        Assert.Contains(resultado.Errores, e => e.Hoja == "Unidades" && e.Columna == "TipoUnidad" && e.Mensaje.Contains("inválido"));
        Assert.Contains(resultado.Errores, e => e.Hoja == "Repuestos" && e.Columna == "Costo" && e.Mensaje.Contains("mayor o igual a 0"));
        Assert.Contains(resultado.Errores, e => e.Hoja == "Repuestos" && e.Columna == "StockActual" && e.Mensaje.Contains("mayor o igual a 0"));
        Assert.Contains(resultado.Errores, e => e.Hoja == "Repuestos" && e.Columna == "Codigo" && e.Mensaje.Contains("duplicado en la hoja Repuestos"));
    }

    // =========================================================================
    // 5. Modo ErrorSiExiste con registros preexistentes en BD -> Error y Rollback
    // =========================================================================

    [Fact]
    public async Task ModoErrorSiExiste_ConClienteYProductoPreexistentesEnBd_DebeRechazarYNoGuardarNada()
    {
        using var context = CrearDbContextMemoria();
        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        // Sembrar cliente y producto en BD
        var categoria = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Partes" };
        context.CategoriasProducto.Add(categoria);
        context.Clientes.Add(new Cliente
        {
            Id = Guid.NewGuid(),
            NumeroDocumento = "45871236",
            DocumentoIdentidad = "45871236",
            NombreCompleto = "Cliente Existente",
            TipoDocumento = TipoDocumentoCliente.DNI,
            Activo = true
        });
        context.Productos.Add(new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = categoria.Id,
            Codigo = "90793-38046",
            Nombre = "Aceite Existente",
            StockActual = 10,
            Activo = true
        });
        await context.SaveChangesAsync();

        using var stream = CrearLibroExcel(
            llenarClientes: ws =>
            {
                EscribirCabecera(ws, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
                ws.Cell(2, 1).Value = "DNI";
                ws.Cell(2, 2).Value = "45871236"; // Ya en BD
                ws.Cell(2, 3).Value = "Cliente Que Ya Existe";
            },
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                ws.Cell(2, 1).Value = "90793-38046"; // Ya en BD
                ws.Cell(2, 2).Value = "Aceite Existente";
                ws.Cell(2, 3).Value = "Partes";
                ws.Cell(2, 5).Value = 30;
                ws.Cell(2, 6).Value = 45;
                ws.Cell(2, 7).Value = 10;
            });

        var resultado = await service.EjecutarImportacionAsync(stream, "duplicados_bd.xlsx", null, null, modoDuplicados: "ErrorSiExiste");

        Assert.False(resultado.Exitoso);
        Assert.Equal(2, resultado.Errores.Count);
        Assert.Contains(resultado.Errores, e => e.Hoja == "Clientes" && e.Mensaje.Contains("Ya existe un cliente activo en la base de datos"));
        Assert.Contains(resultado.Errores, e => e.Hoja == "Repuestos" && e.Mensaje.Contains("Ya existe un producto registrado en la base de datos"));

        // Verificar que no se alteró la BD
        Assert.Single(await context.Clientes.ToListAsync());
        Assert.Single(await context.Productos.ToListAsync());
        Assert.Empty(await context.MovimientosInventario.ToListAsync());
    }

    // =========================================================================
    // 6. Modo OmitirExistentes: omite cliente pero importa y vincula su unidad
    // =========================================================================

    [Fact]
    public async Task ModoOmitirExistentes_ClienteExistente_DebeOmitirClienteEImportarUnidadNuevaVinculada()
    {
        using var context = CrearDbContextMemoria();
        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        var clienteExistenteId = Guid.NewGuid();
        context.Clientes.Add(new Cliente
        {
            Id = clienteExistenteId,
            NumeroDocumento = "45871236",
            DocumentoIdentidad = "45871236",
            NombreCompleto = "Juan Perez (Existente)",
            TipoDocumento = TipoDocumentoCliente.DNI,
            Activo = true
        });
        await context.SaveChangesAsync();

        using var stream = CrearLibroExcel(
            llenarClientes: ws =>
            {
                EscribirCabecera(ws, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
                // Cliente que ya está en BD
                ws.Cell(2, 1).Value = "DNI";
                ws.Cell(2, 2).Value = "45871236";
                ws.Cell(2, 3).Value = "Juan Perez";
            },
            llenarUnidades: ws =>
            {
                EscribirCabecera(ws, ["DocumentoPropietario", "TipoUnidad", "Placa", "NumeroSerieVIN", "NumeroMotor", "Marca", "Modelo", "Anio", "TipoMedidor", "LecturaMedidor"]);
                // Unidad nueva perteneciente al cliente existente
                ws.Cell(2, 1).Value = "45871236";
                ws.Cell(2, 2).Value = "Motocicleta";
                ws.Cell(2, 3).Value = "9999-ZZ";
                ws.Cell(2, 4).Value = "VIN-NUEVO-001";
                ws.Cell(2, 5).Value = "MOT-001";
                ws.Cell(2, 6).Value = "Yamaha";
                ws.Cell(2, 7).Value = "MT-03";
                ws.Cell(2, 8).Value = 2024;
                ws.Cell(2, 9).Value = "Kilometros";
                ws.Cell(2, 10).Value = 1200;
            },
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                ws.Cell(2, 1).Value = "NEW-PART-01";
                ws.Cell(2, 2).Value = "Pastillas de Freno";
                ws.Cell(2, 3).Value = "Frenos";
                ws.Cell(2, 5).Value = 40;
                ws.Cell(2, 6).Value = 65;
                ws.Cell(2, 7).Value = 15;
            });

        var resultado = await service.EjecutarImportacionAsync(stream, "omitir_existente.xlsx", null, null, modoDuplicados: "OmitirExistentes");

        Assert.True(resultado.Exitoso);
        Assert.Empty(resultado.Errores);

        // Cliente omitido (no duplicado en BD)
        Assert.Equal(0, resultado.Clientes.RegistrosCreados);
        Assert.Equal(1, resultado.Clientes.RegistrosOmitidos);

        // Unidad importada y vinculada
        Assert.Equal(1, resultado.Unidades?.RegistrosCreados);
        Assert.Equal(0, resultado.Unidades?.RegistrosOmitidos);

        // Repuesto importado
        Assert.Equal(1, resultado.Repuestos?.RegistrosCreados);

        // Base de datos: el cliente sigue siendo solo 1
        var clientesEnBd = await context.Clientes.ToListAsync();
        Assert.Single(clientesEnBd);
        Assert.Equal(clienteExistenteId, clientesEnBd[0].Id);

        // El vehículo se creó y está correctamente asociado a clienteExistenteId
        var vehiculosEnBd = await context.Vehiculos.ToListAsync();
        Assert.Single(vehiculosEnBd);
        Assert.Equal(clienteExistenteId, vehiculosEnBd[0].ClienteId);
        Assert.Equal("9999-ZZ", vehiculosEnBd[0].Placa);

        // Kardex creado para el repuesto nuevo
        var movimientos = await context.MovimientosInventario.ToListAsync();
        Assert.Single(movimientos);
        Assert.Equal(15, movimientos[0].Cantidad);
    }

    // =========================================================================
    // 7. Modo OmitirExistentes con repuesto existente: no duplica Kardex
    // =========================================================================

    [Fact]
    public async Task ModoOmitirExistentes_RepuestoExistente_DebeOmitirRepuestoYNoGenerarKardex()
    {
        using var context = CrearDbContextMemoria();
        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        var categoria = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Fluidos" };
        context.CategoriasProducto.Add(categoria);
        context.Productos.Add(new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = categoria.Id,
            Codigo = "EXISTING-001",
            Nombre = "Aceite Previsto",
            StockActual = 5,
            Activo = true
        });
        await context.SaveChangesAsync();

        using var stream = CrearLibroExcel(
            llenarClientes: ws =>
            {
                EscribirCabecera(ws, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
                ws.Cell(2, 1).Value = "DNI";
                ws.Cell(2, 2).Value = "11223344";
                ws.Cell(2, 3).Value = "Nuevo Cliente";
            },
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                // Repuesto existente (debe omitirse)
                ws.Cell(2, 1).Value = "EXISTING-001";
                ws.Cell(2, 2).Value = "Aceite Previsto";
                ws.Cell(2, 3).Value = "Fluidos";
                ws.Cell(2, 5).Value = 20;
                ws.Cell(2, 6).Value = 35;
                ws.Cell(2, 7).Value = 100;
                // Repuesto nuevo
                ws.Cell(3, 1).Value = "BRAND-NEW-01";
                ws.Cell(3, 2).Value = "Bujía NGK";
                ws.Cell(3, 3).Value = "Encendido";
                ws.Cell(3, 5).Value = 15;
                ws.Cell(3, 6).Value = 28;
                ws.Cell(3, 7).Value = 8;
            });

        var resultado = await service.EjecutarImportacionAsync(stream, "repuesto_existente.xlsx", null, null, modoDuplicados: "OmitirExistentes");

        Assert.True(resultado.Exitoso);
        Assert.Equal(1, resultado.Repuestos?.RegistrosCreados);
        Assert.Equal(1, resultado.Repuestos?.RegistrosOmitidos);

        // El repuesto preexistente mantiene su stock previo intacto
        var prodExistente = await context.Productos.FirstAsync(p => p.Codigo == "EXISTING-001");
        Assert.Equal(5, prodExistente.StockActual);

        // Solo se generó 1 Kardex para el repuesto nuevo con cantidad 8
        var movimientos = await context.MovimientosInventario.ToListAsync();
        Assert.Single(movimientos);
        Assert.Equal(8, movimientos[0].Cantidad);
        Assert.Equal(15m, movimientos[0].CostoUnitario);
    }

    // =========================================================================
    // 8. Retrocompatibilidad con hoja legada Catalogo
    // =========================================================================

    [Fact]
    public async Task Retrocompatibilidad_HojaCatalogo_DebeImportarConCategoriaId()
    {
        using var context = CrearDbContextMemoria();
        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        var categoria = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Catalogo General" };
        context.CategoriasProducto.Add(categoria);
        await context.SaveChangesAsync();

        using var stream = CrearLibroExcel(
            llenarClientes: ws =>
            {
                EscribirCabecera(ws, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
                ws.Cell(2, 1).Value = "DNI";
                ws.Cell(2, 2).Value = "77889900";
                ws.Cell(2, 3).Value = "Cliente Legado";
            },
            llenarCatalogo: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre"]);
                ws.Cell(2, 1).Value = "LEGACY-001";
                ws.Cell(2, 2).Value = "Producto Legado";
            });

        var resultado = await service.EjecutarImportacionAsync(stream, "legado.xlsx", categoria.Id, null);

        Assert.True(resultado.Exitoso);
        Assert.Equal(1, resultado.Clientes.RegistrosCreados);
        Assert.Equal(1, resultado.Catalogo.RegistrosCreados);

        var producto = await context.Productos.FirstAsync(p => p.Codigo == "LEGACY-001");
        Assert.Equal("Producto Legado", producto.Nombre);
        Assert.Equal(0m, producto.Costo);
        Assert.Equal(0m, producto.PrecioVenta);
        Assert.Equal(0, producto.StockActual);
        Assert.Empty(await context.MovimientosInventario.ToListAsync()); // Cero Kardex
    }

    // =========================================================================
    // 9. Rechazo de hojas no autorizadas
    // =========================================================================

    [Fact]
    public async Task ValidarArchivoAsync_HojaNoAutorizada_DebeRechazarLibro()
    {
        using var context = CrearDbContextMemoria();
        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        var workbook = new XLWorkbook();
        var wsCli = workbook.Worksheets.Add("Clientes");
        EscribirCabecera(wsCli, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
        wsCli.Cell(2, 1).Value = "DNI";
        wsCli.Cell(2, 2).Value = "45871236";
        wsCli.Cell(2, 3).Value = "Carlos Mendoza";

        // Hoja no permitida
        workbook.Worksheets.Add("HojaNoAutorizada");

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var resultado = await service.ValidarArchivoAsync(ms, "hoja_invalida.xlsx", null, null);

        Assert.False(resultado.Exitoso);
        Assert.Contains(resultado.Errores, e => e.Mensaje.Contains("no está permitida"));
    }

    // =========================================================================
    // 10. Rollback ante error de ejecución en base de datos
    // =========================================================================

    private class FailingApplicationDbContext : ApplicationDbContext
    {
        public FailingApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public bool ForzarErrorAlGuardar { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ForzarErrorAlGuardar)
            {
                throw new DbUpdateException("Conflicto simulado al persistir en base de datos", new Exception("Restricción de base de datos"));
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task EjecutarImportacionAsync_RollbackPorExcepcionEnGuardado_DebeRevertirTransaccionYReportarFallo()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var context = new FailingApplicationDbContext(options);
        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        using var stream = CrearLibroExcel(
            llenarClientes: ws =>
            {
                EscribirCabecera(ws, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
                ws.Cell(2, 1).Value = "DNI";
                ws.Cell(2, 2).Value = "45871236";
                ws.Cell(2, 3).Value = "Carlos Mendoza";
            },
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                ws.Cell(2, 1).Value = "TEST-SKU";
                ws.Cell(2, 2).Value = "Repuesto Test";
                ws.Cell(2, 3).Value = "General";
                ws.Cell(2, 5).Value = 10;
                ws.Cell(2, 6).Value = 20;
                ws.Cell(2, 7).Value = 5;
            });

        // Activar fallo simulado al momento de guardar
        context.ForzarErrorAlGuardar = true;

        var resultado = await service.EjecutarImportacionAsync(stream, "fallo_db.xlsx", null, null);

        Assert.False(resultado.Exitoso);
        Assert.Equal(0, resultado.Clientes.RegistrosCreados);
        Assert.Equal(0, resultado.Repuestos?.RegistrosCreados);
        Assert.Contains(resultado.Errores, e => e.Hoja == "Archivo" && e.Columna == "Transaccion" && e.Mensaje.Contains("revertida por un conflicto"));
    }

    // =========================================================================
    // 11. Manejo determinista de categorías duplicadas y prevención de ambigüedad
    // =========================================================================

    private static void EscribirClienteEjemplo(IXLWorksheet ws)
    {
        EscribirCabecera(ws, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
        ws.Cell(2, 1).Value = "DNI";
        ws.Cell(2, 2).Value = "12345678";
        ws.Cell(2, 3).Value = "Cliente Prueba";
    }

    [Fact]
    public async Task ValidarYEjecutar_CategoriasUnicas_DebeProcesarNormalmente()
    {
        using var context = CrearDbContextMemoria();
        var catLubricantes = new CategoriaProducto
        {
            Id = Guid.NewGuid(),
            Nombre = "LUBRICANTES",
            StockMinimoDefault = 4,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        context.CategoriasProducto.Add(catLubricantes);
        await context.SaveChangesAsync();

        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        using var stream = CrearLibroExcel(
            llenarClientes: EscribirClienteEjemplo,
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                ws.Cell(2, 1).Value = "MOT-001";
                ws.Cell(2, 2).Value = "Aceite Yamalube 10W40";
                ws.Cell(2, 3).Value = "Lubricantes"; // Coincide case-insensitive
                ws.Cell(2, 4).Value = "litro";
                ws.Cell(2, 5).Value = 35;
                ws.Cell(2, 6).Value = 55;
                ws.Cell(2, 7).Value = 10;
                ws.Cell(2, 8).Value = 2;
            });

        // 1. Validar
        var resultadoValidacion = await service.ValidarArchivoAsync(stream, "repuestos_unicos.xlsx", null, null);
        Assert.True(resultadoValidacion.Exitoso);
        Assert.Empty(resultadoValidacion.Errores);
        Assert.Equal(1, resultadoValidacion.Repuestos?.FilasValidas);

        // 2. Ejecutar
        stream.Position = 0;
        var resultadoEjecucion = await service.EjecutarImportacionAsync(stream, "repuestos_unicos.xlsx", null, null);
        Assert.True(resultadoEjecucion.Exitoso);
        Assert.Equal(1, resultadoEjecucion.Repuestos?.RegistrosCreados);

        var prod = await context.Productos.FirstOrDefaultAsync(p => p.Codigo == "MOT-001");
        Assert.NotNull(prod);
        Assert.Equal(catLubricantes.Id, prod.CategoriaId);
        Assert.Equal(1, await context.CategoriasProducto.CountAsync());
    }

    [Fact]
    public async Task ValidarYEjecutar_CategoriasDuplicadasActivas_MismoNombreMayusculasYEspacios_DebeRetornarErrorControladoYNoLanzarExcepcion()
    {
        using var context = CrearDbContextMemoria();
        var cat1 = new CategoriaProducto
        {
            Id = Guid.NewGuid(),
            Nombre = "Repuestos",
            StockMinimoDefault = 4,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        var cat2 = new CategoriaProducto
        {
            Id = Guid.NewGuid(),
            Nombre = "  repuestos  ", // Difiere solo en mayúsculas y espacios
            StockMinimoDefault = 2,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        context.CategoriasProducto.AddRange(cat1, cat2);
        await context.SaveChangesAsync();

        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        using var stream = CrearLibroExcel(
            llenarClientes: EscribirClienteEjemplo,
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                ws.Cell(2, 1).Value = "REP-001";
                ws.Cell(2, 2).Value = "Pastillas de freno";
                ws.Cell(2, 3).Value = "REPUESTOS";
                ws.Cell(2, 4).Value = "unidad";
                ws.Cell(2, 5).Value = 25;
                ws.Cell(2, 6).Value = 45;
                ws.Cell(2, 7).Value = 0;
            });

        // 1. Validar: no debe lanzar ArgumentException, debe reportar error controlado
        var resultadoValidacion = await service.ValidarArchivoAsync(stream, "repuestos_duplicados.xlsx", null, null);
        Assert.False(resultadoValidacion.Exitoso);
        Assert.Equal(1, resultadoValidacion.Repuestos?.FilasConError);
        Assert.Equal(0, resultadoValidacion.Repuestos?.FilasValidas);

        var errorAmb = Assert.Single(resultadoValidacion.Errores);
        Assert.Equal("Repuestos", errorAmb.Hoja);
        Assert.Equal(2, errorAmb.Fila);
        Assert.Equal("Categoria", errorAmb.Columna);
        Assert.Equal("REPUESTOS", errorAmb.ValorLeido);
        Assert.Contains("ambigüedad", errorAmb.Mensaje, StringComparison.OrdinalIgnoreCase);

        // 2. Ejecutar: no debe lanzar ArgumentException y debe abortar sin modificar datos
        stream.Position = 0;
        var resultadoEjecucion = await service.EjecutarImportacionAsync(stream, "repuestos_duplicados.xlsx", null, null);
        Assert.False(resultadoEjecucion.Exitoso);
        Assert.Equal(0, resultadoEjecucion.Repuestos?.RegistrosCreados);
        Assert.Empty(await context.Productos.ToListAsync());
    }

    [Fact]
    public async Task ValidarYEjecutar_CategoriaActivaECategoriaInactivaConMismoNombre_DebeUsarActivaSinAmbiguedad()
    {
        using var context = CrearDbContextMemoria();
        var catActiva = new CategoriaProducto
        {
            Id = Guid.NewGuid(),
            Nombre = "Baterias",
            StockMinimoDefault = 4,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        var catInactiva = new CategoriaProducto
        {
            Id = Guid.NewGuid(),
            Nombre = "  BATERIAS  ", // Mismo nombre normalizado pero INACTIVA
            StockMinimoDefault = 2,
            Activo = false,
            FechaCreacion = DateTime.UtcNow.AddMonths(-1)
        };
        context.CategoriasProducto.AddRange(catActiva, catInactiva);
        await context.SaveChangesAsync();

        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        using var stream = CrearLibroExcel(
            llenarClientes: EscribirClienteEjemplo,
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                ws.Cell(2, 1).Value = "BAT-001";
                ws.Cell(2, 2).Value = "Batería Yuasa 12V";
                ws.Cell(2, 3).Value = "baterias";
                ws.Cell(2, 4).Value = "unidad";
                ws.Cell(2, 5).Value = 120;
                ws.Cell(2, 6).Value = 180;
                ws.Cell(2, 7).Value = 3;
            });

        // 1. Validar: la inactiva no debe interferir ni crear ambigüedad
        var resultadoValidacion = await service.ValidarArchivoAsync(stream, "baterias.xlsx", null, null);
        Assert.True(resultadoValidacion.Exitoso);
        Assert.Empty(resultadoValidacion.Errores);
        Assert.Equal(1, resultadoValidacion.Repuestos?.FilasValidas);

        // 2. Ejecutar: debe asociar al ID de la activa, no crear otra
        stream.Position = 0;
        var resultadoEjecucion = await service.EjecutarImportacionAsync(stream, "baterias.xlsx", null, null);
        Assert.True(resultadoEjecucion.Exitoso);
        Assert.Equal(1, resultadoEjecucion.Repuestos?.RegistrosCreados);

        var prod = await context.Productos.FirstOrDefaultAsync(p => p.Codigo == "BAT-001");
        Assert.NotNull(prod);
        Assert.Equal(catActiva.Id, prod.CategoriaId);
        Assert.Equal(2, await context.CategoriasProducto.CountAsync()); // No se agregaron categorías
    }

    [Fact]
    public async Task ValidarArchivoAsync_ConAmbiguedadEnCategorias_NoDebeModificarBaseDeDatos()
    {
        using var context = CrearDbContextMemoria();
        var cat1 = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Filtros", Activo = true, FechaCreacion = DateTime.UtcNow };
        var cat2 = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "FILTROS", Activo = true, FechaCreacion = DateTime.UtcNow };
        context.CategoriasProducto.AddRange(cat1, cat2);
        await context.SaveChangesAsync();

        int cantClientesAntes = await context.Clientes.CountAsync();
        int cantVehiculosAntes = await context.Vehiculos.CountAsync();
        int cantProductosAntes = await context.Productos.CountAsync();
        int cantCategoriasAntes = await context.CategoriasProducto.CountAsync();

        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        using var stream = CrearLibroExcel(
            llenarClientes: ws =>
            {
                EscribirCabecera(ws, ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"]);
                ws.Cell(2, 1).Value = "DNI";
                ws.Cell(2, 2).Value = "12345678";
                ws.Cell(2, 3).Value = "Juan Perez";
            },
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                ws.Cell(2, 1).Value = "FIL-001";
                ws.Cell(2, 2).Value = "Filtro de aire";
                ws.Cell(2, 3).Value = "filtros"; // Categ ambigua
                ws.Cell(2, 4).Value = "unidad";
                ws.Cell(2, 5).Value = 15;
                ws.Cell(2, 6).Value = 30;
                ws.Cell(2, 7).Value = 0;
            });

        var resultado = await service.ValidarArchivoAsync(stream, "ambiguedad_dryrun.xlsx", null, null);

        Assert.False(resultado.Exitoso);
        Assert.True(resultado.SoloValidacion);
        Assert.NotEmpty(resultado.Errores);

        // Verificación estricta de ausencia de escrituras
        Assert.Equal(cantClientesAntes, await context.Clientes.CountAsync());
        Assert.Equal(cantVehiculosAntes, await context.Vehiculos.CountAsync());
        Assert.Equal(cantProductosAntes, await context.Productos.CountAsync());
        Assert.Equal(cantCategoriasAntes, await context.CategoriasProducto.CountAsync());
        Assert.False(context.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task ValidarYEjecutar_CategoriaDuplicadaEnBdPeroExcelUsaOtraCategoriaUnica_DebeProcesarNormalmente()
    {
        using var context = CrearDbContextMemoria();
        // Categorías duplicadas en BD no referenciadas por el Excel
        var dup1 = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Herramientas", Activo = true, FechaCreacion = DateTime.UtcNow };
        var dup2 = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "HERRAMIENTAS", Activo = true, FechaCreacion = DateTime.UtcNow };
        // Categoría única referenciada por el Excel
        var unica = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Aceites", Activo = true, FechaCreacion = DateTime.UtcNow };
        context.CategoriasProducto.AddRange(dup1, dup2, unica);
        await context.SaveChangesAsync();

        var fakeAuditoria = new FakeAuditoriaService();
        var service = new ImportadorExcelService(context, fakeAuditoria);

        using var stream = CrearLibroExcel(
            llenarClientes: EscribirClienteEjemplo,
            llenarRepuestos: ws =>
            {
                EscribirCabecera(ws, ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo"]);
                ws.Cell(2, 1).Value = "ACE-001";
                ws.Cell(2, 2).Value = "Aceite 2T";
                ws.Cell(2, 3).Value = "Aceites";
                ws.Cell(2, 4).Value = "litro";
                ws.Cell(2, 5).Value = 20;
                ws.Cell(2, 6).Value = 35;
                ws.Cell(2, 7).Value = 5;
            });

        var resultadoValidacion = await service.ValidarArchivoAsync(stream, "aceites.xlsx", null, null);
        Assert.True(resultadoValidacion.Exitoso);
        Assert.Empty(resultadoValidacion.Errores);

        stream.Position = 0;
        var resultadoEjecucion = await service.EjecutarImportacionAsync(stream, "aceites.xlsx", null, null);
        Assert.True(resultadoEjecucion.Exitoso);
        Assert.Equal(1, resultadoEjecucion.Repuestos?.RegistrosCreados);

        var prod = await context.Productos.FirstOrDefaultAsync(p => p.Codigo == "ACE-001");
        Assert.NotNull(prod);
        Assert.Equal(unica.Id, prod.CategoriaId);
    }
}
