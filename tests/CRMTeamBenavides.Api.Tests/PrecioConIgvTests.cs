using ClosedXML.Excel;
using CRMTeamBenavides.Api.Features.Inventario;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.Importacion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// El precio de venta se registra en su moneda y con IGV incluido por defecto: se cobra
/// exactamente el precio de lista y el sistema separa la base y el IGV.
/// </summary>
public class PrecioConIgvTests
{
    private const decimal Igv = 18m;

    private static Producto Repuesto(decimal precio, MonedaPrecio moneda = MonedaPrecio.PEN, bool incluyeIgv = true) =>
        new() { Nombre = "Filtro de aceite", PrecioVenta = precio, MonedaPrecio = moneda, PrecioIncluyeIgv = incluyeIgv, Activo = true };

    [Fact]
    public void Un_precio_con_IGV_en_soles_se_cobra_tal_cual_y_se_separa_la_base()
    {
        var lista = PrecioLista.Calcular(Repuesto(10.00m), tipoCambio: null, Igv)!.Value;

        Assert.Equal(10.00m, lista.ConIgv);
        Assert.Equal(8.47m, lista.SinIgv);
        Assert.Equal(10.00m, lista.Referencia);
    }

    [Fact]
    public void Un_precio_sin_IGV_queda_como_hasta_ahora()
    {
        var lista = PrecioLista.Calcular(Repuesto(29.66m, incluyeIgv: false), tipoCambio: null, Igv)!.Value;

        Assert.Null(lista.ConIgv);
        Assert.Equal(29.66m, lista.Referencia);
        Assert.Equal((29.66m, 5.34m, 35.00m, Igv), OrdenServicioService.CalcularFinanzasLinea(1, 29.66m, TipoAfectacionIgv.Gravado, Igv));
    }

    [Fact]
    public void En_dolares_se_convierte_con_el_tipo_de_cambio_del_dia_hacia_arriba_al_sol()
    {
        var lista = PrecioLista.Calcular(Repuesto(25.00m, MonedaPrecio.USD), tipoCambio: 3.75m, Igv)!.Value;

        Assert.Equal(94.00m, lista.ConIgv); // 93,75 sube a 94
        Assert.Equal(79.66m, lista.SinIgv);
    }

    [Fact]
    public void En_dolares_sin_tipo_de_cambio_no_hay_precio_de_lista()
    {
        Assert.Null(PrecioLista.Calcular(Repuesto(25.00m, MonedaPrecio.USD), tipoCambio: null, Igv));
    }

    [Fact]
    public void Ningun_precio_con_IGV_pierde_un_centimo_al_cobrarlo()
    {
        // Todos los precios de S/ 0,01 a S/ 300,00, de a 1, 2, 3 y 7 unidades.
        for (var centimos = 1; centimos <= 30000; centimos++)
        {
            var precio = centimos / 100m;
            foreach (var cantidad in new[] { 1, 2, 3, 7 })
            {
                var (baseImponible, igv, total, _) = OrdenServicioService.CalcularFinanzasLinea(
                    cantidad, PrecioLista.QuitarIgv(precio, Igv), TipoAfectacionIgv.Gravado, Igv, precio);

                Assert.Equal(precio * cantidad, total);
                Assert.Equal(total, baseImponible + igv);
            }
        }
    }

    [Fact]
    public void Una_linea_exonerada_no_lleva_IGV_aunque_el_repuesto_lo_incluya()
    {
        var precio = PrecioLista.DeEscrito(10.00m, incluyeIgv: true, TipoAfectacionIgv.Exonerado, Igv);

        Assert.Null(precio.ConIgv);
        Assert.Equal(10.00m, precio.SinIgv);
    }

    // ------------------------------------------------------------------ Registro del repuesto

