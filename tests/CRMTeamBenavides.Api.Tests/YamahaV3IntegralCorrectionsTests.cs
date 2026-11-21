using System.Net;
using System.Text;
using System.Text.Json;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha.Exportadores;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Suite integral de regresión y cobertura para los tres servicios Yamaha v3
/// (ReceiveRetailVentas, ReceiveRetailStock, ReceiveRetailServicioTecnico)
/// en las cinco líneas de producto/operación: MC, ATV, OM, SP y STE.
///
/// Garantía de seguridad: todas las pruebas son 100 % locales en memoria,
/// sin llamadas HTTP externas ni alteración de bases compartidas.
/// </summary>
public class YamahaV3IntegralCorrectionsTests
{
    private static ApplicationDbContext CrearDbContextMemoria()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa
        {
            RazonSocial = "Team Benavides S.R.L.",
            Ruc = "20455687765",
            NombreEmpresa = "Team Benavides Arequipa",
            MonedaBase = "PEN",
            PorcentajeIgv = 18.00m
        });
        context.SaveChanges();
        return context;
    }

    private static IOptions<YamahaSettings> CrearOpciones(Action<YamahaSettings>? configurar = null)
    {
        var settings = new YamahaSettings
        {
            Enabled = false,
            CodigoPuntoVenta = "20455687765_1",
            NombreComercialPuntoVenta = "Team Benavides",
            TipoPuntoVenta = "3S"
        };
        configurar?.Invoke(settings);
        return Options.Create(settings);
    }

    private static YamahaPuntoVentaContexto CrearContexto() => new(
        RazonSocial: "Team Benavides S.R.L.",
        Ruc: "20455687765",
        CodigoPuntoVenta: "20455687765_1",
        NombreComercialPuntoVenta: "Team Benavides",
        TipoPuntoVenta: "3S",
        PorcentajeIgv: 18.00m);

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respuestas = new();
        public List<HttpRequestMessage> Peticiones { get; } = new();

        public void Encolar(HttpStatusCode status, string body)
        {
            _respuestas.Enqueue(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones.Add(request);
            return Task.FromResult(_respuestas.Count > 0
                ? _respuestas.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"total\":1,\"exitosos\":1,\"fallidos\":0,\"status\":\"ok\"}", Encoding.UTF8, "application/json")
                });
        }
    }

    // =========================================================================
    // 1. Ventas: ATV y OM vía MotosInventario.VenderAsync (flujo real completo)
    // =========================================================================

    [Fact]
    public async Task Ventas_ATV_FlujoCompletoConMotosInventarioVenderAsync_SeExportaComoATV_YNoComoMC()
    {
        using var context = CrearDbContextMemoria();
        var catAtv = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Cuatrimotos", Activo = true };
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "45678901",
            NombreCompleto = "Luis Quispe",
            Telefono = "+51 987-654-321",
            Email = "luis.quispe@example.com",
            Activo = true
        };
        // Modelo ATV sin la palabra "Cuatrimoto" en el nombre: depende de Categoria y TipoUnidad
        var prodAtv = new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = catAtv.Id,
            Categoria = catAtv,
            Codigo = "YFM700-WOLV",
            Nombre = "Yamaha Wolverine RMAX2 1000",
            Marca = "YAMAHA",
            Tipo = TipoProducto.Vehiculo,
            StockActual = 1,
            Costo = 45000m,
            PrecioVenta = 55000m,
            Activo = true
        };
        var unidad = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodAtv.Id,
            Producto = prodAtv,
            NumeroSerieVIN = "5Y4AJ95E6TA101267",
            NumeroMotor = "M609E-001234",
            Anio = 2026,
            Color = "Azul  Mate",
            CostoUnitario = 45000m,
            Estado = EstadoUnidadInventario.EnStock,
            Activo = true
        };

        context.CategoriasProducto.Add(catAtv);
        context.Clientes.Add(cliente);
        context.Productos.Add(prodAtv);
        context.UnidadesInventario.Add(unidad);
        await context.SaveChangesAsync();

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Estado = EstadoVenta.Confirmada,
            Declarada = false, // Declaración interna independiente de Yamaha
            Fecha = new DateTime(2026, 11, 15, 16, 0, 0, DateTimeKind.Utc),
            SubtotalGravado = 46610.17m,
            MontoIgv = 8389.83m,
            Total = 55000.00m,
            Activo = true
        };
        var detalle = new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Venta = venta,
            ProductoId = prodAtv.Id,
            Producto = prodAtv,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 46610.17m,
            SubtotalGravado = 46610.17m,
            Total = 55000.00m,
            UnidadesInventarioIds = new List<Guid> { unidad.Id },
            Activo = true
        };
        venta.Detalles.Add(detalle);
        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000501",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 11, 15, 16, 0, 0, DateTimeKind.Utc),
            Activo = true
        };
        context.Ventas.Add(venta);

        // Ejecuta el flujo real de venta de unidades
        var errorVenta = await MotosInventario.VenderAsync(context, venta, detalle, prodAtv);
        Assert.Null(errorVenta);
        await context.SaveChangesAsync();

        // Verifica que el vehículo del cliente se creó como Cuatrimoto, NO como Motocicleta
        var vehiculoCreado = await context.Vehiculos.SingleAsync(v => v.NumeroSerieVIN == "5Y4AJ95E6TA101267");
        Assert.Equal(TipoUnidad.Cuatrimoto, vehiculoCreado.TipoUnidad);

        var exporter = new YamahaVentasExporter(context, new YamahaMappingService(), CrearOpciones());
        var res = await exporter.ExportarVentasAsync(
            desdeUtc: new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc),
            hastaUtc: new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, res.TotalRegistrosValidos);
        var dto = res.RegistrosValidos[0];
        Assert.Equal("ATV", dto.LineaNegocio);
        Assert.Equal("5Y4AJ95E6TA101267", dto.VIN);
        Assert.Equal("M609E-001234", dto.NumeroMotor);
        Assert.Equal(2026, dto.AnioModelo);
        Assert.Equal("Azul Mate", dto.Color);           // doble espacio colapsado
        Assert.Equal("987654321", dto.CelularCliente);  // +51 y guiones sanitizados
        Assert.Equal("15/11/2026", dto.FechaFacturacion);

        var val = YamahaV3Validator.ValidarLoteVentas(res.RegistrosValidos);
        Assert.True(val.EsLoteValido, string.Join("; ", val.Hallazgos.Select(h => $"{h.Campo}: {h.Mensaje}")));
        Assert.Equal(0, val.TotalAdvertencias);
    }

    [Fact]
    public async Task Ventas_OM_MotorFueraDeBorda_CumpleReglasSeccion4SinAnioNiNumeroMotor()
    {
        using var context = CrearDbContextMemoria();
        var catOm = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Motores Fuera de Borda", Activo = true };
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            TipoDocumento = TipoDocumentoCliente.RUC,
            NumeroDocumento = "20555666777",
            NombreCompleto = "Pesquera del Sur S.A.C.",
            Telefono = "054-223344", // teléfono fijo -> debe sanitizarse a "" sin tumbar el lote
            Email = "correo-invalido", // correo inválido -> debe sanitizarse a "" sin tumbar el lote
            Activo = true
        };
        var prodOm = new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = catOm.Id,
            Categoria = catOm,
            Codigo = "F115BETX",
            Nombre = "Motor Fuera de Borda Yamaha F115BETX",
            Marca = "YAMAHA",
            Tipo = TipoProducto.Vehiculo,
            StockActual = 1,
            Costo = 38000m,
            PrecioVenta = 46000m,
            Activo = true
        };
        // Serie marina corta (< 15 chars, válida en VARCHAR(20)) y sin NumeroMotor/Anio/Color obligatorios
        var unidadOm = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodOm.Id,
            Producto = prodOm,
            NumeroSerieVIN = "63P-1045678",
            NumeroMotor = null,
            Anio = null,
            Color = null,
            CostoUnitario = 38000m,
            Estado = EstadoUnidadInventario.EnStock,
            Activo = true
        };

        context.CategoriasProducto.Add(catOm);
        context.Clientes.Add(cliente);
        context.Productos.Add(prodOm);
        context.UnidadesInventario.Add(unidadOm);
        await context.SaveChangesAsync();

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Estado = EstadoVenta.Confirmada,
            Fecha = new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc),
            SubtotalGravado = 38983.05m,
            MontoIgv = 7016.95m,
            Total = 46000.00m,
            Activo = true
        };
        var detalle = new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Venta = venta,
            ProductoId = prodOm.Id,
            Producto = prodOm,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 38983.05m,
            SubtotalGravado = 38983.05m,
            Total = 46000.00m,
            UnidadesInventarioIds = new List<Guid> { unidadOm.Id },
            Activo = true
        };
        venta.Detalles.Add(detalle);
        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Factura",
            Serie = "F001",
            Numero = "000502",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc),
            Activo = true
        };
        context.Ventas.Add(venta);

        var errorVenta = await MotosInventario.VenderAsync(context, venta, detalle, prodOm);
        Assert.Null(errorVenta);
        await context.SaveChangesAsync();

        var vehiculoOm = await context.Vehiculos.SingleAsync(v => v.NumeroSerieVIN == "63P-1045678");
        Assert.Equal(TipoUnidad.MotorFueraDeBorda, vehiculoOm.TipoUnidad);
        Assert.Equal(TipoMedidor.Horas, vehiculoOm.TipoMedidor);

        var exporter = new YamahaVentasExporter(context, new YamahaMappingService(), CrearOpciones());
        var res = await exporter.ExportarVentasAsync(
            desdeUtc: new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc),
            hastaUtc: new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, res.TotalRegistrosValidos);
        var dto = res.RegistrosValidos[0];
        Assert.Equal("OM", dto.LineaNegocio);
        Assert.Equal("63P-1045678", dto.VIN);
        Assert.Null(dto.AnioModelo);
        Assert.Equal(string.Empty, dto.NumeroMotor);
        Assert.Equal(string.Empty, dto.CelularCliente);
        Assert.Equal(string.Empty, dto.CorreoCliente);

        var val = YamahaV3Validator.ValidarLoteVentas(res.RegistrosValidos);
        Assert.True(val.EsLoteValido, string.Join("; ", val.Hallazgos.Select(h => $"{h.Campo}: {h.Mensaje}")));
        Assert.Equal(0, val.TotalAdvertencias);

        // Verifica serialización JSON con campos oficiales confirmados por Yamaha (TipoDocumento / NumeroDocumento y AnioModelo=0)
        var json = YamahaJsonSerializer.SerializarVentas(res.RegistrosValidos);
        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement[0];
        Assert.Equal("OM", item.GetProperty("LineaNegocio").GetString());
        Assert.Equal(0, item.GetProperty("AnioModelo").GetInt32());
        Assert.Equal("", item.GetProperty("NumeroMotor").GetString());
        Assert.Equal("RUC", item.GetProperty("TipoDocumento").GetString());
        Assert.Equal("20555666777", item.GetProperty("NumeroDocumento").GetString());
    }

    // =========================================================================
    // 2. Stock: MC, ATV (por categoría), OM (con fecha de compra), SP y exclusiones
    // =========================================================================

    [Fact]
    public async Task Stock_DebeExportarMC_ATV_OM_Y_SP_ConClasificacionExplicita_YExcluirInactivosYSinClasificar()
    {
        using var context = CrearDbContextMemoria();
        var catMc = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Motocicletas", Activo = true };
        var catAtv = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Cuatrimotos", Activo = true };
        var catOm = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Línea Náutica", Activo = true };
        var catGen = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Generadores", Activo = true };
        var catSin = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Promociones", Activo = true }; // contiene "om" pero NO es OM
        var catRep = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Repuestos", Activo = true };

        var proveedor = new Proveedor { Id = Guid.NewGuid(), NumeroDocumento = "20383929051", RazonSocial = "Yamaha Motor del Perú S.A.", Activo = true };
        var compraOm = new Compra
        {
            Id = Guid.NewGuid(),
            NumeroCompra = "CMP-000010",
            ProveedorId = proveedor.Id,
            Proveedor = proveedor,
            TipoComprobante = TipoComprobanteCompra.Factura,
            Serie = "F001",
            Numero = "000123",
            FechaEmision = new DateOnly(2026, 11, 2),
            Estado = EstadoCompra.Registrada,
            Activo = true
        };

        var prodMc = new Producto { Id = Guid.NewGuid(), CategoriaId = catMc.Id, Codigo = "MT03-2026", Nombre = "Yamaha MT-03 ABS", Marca = "YAMAHA", Tipo = TipoProducto.Vehiculo, Activo = true };
        // ATV sin palabra clave en el nombre -> debe detectarse por catAtv
        var prodAtv = new Producto { Id = Guid.NewGuid(), CategoriaId = catAtv.Id, Codigo = "YXE85-2026", Nombre = "Yamaha X2 850 Special", Marca = "YAMAHA", Tipo = TipoProducto.Vehiculo, Activo = true };
        // OM -> debe detectarse por catOm y usar FechaEmision de compraOm
        var prodOm = new Producto { Id = Guid.NewGuid(), CategoriaId = catOm.Id, Codigo = "E40GMHL", Nombre = "Motor E40GMHL 40HP", Marca = "YAMAHA", Tipo = TipoProducto.Vehiculo, Activo = true };
        // Generador -> NO reportable, debe omitirse explícitamente (nunca caer a MC)
        var prodGen = new Producto { Id = Guid.NewGuid(), CategoriaId = catGen.Id, Codigo = "EF2000IS", Nombre = "Generador Yamaha EF2000iS", Marca = "YAMAHA", Tipo = TipoProducto.Vehiculo, Activo = true };
        // Vehículo en categoría "Promociones" (contiene "om") sin clasificación -> NO debe caer ni a OM ni a MC
        var prodAmbiguo = new Producto { Id = Guid.NewGuid(), CategoriaId = catSin.Id, Codigo = "PROMO-01", Nombre = "Unidad Especial X99", Marca = "YAMAHA", Tipo = TipoProducto.Vehiculo, Activo = true };
        // Modelo inactivo con unidad EnStock -> debe excluirse
        var prodInactivo = new Producto { Id = Guid.NewGuid(), CategoriaId = catMc.Id, Codigo = "OLD-MC", Nombre = "Yamaha YB125 Descontinuada", Marca = "YAMAHA", Tipo = TipoProducto.Vehiculo, Activo = false };
        // Repuesto SP con stock 0 (válido) y repuesto con SKU inválido (aislado sin romper el lote)
        var prodSpCero = new Producto { Id = Guid.NewGuid(), CategoriaId = catRep.Id, Codigo = "90793AY42700", Nombre = "Aceite Yamalube 10W40", Marca = "YAMAHA", Tipo = TipoProducto.Repuesto, StockActual = 0, Costo = 31.72m, Activo = true };
        var prodSpSkuInvalido = new Producto { Id = Guid.NewGuid(), CategoriaId = catRep.Id, Codigo = "6KM#0450016000", Nombre = "Repuesto con simbolo", Marca = "YAMAHA", Tipo = TipoProducto.Repuesto, StockActual = 5, Costo = 10m, Activo = true };

        context.CategoriasProducto.AddRange(catMc, catAtv, catOm, catGen, catSin, catRep);
        context.Proveedores.Add(proveedor);
        context.Compras.Add(compraOm);
        context.Productos.AddRange(prodMc, prodAtv, prodOm, prodGen, prodAmbiguo, prodInactivo, prodSpCero, prodSpSkuInvalido);
        context.UnidadesInventario.AddRange(
            new UnidadInventario { Id = Guid.NewGuid(), ProductoId = prodMc.Id, NumeroSerieVIN = "MH3RH25RXTK004019", CostoUnitario = 22000m, Estado = EstadoUnidadInventario.EnStock, Activo = true },
            new UnidadInventario { Id = Guid.NewGuid(), ProductoId = prodAtv.Id, NumeroSerieVIN = "5Y4AJ95E6TA109999", CostoUnitario = 41000m, Estado = EstadoUnidadInventario.EnStock, Activo = true },
            new UnidadInventario { Id = Guid.NewGuid(), ProductoId = prodOm.Id, NumeroSerieVIN = "66T-L-1054321", CostoUnitario = 12500m, CompraId = compraOm.Id, Estado = EstadoUnidadInventario.EnStock, Activo = true },
            new UnidadInventario { Id = Guid.NewGuid(), ProductoId = prodGen.Id, NumeroSerieVIN = "GEN00000000000001", CostoUnitario = 4500m, Estado = EstadoUnidadInventario.EnStock, Activo = true },
            new UnidadInventario { Id = Guid.NewGuid(), ProductoId = prodAmbiguo.Id, NumeroSerieVIN = "AMB00000000000001", CostoUnitario = 8000m, Estado = EstadoUnidadInventario.EnStock, Activo = true },
            new UnidadInventario { Id = Guid.NewGuid(), ProductoId = prodInactivo.Id, NumeroSerieVIN = "LBPRE1321T0016907", CostoUnitario = 5000m, Estado = EstadoUnidadInventario.EnStock, Activo = true }
        );
        await context.SaveChangesAsync();

        var exporter = new YamahaStockExporter(context, new YamahaMappingService(), CrearOpciones());
        var res = await exporter.ExportarStockAsync(soloMarcaYamaha: true);

        Assert.Equal(4, res.TotalRegistrosValidos); // MC + ATV + OM + SP(0)
        Assert.Equal(3, res.TotalRegistrosOmitidos); // Generador + Ambiguo + SKU inválido (#)
        Assert.Contains(res.RegistrosValidos, r => r.LineaNegocio == "MC" && r.VIN == "MH3RH25RXTK004019" && r.StockDisponible == 1);
        Assert.Contains(res.RegistrosValidos, r => r.LineaNegocio == "ATV" && r.VIN == "5Y4AJ95E6TA109999" && r.StockDisponible == 1);
        Assert.Contains(res.RegistrosValidos, r => r.LineaNegocio == "OM" && r.VIN == "66T-L-1054321" && r.StockDisponible == 1 && r.FechaIngreso == "02/11/2026");
        Assert.Contains(res.RegistrosValidos, r => r.LineaNegocio == "SP" && r.CodigoYamaha == "90793AY42700" && r.StockDisponible == 0);
        Assert.DoesNotContain(res.RegistrosValidos, r => r.LineaNegocio == "MV");
        Assert.DoesNotContain(res.RegistrosValidos, r => r.CodigoYamaha is "EF2000IS" or "PROMO-01" or "OLD-MC" or "6KM#0450016000");

        var val = YamahaV3Validator.ValidarLoteStock(res.RegistrosValidos);
        Assert.True(val.EsLoteValido, string.Join("; ", val.Hallazgos.Select(h => $"{h.Campo}: {h.Mensaje}")));
        Assert.Equal(0, val.TotalAdvertencias);
    }

    // =========================================================================
    // 3. Servicio Técnico: Cancelada, doble Any, Descripcion STE, Gratuita y Combinados
    // =========================================================================

    [Fact]
    public async Task ServicioTecnico_DebeExcluirCanceladas_NoDuplicarPorMultiplesComprobantes_YRespetarContratoSTE()
    {
        using var context = CrearDbContextMemoria();
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "71234567",
            NombreCompleto = "María Torres",
            Telefono = "+51 955 111 222",
            Email = "maria@example.com",
            Activo = true
        };
        var vehiculo = new Vehiculo
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Placa = "1234-AB",
            NumeroSerieVIN = "MH3RG5676TK007534",
            Marca = "YAMAHA",
            Modelo = "MT-15",
            Anio = 2026,
            Kilometraje = 5000,
            Activo = true
        };
        var prodRepuesto = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "2DP-E4451-00",
            Nombre = "Filtro de Aire Original",
            Marca = "YAMAHA",
            Tipo = TipoProducto.Repuesto,
            Activo = true
        };

        // 1. Orden cancelada CON comprobante emitido -> DEBE EXCLUIRSE
        var ordenCancelada = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000900",
            ClienteId = cliente.Id,
            VehiculoId = vehiculo.Id,
            Estado = EstadoOrdenServicio.Cancelada,
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            Activo = true
        };
        ordenCancelada.Comprobantes.Add(new Comprobante
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = ordenCancelada.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000900",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 11, 15, 16, 0, 0, DateTimeKind.Utc),
            Activo = true
        });
        ordenCancelada.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = ordenCancelada.Id,
            TipoItem = TipoItemServicio.ManoDeObra,
            Descripcion = "Servicio cancelado",
            Cantidad = 1,
            PrecioUnitario = 100m,
            Total = 118m,
            SubtotalGravado = 100m,
            Activo = true
        });

        // 2. Orden con 2 comprobantes que cruzan la ventana (uno antes de desdeUtc, otro después de hastaUtc) -> NO debe coincidir
        var ordenCruzada = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000901",
            ClienteId = cliente.Id,
            VehiculoId = vehiculo.Id,
            Estado = EstadoOrdenServicio.Entregada,
            TipoAtencion = TipoAtencion.MantenimientoCorrectivo,
            Activo = true
        };
        ordenCruzada.Comprobantes.Add(new Comprobante
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = ordenCruzada.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000901",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc), // <= hastaUtc pero <= desdeUtc
            Activo = true
        });
        ordenCruzada.Comprobantes.Add(new Comprobante
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = ordenCruzada.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000902",
            Estado = "Emitido",
            FechaCreacion = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc), // > desdeUtc pero > hastaUtc
            Activo = true
        });
        ordenCruzada.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = ordenCruzada.Id,
            TipoItem = TipoItemServicio.ManoDeObra,
            Descripcion = "Reparacion cruzada",
            Cantidad = 1,
            PrecioUnitario = 80m,
            Total = 94.40m,
            SubtotalGravado = 80m,
            Activo = true
        });

        // 3. Orden válida con Preventivo+Correctivo, Mano de Obra + Repuesto Gratuito (S/ 0.00)
        var ordenValida = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000902",
            ClienteId = cliente.Id,
            VehiculoId = vehiculo.Id,
            Estado = EstadoOrdenServicio.Entregada,
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            TiposAtencion = new List<TipoAtencion> { TipoAtencion.MantenimientoPreventivo, TipoAtencion.MantenimientoCorrectivo },
            KilometrajeIngreso = 5100,
            Activo = true
        };
        ordenValida.Comprobantes.Add(new Comprobante
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = ordenValida.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000903",
            Estado = "Emitido",
            // 20:30 hora Perú del 15/11/2026 = 01:30 UTC del 16/11/2026
            FechaCreacion = new DateTime(2026, 11, 16, 1, 30, 0, DateTimeKind.Utc),
            Activo = true
        });
        ordenValida.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = ordenValida.Id,
            TipoItem = TipoItemServicio.ManoDeObra,
            Descripcion = "Mantenimiento 5000km",
            Cantidad = 1,
            PrecioUnitario = 100m,
            SubtotalGravado = 100m,
            Total = 118m,
            Activo = true
        });
        ordenValida.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = ordenValida.Id,
            TipoItem = TipoItemServicio.Repuesto,
            ProductoId = prodRepuesto.Id,
            Producto = prodRepuesto,
            Descripcion = "Filtro bonificado",
            Cantidad = 1,
            PrecioUnitario = 45m, // valor referencial no cobrado
            TipoAfectacionIgv = TipoAfectacionIgv.GravadoBonificaciones,
            SubtotalGravado = 0m,
            MontoIgv = 0m,
            Total = 0m,
            Activo = true
        });

        context.Clientes.Add(cliente);
        context.Vehiculos.Add(vehiculo);
        context.Productos.Add(prodRepuesto);
        context.OrdenesServicio.AddRange(ordenCancelada, ordenCruzada, ordenValida);
        await context.SaveChangesAsync();

        var exporter = new YamahaServicioTecnicoExporter(context, new YamahaMappingService(), CrearOpciones());
        var res = await exporter.ExportarServicioTecnicoAsync(
            desdeUtc: new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc),
            hastaUtc: new DateTime(2026, 11, 16, 5, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, res.TotalRegistrosValidos);
        Assert.All(res.RegistrosValidos, r => Assert.Equal("OT-000902", r.NumeroOrdenTrabajo));
        Assert.All(res.RegistrosValidos, r => Assert.Equal("15/11/2026", r.FechaFacturacion)); // Hora Perú, NO 16/11/2026
        Assert.All(res.RegistrosValidos, r => Assert.Equal("955111222", r.Celular));
        Assert.All(res.RegistrosValidos, r => Assert.Equal(string.Empty, r.Version)); // Nunca el año 2026

        var lineaSte = res.RegistrosValidos.Single(r => r.SubLineaNegocio == "STE");
        Assert.Equal(string.Empty, lineaSte.Descripcion); // Coincide con Spec 6.2 y payload aceptado el 28/10
        Assert.Equal("MP", lineaSte.TipoServicio);        // No inventa MP+R sin confirmación
        Assert.Contains(res.ObservacionesYAdvertencias, o => o.Contains("múltiples tipos de atención"));

        var lineaSp = res.RegistrosValidos.Single(r => r.SubLineaNegocio == "SP");
        Assert.Equal("-", lineaSp.TipoServicio);
        Assert.Equal(0.00m, lineaSp.PrecioUnit);
        Assert.Equal(0.00m, lineaSp.MontoSinIGV);
        Assert.Equal(0.00m, lineaSp.MontoConIGV);
        Assert.Equal(0.00m, lineaSp.TotalConIGV);

        var val = YamahaV3Validator.ValidarLoteServicioTecnico(res.RegistrosValidos);
        Assert.True(val.EsLoteValido, string.Join("; ", val.Hallazgos.Select(h => $"{h.Campo}: {h.Mensaje}")));
        Assert.Equal(0, val.TotalAdvertencias); // Ya no dispara YAM-ST-DESC-003
    }

    // =========================================================================
    // 4. Cantidades cero, precios cero, códigos inválidos y clasificación sin fallback
    // =========================================================================

    [Fact]
    public void Mapping_CantidadCeroYCodigoSkuInvalido_DebenSerNoProyectablesSinLanzarExcepcion()
    {
        var mapper = new YamahaMappingService();
        var ctx = CrearContexto();
        var cliente = new Cliente { NombreCompleto = "Cliente Prueba", TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "45678901" };
        var comp = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "000010", Estado = "Emitido", FechaCreacion = DateTime.UtcNow, Activo = true };
        var venta = new Venta { Estado = EstadoVenta.Confirmada, Activo = true, Cliente = cliente, Comprobante = comp };

        var detCantidadCero = new DetalleVenta
        {
            Producto = new Producto { Codigo = "90793AY42700", Nombre = "Aceite", Tipo = TipoProducto.Repuesto },
            Cantidad = 0,
            PrecioUnitario = 30m,
            Total = 0m,
            Activo = true
        };
        var resCantCero = mapper.MapearDetalleVenta(venta, detCantidadCero, ctx);
        Assert.False(resCantCero.EsValido);
        Assert.Contains(resCantCero.ErroresBloqueantes, e => e.Contains("cantidad", StringComparison.OrdinalIgnoreCase));

        var detSkuInvalido = new DetalleVenta
        {
            Producto = new Producto { Codigo = "YAM90/9021TTJT025", Nombre = "Llanta", Tipo = TipoProducto.Repuesto },
            Cantidad = 1,
            PrecioUnitario = 100m,
            Total = 118m,
            Activo = true
        };
        var resSkuInv = mapper.MapearDetalleVenta(venta, detSkuInvalido, ctx);
        Assert.False(resSkuInv.EsValido);
        Assert.Contains(resSkuInv.ErroresBloqueantes, e => e.Contains("formato Yamaha", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mapping_VentaGratuitaPrecioCero_GeneraCerosConsistentesYPasaValidador()
    {
        var mapper = new YamahaMappingService();
        var ctx = CrearContexto();
        var cliente = new Cliente { NombreCompleto = "Cliente Premio", TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "45678901" };
        var comp = new Comprobante { Tipo = "Boleta", Serie = "B001", Numero = "000011", Estado = "Emitido", FechaCreacion = new DateTime(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc), Activo = true };
        var venta = new Venta { Estado = EstadoVenta.Confirmada, Activo = true, Cliente = cliente, Comprobante = comp };
        var detGratuita = new DetalleVenta
        {
            Producto = new Producto { Codigo = "90793AY42700", Nombre = "Aceite Yamalube", Tipo = TipoProducto.Repuesto },
            Cantidad = 1,
            PrecioUnitario = 49.15m,
            TipoAfectacionIgv = TipoAfectacionIgv.GravadoRetiroPorPremio,
            SubtotalGravado = 0m,
            MontoIgv = 0m,
            Total = 0m,
            Activo = true
        };

        var res = mapper.MapearDetalleVenta(venta, detGratuita, ctx);
        Assert.True(res.EsValido);
        Assert.Equal(0.00m, res.Dto!.MontoSinIGV);
        Assert.Equal(0.00m, res.Dto.MontoConIGV);
        Assert.Equal(0.00m, res.Dto.PrecioFinal);
        Assert.Equal(0.00m, res.Dto.PrecioUnitarioConIGV);

        var val = YamahaV3Validator.ValidarLoteVentas(new[] { res.Dto });
        Assert.True(val.EsLoteValido, string.Join("; ", val.Hallazgos.Select(h => h.Mensaje)));
    }

    [Fact]
    public void ClasificarLineaNegocioVehiculo_NuncaRetornaMV_NiFalsoPositivoOM_NiFallbackSilenciosoAMC()
    {
        // Categoría con subcadena "om" ("Promociones", "Comercial") no debe clasificar como OM ni caer a MC
        var prodComercial = new Producto
        {
            Codigo = "X-001",
            Nombre = "Unidad Especial 2026",
            Tipo = TipoProducto.Vehiculo,
            Categoria = new CategoriaProducto { Nombre = "Comercial" }
        };
        var resComercial = YamahaCatalogMapper.ClasificarLineaNegocioVehiculo(prodComercial, null);
        Assert.False(resComercial.EsValido);
        Assert.Equal(string.Empty, YamahaCatalogMapper.DeterminarLineaNegocioVehiculo(prodComercial, null));

        // Generador explícito jamás debe enviarse como MC
        var vehGenerador = new Vehiculo { TipoUnidad = TipoUnidad.Generador, Marca = "YAMAHA", Modelo = "EF2000iS" };
        var resGen = YamahaCatalogMapper.ClasificarLineaNegocioVehiculo(null, vehGenerador);
        Assert.False(resGen.EsValido);

        // Categoría explícita "OM" sí clasifica como OM
        var prodOm = new Producto
        {
            Codigo = "F40-2026",
            Nombre = "Motor F40",
            Tipo = TipoProducto.Vehiculo,
            Categoria = new CategoriaProducto { Nombre = "OM" }
        };
        var resOm = YamahaCatalogMapper.ClasificarLineaNegocioVehiculo(prodOm, null);
        Assert.True(resOm.EsValido);
        Assert.Equal("OM", resOm.Valor);
    }

    // =========================================================================
    // 5. Respuestas parciales HTTP 200 de Azure Functions y control por líneas/fechas
    // =========================================================================

    [Fact]
    public async Task YamahaApiClient_Http200ConFallidosMayorACero_DebeReportarseComoFalloLogico()
    {
        var handler = new MockHttpHandler();
        handler.Encolar(HttpStatusCode.OK, "{\"total\":10,\"exitosos\":8,\"fallidos\":2,\"status\":\"error\"}");

        using var httpClient = new HttpClient(handler);
        var client = new YamahaApiClient(
            httpClient,
            NullLogger<YamahaApiClient>.Instance,
            Options.Create(new YamahaSettings
            {
                Enabled = true,
                VentasEndpointUrl = "https://yamaha.invalid/api/ReceiveRetailVentas",
                FunctionKey = "fake-test-key"
            }));

        var resp = await client.EnviarVentasAsync(new List<YamahaVentaV3Dto>());

        Assert.Equal(200, resp.CodigoHttp);
        Assert.False(resp.EsExitoso);
        Assert.Contains("fallidos=2", resp.MensajeError);
        Assert.Single(handler.Peticiones); // No reintenta un rechazo lógico 200
    }

    [Fact]
    public async Task VentasExporter_ConLineasHabilitadasYFechaInicioVehiculos_ControlaArranqueDiferenciado()
    {
        using var context = CrearDbContextMemoria();
        var catMc = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Motocicletas", Activo = true };
        var catRep = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Repuestos", Activo = true };
        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "45678901", NombreCompleto = "Ana Perez", Activo = true };
        var prodSp = new Producto { Id = Guid.NewGuid(), CategoriaId = catRep.Id, Codigo = "90793AY42700", Nombre = "Aceite", Tipo = TipoProducto.Repuesto, Activo = true };
        var prodMc = new Producto { Id = Guid.NewGuid(), CategoriaId = catMc.Id, Codigo = "FZ25-2026", Nombre = "Yamaha FZ25", Tipo = TipoProducto.Vehiculo, Activo = true };
        var unidadOct = new UnidadInventario { Id = Guid.NewGuid(), ProductoId = prodMc.Id, NumeroSerieVIN = "ME1RG4287S2011390", NumeroMotor = "G3H7E0171226", Anio = 2026, Color = "Negro", Estado = EstadoUnidadInventario.Vendida, Activo = true };
        var unidadNov = new UnidadInventario { Id = Guid.NewGuid(), ProductoId = prodMc.Id, NumeroSerieVIN = "ME1RG4288S2011205", NumeroMotor = "G3H7E0170725", Anio = 2026, Color = "Azul", Estado = EstadoUnidadInventario.Vendida, Activo = true };

        var fechaOct = new DateTime(2026, 10, 15, 16, 0, 0, DateTimeKind.Utc);
        var fechaNov = new DateTime(2026, 11, 5, 16, 0, 0, DateTimeKind.Utc);

        var ventaSpOct = new Venta
        {
            Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Fecha = fechaOct, Total = 58m, Activo = true,
            Comprobante = new Comprobante { Id = Guid.NewGuid(), Tipo = "Boleta", Serie = "B001", Numero = "000001", Estado = "Emitido", FechaCreacion = fechaOct, Activo = true },
            Detalles = new List<DetalleVenta> { new() { Id = Guid.NewGuid(), ProductoId = prodSp.Id, Cantidad = 1, PrecioUnitario = 49.15m, SubtotalGravado = 49.15m, Total = 58m, Activo = true } }
        };
        var ventaMcOct = new Venta
        {
            Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Fecha = fechaOct, Total = 15000m, Activo = true,
            Comprobante = new Comprobante { Id = Guid.NewGuid(), Tipo = "Boleta", Serie = "B001", Numero = "000002", Estado = "Emitido", FechaCreacion = fechaOct, Activo = true },
            Detalles = new List<DetalleVenta> { new() { Id = Guid.NewGuid(), ProductoId = prodMc.Id, Cantidad = 1, PrecioUnitario = 12711.86m, SubtotalGravado = 12711.86m, Total = 15000m, UnidadesInventarioIds = new() { unidadOct.Id }, Activo = true } }
        };
        var ventaMcNov = new Venta
        {
            Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Fecha = fechaNov, Total = 15000m, Activo = true,
            Comprobante = new Comprobante { Id = Guid.NewGuid(), Tipo = "Boleta", Serie = "B001", Numero = "000003", Estado = "Emitido", FechaCreacion = fechaNov, Activo = true },
            Detalles = new List<DetalleVenta> { new() { Id = Guid.NewGuid(), ProductoId = prodMc.Id, Cantidad = 1, PrecioUnitario = 12711.86m, SubtotalGravado = 12711.86m, Total = 15000m, UnidadesInventarioIds = new() { unidadNov.Id }, Activo = true } }
        };

        context.CategoriasProducto.AddRange(catMc, catRep);
        context.Clientes.Add(cliente);
        context.Productos.AddRange(prodSp, prodMc);
        context.UnidadesInventario.AddRange(unidadOct, unidadNov);
        context.Ventas.AddRange(ventaSpOct, ventaMcOct, ventaMcNov);
        await context.SaveChangesAsync();

        // Histórico de 3 meses para SP, pero vehículos solo desde 01/11/2026
        var exporter = new YamahaVentasExporter(context, new YamahaMappingService(), CrearOpciones(s =>
        {
            s.LineasHabilitadas = new List<string> { "SP", "MC", "ATV" };
            s.FechaInicioVehiculosUtc = new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc);
        }));

        var res = await exporter.ExportarVentasAsync(
            desdeUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            hastaUtc: new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, res.TotalRegistrosValidos); // SP de octubre + MC de noviembre (excluye MC de octubre)
        Assert.Contains(res.RegistrosValidos, r => r.LineaNegocio == "SP" && r.NumeroComprobante == "B001-000001");
        Assert.Contains(res.RegistrosValidos, r => r.LineaNegocio == "MC" && r.NumeroComprobante == "B001-000003");
        Assert.DoesNotContain(res.RegistrosValidos, r => r.NumeroComprobante == "B001-000002");
    }

    [Fact]
    public void FormatearFecha_UtcDespuesDeLas19HorasPeru_NoDebeDesplazarseAlDiaSiguiente()
    {
        // 2026-11-15 21:45 hora Perú (UTC-5) == 2026-11-16 02:45 UTC
        var utcNochePeru = new DateTime(2026, 11, 16, 2, 45, 0, DateTimeKind.Utc);
        Assert.Equal("15/11/2026", YamahaCatalogMapper.FormatearFecha(utcNochePeru));
    }

    [Fact]
    public async Task YamahaApiClient_Agota3ReintentosAnte503_YRetornaFalloSinExponerFunctionKey()
    {
        var handler = new MockHttpHandler();
        handler.Encolar(HttpStatusCode.ServiceUnavailable, "Service unavailable 1");
        handler.Encolar(HttpStatusCode.ServiceUnavailable, "Service unavailable 2");
        handler.Encolar(HttpStatusCode.ServiceUnavailable, "Service unavailable 3");

        using var httpClient = new HttpClient(handler);
        var client = new YamahaApiClient(
            httpClient,
            NullLogger<YamahaApiClient>.Instance,
            Options.Create(new YamahaSettings
            {
                Enabled = true,
                StockEndpointUrl = "https://yamaha.invalid/api/ReceiveRetailStock",
                FunctionKey = "super-secret-key-xyz"
            }));

        var resp = await client.EnviarStockAsync(new List<YamahaStockV3Dto>());

        Assert.False(resp.EsExitoso);
        Assert.Equal(503, resp.CodigoHttp);
        Assert.Equal(3, handler.Peticiones.Count);
        Assert.DoesNotContain("super-secret-key-xyz", resp.MensajeError ?? string.Empty);
        Assert.DoesNotContain("super-secret-key-xyz", resp.CuerpoRespuesta ?? string.Empty);
    }

    [Fact]
    public void ServicioTecnico_OrdenConVehiculoSinPlacaOMYHorasUso_ReportaObservacionYRechazaSinInventarPlaca()
    {
        var mapper = new YamahaMappingService();
        var ctx = CrearContexto();
        var cliente = new Cliente { NombreCompleto = "Pesquera Sur", TipoDocumento = TipoDocumentoCliente.RUC, NumeroDocumento = "20555666777" };
        var vehiculoOm = new Vehiculo
        {
            Marca = "YAMAHA",
            Modelo = "F115BETX",
            TipoUnidad = TipoUnidad.MotorFueraDeBorda,
            TipoMedidor = TipoMedidor.Horas,
            HorasUso = 250m,
            Placa = null, // Sin placa física
            NumeroSerieVIN = "63P-10456780001"
        };
        var comp = new Comprobante { Tipo = "Factura", Serie = "F001", Numero = "000111", Estado = "Emitido", FechaCreacion = new DateTime(2026, 11, 15, 16, 0, 0, DateTimeKind.Utc), Activo = true };
        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000950",
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            Estado = EstadoOrdenServicio.Entregada,
            Cliente = cliente,
            Vehiculo = vehiculoOm,
            Activo = true
        };
        var det = new DetalleServicio
        {
            Id = Guid.NewGuid(),
            TipoItem = TipoItemServicio.ManoDeObra,
            Descripcion = "Servicio 250 horas",
            Cantidad = 1,
            PrecioUnitario = 200m,
            SubtotalGravado = 200m,
            Total = 236m,
            Activo = true
        };

        // Sin inventar una placa ficticia: cumple Sección 6.2 rechazando orden sin placa hasta confirmación de Yamaha
        var res = mapper.MapearDetalleServicio(orden, det, ctx, comp);
        Assert.False(res.EsValido);
        Assert.Contains(res.ErroresBloqueantes, e => e.Contains("Placa", StringComparison.OrdinalIgnoreCase));
    }
}
