using System.Net;
using CRMTeamBenavides.Api.Features.Yamaha;
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
/// Suite de pruebas automatizadas para los 15 escenarios obligatorios de:
/// 1. Selección explícita (opt-in) con ReportarAYamaha (por defecto false).
/// 2. Soporte completo de unidades físicas serializadas para Motos (MC) y Cuatrimotos (ATV).
/// 3. Garantía de no exportación de operaciones no marcadas y comportamiento ante SinCambios.
/// </summary>
public class YamahaV3OptInAndVehiclesTests
{
    private static ApplicationDbContext CrearDbContextMemoria()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);

        context.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa
        {
            RazonSocial = "Team Benavides S.R.L.",
            Ruc = "20601234567",
            NombreEmpresa = "Team Benavides Arequipa",
            MonedaBase = "PEN"
        });

        context.SaveChanges();
        return context;
    }

    private static IOptions<YamahaSettings> CrearOpcionesYamaha()
    {
        return Options.Create(new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            MargenAsentamientoSegundos = 0
        });
    }

    // =========================================================================
    // Escenario 1: Orden facturada pero con vehículo NO Yamaha no se exporta
    // =========================================================================
    [Fact]
    public async Task Escenario01_OrdenFacturada_ConVehiculoNoYamaha_NoSeExporta()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaServicioTecnicoExporter(context, mappingService, CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "12345678", NombreCompleto = "Juan Perez", Activo = true };
        var vehiculo = new Vehiculo { Id = Guid.NewGuid(), Placa = "1234-5H", NumeroSerieVIN = "JH2PC37087M000001", Marca = "Honda", Modelo = "CBR600", Activo = true };
        var producto = new Producto { Id = Guid.NewGuid(), Codigo = "HND-001", Nombre = "Filtro Aire Honda", Activo = true };

        context.Clientes.Add(cliente);
        context.Vehiculos.Add(vehiculo);
        context.Productos.Add(producto);

        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000101",
            ClienteId = cliente.Id,
            Cliente = cliente,
            VehiculoId = vehiculo.Id,
            Vehiculo = vehiculo,
            KilometrajeIngreso = 5000,
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            Estado = EstadoOrdenServicio.Entregada,
            FechaIngreso = DateTime.UtcNow.AddDays(-1),
            Activo = true
        };

        orden.Comprobantes.Add(new Comprobante
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = orden.Id,
            Tipo = "Factura",
            Serie = "F001",
            Numero = "000101",
            Estado = "Emitido",
            SubtotalGravado = 100m,
            MontoIgv = 18m,
            Total = 118m,
            FechaCreacion = DateTime.UtcNow.AddHours(-1)
        });

        orden.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = orden.Id,
            TipoItem = TipoItemServicio.Repuesto,
            ProductoId = producto.Id,
            Producto = producto,
            Cantidad = 1,
            PrecioUnitario = 100m,
            SubtotalGravado = 100m,
            Total = 118m,
            Activo = true
        });

        context.OrdenesServicio.Add(orden);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));

        Assert.Equal(0, res.TotalRegistrosValidos);
        Assert.Empty(res.RegistrosValidos);
    }

    // =========================================================================
    // Escenario 2: Orden facturada con ReportarAYamaha = true se exporta con todos sus detalles permitidos
    // =========================================================================
    [Fact]
    public async Task Escenario02_OrdenFacturada_ConReportarAYamahaTrue_SeExportaConDetallesPermitidos()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaServicioTecnicoExporter(context, mappingService, CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "12345678", NombreCompleto = "Carlos Gómez", Activo = true };
        var vehiculo = new Vehiculo { Id = Guid.NewGuid(), Placa = "2234-5Y", NumeroSerieVIN = "9C6AB0123X0000002", Marca = "Yamaha", Modelo = "Crypton", Activo = true };
        var producto = new Producto { Id = Guid.NewGuid(), Codigo = "4S9-E4451-00", Nombre = "Filtro Crypton", Activo = true };

        context.Clientes.Add(cliente);
        context.Vehiculos.Add(vehiculo);
        context.Productos.Add(producto);

        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000102",
            ClienteId = cliente.Id,
            Cliente = cliente,
            VehiculoId = vehiculo.Id,
            Vehiculo = vehiculo,
            KilometrajeIngreso = 2500,
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            Estado = EstadoOrdenServicio.Entregada,
            ReportarAYamaha = true, // Marcada para reportar
            FechaIngreso = DateTime.UtcNow.AddDays(-1),
            Activo = true
        };

        orden.Comprobantes.Add(new Comprobante
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = orden.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000102",
            Estado = "Emitido",
            SubtotalGravado = 80m,
            MontoIgv = 14.40m,
            Total = 94.40m,
            FechaCreacion = DateTime.UtcNow.AddHours(-1)
        });

        // Detalle Repuesto (SP)
        orden.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = orden.Id,
            TipoItem = TipoItemServicio.Repuesto,
            ProductoId = producto.Id,
            Producto = producto,
            Cantidad = 1,
            PrecioUnitario = 30m,
            SubtotalGravado = 30m,
            Total = 35.40m,
            Activo = true
        });

        // Detalle Mano de Obra (STE)
        orden.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = orden.Id,
            TipoItem = TipoItemServicio.ManoDeObra,
            Descripcion = "Mantenimiento periódico preventivo",
            Cantidad = 1,
            PrecioUnitario = 50m,
            SubtotalGravado = 50m,
            Total = 59m,
            Activo = true
        });

        context.OrdenesServicio.Add(orden);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));

        Assert.Equal(2, res.TotalRegistrosValidos);
        Assert.Contains(res.RegistrosValidos, r => r.SubLineaNegocio == "SP" && r.CodigoYamaha == "4S9-E4451-00");
        Assert.Contains(res.RegistrosValidos, r => r.SubLineaNegocio == "STE" && r.TipoServicio == "MP");
    }

    // =========================================================================
    // Escenario 3: Orden sin comprobante emitido no exporta ni mano de obra ni repuestos
    // =========================================================================
    [Fact]
    public async Task Escenario03_OrdenSinComprobanteEmitido_NoExportaNiManoDeObraNiRepuestos()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaServicioTecnicoExporter(context, mappingService, CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "87654321", NombreCompleto = "Ana Torres", Activo = true };
        var vehiculo = new Vehiculo { Id = Guid.NewGuid(), Placa = "3344-9Z", NumeroSerieVIN = "9C6AB0123X0000003", Marca = "Yamaha", Modelo = "NMAX", Activo = true };
        var producto = new Producto { Id = Guid.NewGuid(), Codigo = "2DP-E4451-00", Nombre = "Filtro NMAX", Activo = true };

        context.Clientes.Add(cliente);
        context.Vehiculos.Add(vehiculo);
        context.Productos.Add(producto);

        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000103",
            ClienteId = cliente.Id,
            Cliente = cliente,
            VehiculoId = vehiculo.Id,
            Vehiculo = vehiculo,
            KilometrajeIngreso = 10000,
            TipoAtencion = TipoAtencion.MantenimientoCorrectivo,
            Estado = EstadoOrdenServicio.Entregada,
            FechaIngreso = DateTime.UtcNow.AddDays(-1),
            Activo = true
        };

        // Sin comprobantes emitidos: orden no facturada
        // Contiene ambos
        orden.Detalles.Add(new DetalleServicio { Id = Guid.NewGuid(), TipoItem = TipoItemServicio.Repuesto, ProductoId = producto.Id, Producto = producto, Cantidad = 2, PrecioUnitario = 50m, SubtotalGravado = 100m, Total = 118m, Activo = true });
        orden.Detalles.Add(new DetalleServicio { Id = Guid.NewGuid(), TipoItem = TipoItemServicio.ManoDeObra, Descripcion = "Reparacion transmision", Cantidad = 1, PrecioUnitario = 100m, SubtotalGravado = 100m, Total = 118m, Activo = true });

        context.OrdenesServicio.Add(orden);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));

        Assert.Equal(0, res.TotalRegistrosValidos);
        Assert.DoesNotContain(res.RegistrosValidos, r => r.SubLineaNegocio == "SP");
        Assert.DoesNotContain(res.RegistrosValidos, r => r.SubLineaNegocio == "STE");
    }

    // =========================================================================
    // Escenario 4: Venta mostrador no confirmada no se exporta
    // =========================================================================
    [Fact]
    public async Task Escenario04_VentaMostrador_NoConfirmada_NoSeExporta()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaVentasExporter(context, mappingService, CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "44556677", NombreCompleto = "Pedro Diaz", Activo = true };
        var prod = new Producto { Id = Guid.NewGuid(), Codigo = "90793-AJ412", Nombre = "Aceite Yamalube", Activo = true };

        context.Clientes.Add(cliente);
        context.Productos.Add(prod);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Estado = EstadoVenta.Cotizacion, // No confirmada (cotización)
            Fecha = DateTime.UtcNow.AddHours(-3),
            SubtotalGravado = 38m,
            MontoIgv = 6.84m,
            Total = 44.84m,
            Activo = true
        };

        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = prod.Id,
            Producto = prod,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 38m,
            SubtotalGravado = 38m,
            Total = 44.84m,
            Activo = true
        });

        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Equal(0, res.TotalRegistrosValidos);
        Assert.Empty(res.RegistrosValidos);
    }

    // =========================================================================
    // Escenario 5: Venta seleccionada de repuestos se exporta como SP
    // =========================================================================
    [Fact]
    public async Task Escenario05_VentaSeleccionadaDeRepuestos_SeExportaComoSP()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaVentasExporter(context, mappingService, CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "44556678", NombreCompleto = "Rosa Flores", Activo = true };
        var prod = new Producto { Id = Guid.NewGuid(), Codigo = "90793-AJ412", Nombre = "Aceite Yamalube 4T", Activo = true };

        context.Clientes.Add(cliente);
        context.Productos.Add(prod);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Estado = EstadoVenta.Confirmada,
            ReportarAYamaha = true, // Seleccionada
            Fecha = DateTime.UtcNow.AddHours(-2),
            SubtotalGravado = 38m,
            MontoIgv = 6.84m,
            Total = 44.84m,
            Activo = true
        };

        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000105",
            Estado = "Emitido",
            SubtotalGravado = 38m,
            MontoIgv = 6.84m,
            Total = 44.84m,
            FechaCreacion = DateTime.UtcNow.AddHours(-2)
        };

        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = prod.Id,
            Producto = prod,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 2,
            PrecioUnitario = 38m,
            SubtotalGravado = 76m,
            Total = 89.68m,
            Activo = true
        });

        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Equal(1, res.TotalRegistrosValidos);
        var reg = res.RegistrosValidos[0];
        Assert.Equal("SP", reg.LineaNegocio);
        Assert.Equal(string.Empty, reg.VIN);
        Assert.Equal("90793-AJ412", reg.CodigoYamaha);
        Assert.Equal(2, reg.CantidadVendida);
    }

    // =========================================================================
    // Escenario 6: Venta de motocicleta se exporta como MC con VIN y datos de la unidad
    // =========================================================================
    [Fact]
    public async Task Escenario06_VentaDeMotocicleta_SeExportaComoMCConVinYDatosDeUnidad()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaVentasExporter(context, mappingService, CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "11223344", NombreCompleto = "Mario Casas", Activo = true };
        var prodMoto = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "MT-03",
            Nombre = "Yamaha MT-03 ABS",
            Tipo = TipoProducto.Vehiculo,
            Activo = true
        };

        var unidadMoto = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            NumeroSerieVIN = "9C6AB0123X0000006",
            NumeroMotor = "G3J6E-012345",
            Anio = 2026,
            Color = "Azul Icon",
            CostoUnitario = 18000m,
            Estado = EstadoUnidadInventario.Vendida,
            Activo = true
        };

        context.Clientes.Add(cliente);
        context.Productos.Add(prodMoto);
        context.UnidadesInventario.Add(unidadMoto);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Estado = EstadoVenta.Confirmada,
            ReportarAYamaha = true,
            Fecha = DateTime.UtcNow.AddHours(-2),
            SubtotalGravado = 20000m,
            MontoIgv = 3600m,
            Total = 23600m,
            Activo = true
        };

        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Factura",
            Serie = "F001",
            Numero = "000106",
            Estado = "Emitido",
            SubtotalGravado = 20000m,
            MontoIgv = 3600m,
            Total = 23600m,
            FechaCreacion = DateTime.UtcNow.AddHours(-2)
        };

        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 20000m,
            SubtotalGravado = 20000m,
            Total = 23600m,
            UnidadesInventarioIds = new List<Guid> { unidadMoto.Id },
            Activo = true
        });

        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Equal(1, res.TotalRegistrosValidos);
        var reg = res.RegistrosValidos[0];
        Assert.Equal("MC", reg.LineaNegocio); // NUNCA 'MV'
        Assert.NotEqual("MV", reg.LineaNegocio);
        Assert.Equal("9C6AB0123X0000006", reg.VIN);
        Assert.Equal("G3J6E-012345", reg.NumeroMotor);
        Assert.Equal(2026, reg.AnioModelo);
        Assert.Equal("Azul Icon", reg.Color);
        Assert.Equal(1, reg.CantidadVendida);
    }

    // =========================================================================
    // Escenario 7: Venta de cuatrimoto se exporta como ATV con VIN y datos de la unidad
    // =========================================================================
    [Fact]
    public async Task Escenario07_VentaDeCuatrimoto_SeExportaComoATVConVinYDatosDeUnidad()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaVentasExporter(context, mappingService, CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.RUC, NumeroDocumento = "20123456789", NombreCompleto = "Agricola del Sur S.A.C.", Activo = true };
        var prodAtv = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "YFM700",
            Nombre = "Cuatrimoto Yamaha Grizzly 700 EPS",
            Tipo = TipoProducto.Vehiculo,
            Activo = true
        };

        var unidadAtv = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodAtv.Id,
            Producto = prodAtv,
            NumeroSerieVIN = "9C6ATV123X0000007",
            NumeroMotor = "3B4-098765",
            Anio = 2026,
            Color = "Verde Camuflado",
            CostoUnitario = 35000m,
            Estado = EstadoUnidadInventario.Vendida,
            Activo = true
        };

        context.Clientes.Add(cliente);
        context.Productos.Add(prodAtv);
        context.UnidadesInventario.Add(unidadAtv);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Estado = EstadoVenta.Confirmada,
            ReportarAYamaha = true,
            Fecha = DateTime.UtcNow.AddHours(-2),
            SubtotalGravado = 40000m,
            MontoIgv = 7200m,
            Total = 47200m,
            Activo = true
        };

        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Factura",
            Serie = "F001",
            Numero = "000107",
            Estado = "Emitido",
            SubtotalGravado = 40000m,
            MontoIgv = 7200m,
            Total = 47200m,
            FechaCreacion = DateTime.UtcNow.AddHours(-2)
        };

        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = prodAtv.Id,
            Producto = prodAtv,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 40000m,
            SubtotalGravado = 40000m,
            Total = 47200m,
            UnidadesInventarioIds = new List<Guid> { unidadAtv.Id },
            Activo = true
        });

        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Equal(1, res.TotalRegistrosValidos);
        var reg = res.RegistrosValidos[0];
        Assert.Equal("ATV", reg.LineaNegocio);
        Assert.Equal("9C6ATV123X0000007", reg.VIN);
        Assert.Equal("3B4-098765", reg.NumeroMotor);
        Assert.Equal(2026, reg.AnioModelo);
        Assert.Equal("Verde Camuflado", reg.Color);
        Assert.Equal(1, reg.CantidadVendida);
    }

    // =========================================================================
    // Escenario 8: Venta de 2 unidades físicas genera 2 registros individuales con cantidad 1
    // =========================================================================
    [Fact]
    public async Task Escenario08_VentaDe2UnidadesFisicas_Genera2RegistrosIndividualesConCantidad1()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaVentasExporter(context, mappingService, CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.RUC, NumeroDocumento = "20999888777", NombreCompleto = "Flotas Benavides S.A.C.", Activo = true };
        var prodMoto = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "YBR-125",
            Nombre = "Yamaha YBR 125Z",
            Tipo = TipoProducto.Vehiculo,
            Activo = true
        };

        var unidad1 = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            NumeroSerieVIN = "9C6YBR123X0000001",
            NumeroMotor = "E3X1-001",
            Anio = 2026,
            Color = "Negro",
            CostoUnitario = 6000m,
            Estado = EstadoUnidadInventario.Vendida,
            Activo = true
        };

        var unidad2 = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            NumeroSerieVIN = "9C6YBR123X0000002",
            NumeroMotor = "E3X1-002",
            Anio = 2026,
            Color = "Rojo",
            CostoUnitario = 6000m,
            Estado = EstadoUnidadInventario.Vendida,
            Activo = true
        };

        context.Clientes.Add(cliente);
        context.Productos.Add(prodMoto);
        context.UnidadesInventario.AddRange(unidad1, unidad2);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Estado = EstadoVenta.Confirmada,
            ReportarAYamaha = true,
            Fecha = DateTime.UtcNow.AddHours(-1),
            SubtotalGravado = 14000m,
            MontoIgv = 2520m,
            Total = 16520m,
            Activo = true
        };

        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Factura",
            Serie = "F001",
            Numero = "000108",
            Estado = "Emitido",
            SubtotalGravado = 14000m,
            MontoIgv = 2520m,
            Total = 16520m,
            FechaCreacion = DateTime.UtcNow.AddHours(-1)
        };

        // Detalle con cantidad 2 y 2 IDs de unidad
        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 2,
            PrecioUnitario = 7000m,
            SubtotalGravado = 14000m,
            Total = 16520m,
            UnidadesInventarioIds = new List<Guid> { unidad1.Id, unidad2.Id },
            Activo = true
        });

        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Equal(2, res.TotalRegistrosValidos);
        Assert.All(res.RegistrosValidos, r =>
        {
            Assert.Equal("MC", r.LineaNegocio);
            Assert.Equal(1, r.CantidadVendida); // CADA UNO CANTIDAD 1
            Assert.Equal(7000m, r.MontoSinIGV);
        });

        Assert.Contains(res.RegistrosValidos, r => r.VIN == "9C6YBR123X0000001" && r.Color == "Negro");
        Assert.Contains(res.RegistrosValidos, r => r.VIN == "9C6YBR123X0000002" && r.Color == "Rojo");
    }

    // =========================================================================
    // Escenario 9: Stock de motos genera registros MC individuales
    // =========================================================================
    [Fact]
    public async Task Escenario09_StockDeMotos_GeneraRegistrosMCIndividuales()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaStockExporter(context, mappingService, CrearOpcionesYamaha());

        var prodMoto = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "R3-2026",
            Nombre = "Yamaha YZF-R3",
            Tipo = TipoProducto.Vehiculo,
            Activo = true
        };

        var unidadMoto = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            NumeroSerieVIN = "9C6R30123X0000009",
            CostoUnitario = 22000m,
            Estado = EstadoUnidadInventario.EnStock,
            Activo = true
        };

        context.Productos.Add(prodMoto);
        context.UnidadesInventario.Add(unidadMoto);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarStockAsync();

        Assert.Equal(1, res.TotalRegistrosValidos);
        var reg = res.RegistrosValidos[0];
        Assert.Equal("MC", reg.LineaNegocio);
        Assert.Equal("9C6R30123X0000009", reg.VIN);
        Assert.Equal(1, reg.StockDisponible);
        Assert.Equal(22000m, reg.CostoVentaSinIGV);
    }

    // =========================================================================
    // Escenario 10: Stock de cuatrimotos genera registros ATV individuales
    // =========================================================================
    [Fact]
    public async Task Escenario10_StockDeCuatrimotos_GeneraRegistrosATVIndividuales()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaStockExporter(context, mappingService, CrearOpcionesYamaha());

        var prodAtv = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "KODIAK-450",
            Nombre = "Cuatrimoto Yamaha Kodiak 450",
            Tipo = TipoProducto.Vehiculo,
            Activo = true
        };

        var unidadAtv = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodAtv.Id,
            Producto = prodAtv,
            NumeroSerieVIN = "9C6KOD123X0000010",
            CostoUnitario = 31000m,
            Estado = EstadoUnidadInventario.EnStock,
            Activo = true
        };

        context.Productos.Add(prodAtv);
        context.UnidadesInventario.Add(unidadAtv);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarStockAsync();

        Assert.Equal(1, res.TotalRegistrosValidos);
        var reg = res.RegistrosValidos[0];
        Assert.Equal("ATV", reg.LineaNegocio);
        Assert.Equal("9C6KOD123X0000010", reg.VIN);
        Assert.Equal(1, reg.StockDisponible);
        Assert.Equal(31000m, reg.CostoVentaSinIGV);
    }

    // =========================================================================
    // Escenario 11: Stock de repuestos continúa generando registros SP
    // =========================================================================
    [Fact]
    public async Task Escenario11_StockDeRepuestos_ContinuaGenerandoRegistrosSP()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaStockExporter(context, mappingService, CrearOpcionesYamaha());

        var repuesto = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "94700-00879",
            Nombre = "Bujia NGK CPR8EA-9",
            Tipo = TipoProducto.Repuesto,
            StockActual = 40,
            Costo = 12.50m,
            PrecioVenta = 20.00m,
            Activo = true
        };

        context.Productos.Add(repuesto);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarStockAsync();

        Assert.Equal(1, res.TotalRegistrosValidos);
        var reg = res.RegistrosValidos[0];
        Assert.Equal("SP", reg.LineaNegocio);
        Assert.Equal(string.Empty, reg.VIN);
        Assert.Equal("94700-00879", reg.CodigoYamaha);
        Assert.Equal(40, reg.StockDisponible);
        Assert.Equal(12.50m, reg.CostoVentaSinIGV);
    }

    // =========================================================================
    // Escenario 12: Unidad vendida/entregada no aparece como disponible en stock
    // =========================================================================
    [Fact]
    public async Task Escenario12_UnidadVendidaOEntregada_NoApareceComoDisponibleEnStock()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var exporter = new YamahaStockExporter(context, mappingService, CrearOpcionesYamaha());

        var prodMoto = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "FZ-S",
            Nombre = "Yamaha FZ-S FI",
            Tipo = TipoProducto.Vehiculo,
            Activo = true
        };

        var unidadVendida = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            NumeroSerieVIN = "9C6FZS123X0000012",
            Estado = EstadoUnidadInventario.Vendida, // VENDIDA: NO DEBE SALIR EN STOCK
            Activo = true
        };

        context.Productos.Add(prodMoto);
        context.UnidadesInventario.Add(unidadVendida);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarStockAsync();

        Assert.Equal(0, res.TotalRegistrosValidos);
        Assert.Empty(res.RegistrosValidos);
    }

    // =========================================================================
    // Escenario 13: Registros históricos migrados quedan con ReportarAYamaha = false
    // =========================================================================
    [Fact]
    public void Escenario13_RegistrosHistoricosMigrados_QuedanConReportarAYamahaFalse()
    {
        var nuevaOrden = new OrdenServicio();
        var nuevaVenta = new Venta();

        // Verificación de valor predeterminado a nivel de clase entidad
        Assert.False(nuevaOrden.ReportarAYamaha);
        Assert.False(nuevaVenta.ReportarAYamaha);

        // Verificación de configuración del modelo EF Core
        using var context = CrearDbContextMemoria();
        var ordenEntity = context.Model.FindEntityType(typeof(OrdenServicio));
        var ventaEntity = context.Model.FindEntityType(typeof(Venta));

        Assert.NotNull(ordenEntity);
        Assert.NotNull(ventaEntity);

        var propOrden = ordenEntity.FindProperty(nameof(OrdenServicio.ReportarAYamaha));
        var propVenta = ventaEntity.FindProperty(nameof(Venta.ReportarAYamaha));

        Assert.NotNull(propOrden);
        Assert.NotNull(propVenta);

        Assert.False(propOrden.IsNullable);
        Assert.False(propVenta.IsNullable);

        Assert.Equal(false, propOrden.GetDefaultValue());
        Assert.Equal(false, propVenta.GetDefaultValue());
    }

    // =========================================================================
    // Escenario 14: Sin operaciones elegibles, el orquestador no realiza llamadas HTTP (SinCambios)
    // =========================================================================
    [Fact]
    public async Task Escenario14_SinOperacionesSeleccionadas_ElOrquestadorNoRealizaLlamadasHTTP_SinCambios()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var settings = new YamahaSettings
        {
            Enabled = true,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            MargenAsentamientoSegundos = 0
        };
        var opciones = Options.Create(settings);

        // Cliente y producto existen
        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "10203040", NombreCompleto = "Test Cliente", Activo = true };
        var producto = new Producto { Id = Guid.NewGuid(), Codigo = "90793-AJ412", Nombre = "Aceite", Activo = true };
        context.Clientes.Add(cliente);
        context.Productos.Add(producto);

        // Venta en borrador sin comprobante: no elegible para exportar a Yamaha
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Estado = EstadoVenta.Cotizacion,
            Fecha = DateTime.UtcNow.AddMinutes(-10),
            SubtotalGravado = 38m,
            MontoIgv = 6.84m,
            Total = 44.84m,
            Activo = true
        };
        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = producto.Id,
            Producto = producto,
            Cantidad = 1,
            PrecioUnitario = 38m,
            SubtotalGravado = 38m,
            Total = 44.84m,
            Activo = true
        });
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var fakeClient = new FakeYamahaApiClient();
        var orquestador = new YamahaSyncOrchestrator(
            context,
            new YamahaVentasExporter(context, mappingService, opciones),
            new YamahaStockExporter(context, mappingService, opciones),
            new YamahaServicioTecnicoExporter(context, mappingService, opciones),
            new YamahaWatermarkService(context),
            fakeClient,
            opciones,
            NullLogger<YamahaSyncOrchestrator>.Instance);

        var resultadoVentas = await orquestador.SincronizarServicioAsync(
            YamahaServicioSincronizacion.Ventas,
            esSimulacion: false,
            modoEnvio: "PruebaSinCambios");

        Assert.Equal(YamahaResultadoSincronizacion.SinCambios, resultadoVentas.Resultado);
        Assert.Equal(0, fakeClient.TotalLlamadasEnvio); // CERO llamadas HTTP

        var resultadoSte = await orquestador.SincronizarServicioAsync(
            YamahaServicioSincronizacion.ServicioTecnico,
            esSimulacion: false,
            modoEnvio: "PruebaSinCambios");

        Assert.Equal(YamahaResultadoSincronizacion.SinCambios, resultadoSte.Resultado);
        Assert.Equal(0, fakeClient.TotalLlamadasEnvio); // CERO llamadas HTTP
    }

    // =========================================================================
    // Escenario 15: Operación no elegible no se filtra indirectamente por otro exportador
    // =========================================================================
    [Fact]
    public async Task Escenario15_OperacionExcluida_NoSeFiltraIndirectamentePorOtroExportador()
    {
        using var context = CrearDbContextMemoria();
        var mappingService = new YamahaMappingService();
        var opciones = CrearOpcionesYamaha();

        var ventasExporter = new YamahaVentasExporter(context, mappingService, opciones);
        var steExporter = new YamahaServicioTecnicoExporter(context, mappingService, opciones);
        var stockExporter = new YamahaStockExporter(context, mappingService, opciones);

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "99887766", NombreCompleto = "Cliente Cruzado", Activo = true };
        var vehiculo = new Vehiculo { Id = Guid.NewGuid(), Placa = "9999-9Z", NumeroSerieVIN = "JH2CRU123X0000015", Marca = "Honda", Modelo = "Cruzada", Activo = true };
        var producto = new Producto { Id = Guid.NewGuid(), Codigo = "CRUZADO-01", Nombre = "Item Cruzado", StockActual = 0, Activo = false };

        context.Clientes.Add(cliente);
        context.Vehiculos.Add(vehiculo);
        context.Productos.Add(producto);

        // 1. Orden de servicio con vehículo NO Yamaha (Honda)
        var ordenExcluida = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-EXCLUIDA",
            ClienteId = cliente.Id,
            Cliente = cliente,
            VehiculoId = vehiculo.Id,
            Vehiculo = vehiculo,
            KilometrajeIngreso = 1000,
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            Estado = EstadoOrdenServicio.Entregada,
            FechaIngreso = DateTime.UtcNow.AddHours(-5),
            Activo = true
        };
        ordenExcluida.Comprobantes.Add(new Comprobante
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = ordenExcluida.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000099",
            Estado = "Emitido",
            SubtotalGravado = 50m,
            MontoIgv = 9m,
            Total = 59m,
            FechaCreacion = DateTime.UtcNow.AddHours(-4)
        });
        ordenExcluida.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = ordenExcluida.Id,
            TipoItem = TipoItemServicio.Repuesto,
            ProductoId = producto.Id,
            Producto = producto,
            Cantidad = 1,
            PrecioUnitario = 50m,
            SubtotalGravado = 50m,
            Total = 59m,
            Activo = true
        });

        // 2. Venta mostrador en borrador (no confirmada)
        var ventaExcluida = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Estado = EstadoVenta.Cotizacion,
            Fecha = DateTime.UtcNow.AddHours(-5),
            SubtotalGravado = 50m,
            MontoIgv = 9m,
            Total = 59m,
            Activo = true
        };
        ventaExcluida.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = ventaExcluida.Id,
            ProductoId = producto.Id,
            Producto = producto,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 50m,
            SubtotalGravado = 50m,
            Total = 59m,
            Activo = true
        });

        context.OrdenesServicio.Add(ordenExcluida);
        context.Ventas.Add(ventaExcluida);
        await context.SaveChangesAsync();

        // Ejecutar los 3 exportadores
        var resVentas = await ventasExporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));
        var resSte = await steExporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));
        var resStock = await stockExporter.ExportarStockAsync();

        // Ninguno debe haber capturado o proyectado la venta ni la orden
        Assert.Empty(resVentas.RegistrosValidos);
        Assert.Empty(resSte.RegistrosValidos);
        Assert.Empty(resStock.RegistrosValidos);
    }
}