    private static ApplicationDbContext Contexto(string nombreBase) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(nombreBase)
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private sealed class SinAuditoria : IAuditoriaService
    {
        public Task RegistrarEventoAsync(Guid? usuarioId, string accion, string entidad, string entidadId, object? detalle = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private static async Task<(ApplicationDbContext Contexto, Guid CategoriaId)> InventarioAsync(decimal? tipoCambio)
    {
        var contexto = Contexto(Guid.NewGuid().ToString());
        var categoria = new CategoriaProducto { Nombre = "Filtros", Activo = true };
        contexto.CategoriasProducto.Add(categoria);
        contexto.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa { PorcentajeIgv = Igv, TipoCambioVigente = tipoCambio });
        await contexto.SaveChangesAsync();
        return (contexto, categoria.Id);
    }

    [Fact]
    public async Task Un_repuesto_nuevo_se_registra_con_IGV_por_defecto_y_responde_su_precio_en_soles()
    {
        var (contexto, categoriaId) = await InventarioAsync(tipoCambio: 3.75m);
        var servicio = new InventarioService(contexto, new SinAuditoria());

        var creado = await servicio.CreateProductoAsync(new CreateProductoRequest(
            categoriaId, "FIL-01", "Filtro de aceite", null, "UND", 25.00m, 0, Costo: 0m, MonedaPrecio: MonedaPrecio.USD));

        Assert.True(creado.IsSuccess, creado.Error);
        Assert.Equal(25.00m, creado.Data!.PrecioVenta);
        Assert.Equal(MonedaPrecio.USD, creado.Data.MonedaPrecio);
        Assert.True(creado.Data.PrecioIncluyeIgv);
        Assert.Equal(94.00m, creado.Data.PrecioListaSoles);
    }

    [Fact]
    public async Task El_costo_en_dolares_y_con_IGV_se_guarda_una_sola_vez_en_soles_y_sin_IGV()
    {
        var (contexto, categoriaId) = await InventarioAsync(tipoCambio: 3.75m);
        var servicio = new InventarioService(contexto, new SinAuditoria());

        var creado = await servicio.CreateProductoAsync(new CreateProductoRequest(
            categoriaId, "FIL-02", "Filtro de aire", null, "UND", 60.00m, 4,
            Costo: 11.80m, MonedaCosto: MonedaPrecio.USD, CostoIncluyeIgv: true));

        Assert.True(creado.IsSuccess, creado.Error);
        // 11,80 × 3,75 = 44,25 con IGV → 37,50 sin IGV
        Assert.Equal(37.50m, creado.Data!.Costo);
        var movimiento = await contexto.MovimientosInventario.SingleAsync();
        Assert.Equal(37.50m, movimiento.CostoUnitario);
    }

    [Fact]
    public async Task Sin_tipo_de_cambio_no_se_registra_un_costo_en_dolares()
    {
        var (contexto, categoriaId) = await InventarioAsync(tipoCambio: null);
        var servicio = new InventarioService(contexto, new SinAuditoria());

        var creado = await servicio.CreateProductoAsync(new CreateProductoRequest(
            categoriaId, "FIL-03", "Filtro", null, "UND", 60.00m, 0, Costo: 10m, MonedaCosto: MonedaPrecio.USD));

        Assert.False(creado.IsSuccess);
        Assert.Contains("tipo de cambio", creado.Error);
    }

    [Fact]
    public async Task Al_editar_sin_moneda_ni_IGV_el_repuesto_los_conserva()
    {
        var (contexto, categoriaId) = await InventarioAsync(tipoCambio: 3.75m);
        var servicio = new InventarioService(contexto, new SinAuditoria());
        var creado = await servicio.CreateProductoAsync(new CreateProductoRequest(
            categoriaId, "FIL-04", "Filtro", null, "UND", 25.00m, 0, MonedaPrecio: MonedaPrecio.USD));

        var editado = await servicio.UpdateProductoAsync(creado.Data!.Id, new UpdateProductoRequest(
            categoriaId, "FIL-04", "Filtro de aceite", null, "UND", 26.00m));

        Assert.True(editado.IsSuccess, editado.Error);
        Assert.Equal(MonedaPrecio.USD, editado.Data!.MonedaPrecio);
        Assert.True(editado.Data.PrecioIncluyeIgv);
        Assert.Equal(98.00m, editado.Data.PrecioListaSoles); // 26 × 3,75 = 97,5 → 98
    }

    // ------------------------------------------------------------------ Importador

    private static MemoryStream LibroDeRepuestos(params object[][] filas)
    {
        using var libro = new XLWorkbook();
        var clientes = libro.Worksheets.Add("Clientes");
        string[] cabecerasClientes = ["TipoDocumento", "NumeroDocumento", "NombreCompleto", "Telefono", "Email", "Direccion"];
        for (var i = 0; i < cabecerasClientes.Length; i++) clientes.Cell(1, i + 1).Value = cabecerasClientes[i];
        var hoja = libro.Worksheets.Add("Repuestos");
        string[] cabeceras = ["Codigo", "Nombre", "Categoria", "UnidadMedida", "Costo", "PrecioVenta", "StockActual", "StockMinimo", "Moneda", "IncluyeIgv"];
        for (var i = 0; i < cabeceras.Length; i++) hoja.Cell(1, i + 1).Value = cabeceras[i];
        for (var f = 0; f < filas.Length; f++)
        {
            for (var c = 0; c < filas[f].Length; c++)
            {
                hoja.Cell(f + 2, c + 1).Value = XLCellValue.FromObject(filas[f][c]);
            }
        }
        var archivo = new MemoryStream();
        libro.SaveAs(archivo);
        archivo.Position = 0;
        return archivo;
    }

    [Fact]
    public async Task El_importador_lee_la_moneda_y_si_incluye_IGV_y_sin_ellas_toma_soles_con_IGV()
    {
        using var contexto = Contexto(Guid.NewGuid().ToString());
        var servicio = new ImportadorExcelService(contexto, new SinAuditoria());

        using var archivo = LibroDeRepuestos(
            ["R-SOL", "En soles con IGV", "Filtros", "UND", 20, 35, 1, 0, "", ""],
            ["R-USD", "En dólares sin IGV", "Filtros", "UND", 20, 25, 1, 0, "Dólares", "No"]);
        var resultado = await servicio.EjecutarImportacionAsync(archivo, "precios.xlsx", null, null);

        Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores.Select(e => e.Mensaje)));
        var enSoles = await contexto.Productos.SingleAsync(p => p.Codigo == "R-SOL");
        var enDolares = await contexto.Productos.SingleAsync(p => p.Codigo == "R-USD");
        Assert.Equal((MonedaPrecio.PEN, true), (enSoles.MonedaPrecio, enSoles.PrecioIncluyeIgv));
        Assert.Equal((MonedaPrecio.USD, false), (enDolares.MonedaPrecio, enDolares.PrecioIncluyeIgv));
    }

    [Fact]
    public async Task El_importador_marca_una_moneda_que_no_reconoce()
    {
        using var contexto = Contexto(Guid.NewGuid().ToString());
        var servicio = new ImportadorExcelService(contexto, new SinAuditoria());

        using var archivo = LibroDeRepuestos(["R-EUR", "En euros", "Filtros", "UND", 20, 25, 1, 0, "EUR", "Sí"]);
        var validacion = await servicio.ValidarArchivoAsync(archivo, "precios.xlsx", null, null);

        var error = Assert.Single(validacion.Errores);
        Assert.Equal("Moneda", error.Columna);
        Assert.Equal(2, error.Fila);
    }
}
