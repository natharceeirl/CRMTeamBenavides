using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha.Exportadores;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Pruebas unitarias para los extractores de PostgreSQL (Stock, Ventas y Servicio Técnico).
/// Verifica consultas EF Core AsNoTracking, filtros de estado, rangos temporales y aislamiento de decisiones D-02 y D-07.
/// </summary>
public class YamahaExportersTests
{
    private static ApplicationDbContext CrearDbContextMemoria()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);

        // Seed básico de configuración de empresa
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

    [Fact]
    public async Task StockExporter_DebeExtraerProductosActivosYAislarFiltroMarcaYamaha()
    {
        using var context = CrearDbContextMemoria();
        var catId = Guid.NewGuid();

        context.Productos.AddRange(
            new Producto
            {
                Id = Guid.NewGuid(),
                CategoriaId = catId,
                Codigo = "2DP-E4451-00",
                Nombre = "Filtro de Aire NMAX",
                Marca = "YAMAHA",
                StockActual = 15,
                Costo = 45.00m,
                PrecioVenta = 65.00m,
                Activo = true
            },
            new Producto
            {
                Id = Guid.NewGuid(),
                CategoriaId = catId,
                Codigo = "GEN-001",
                Nombre = "Aceite Multimarca 20W50",
                Marca = "Generico",
                StockActual = 8,
                Costo = 20.00m,
                PrecioVenta = 30.00m,
                Activo = true
            },
            new Producto
            {
                Id = Guid.NewGuid(),
                CategoriaId = catId,
                Codigo = "INACT-999",
                Nombre = "Repuesto Descontinuado",
                Marca = "YAMAHA",
                StockActual = 0,
                Activo = false // Inactivo
            }
        );
        await context.SaveChangesAsync();

        var mappingService = new YamahaMappingService();
        var settings = Options.Create(new YamahaSettings
        {
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S"
        });

        var exporter = new YamahaStockExporter(context, mappingService, settings);

        // 1. Exportar todo (soloMarcaYamaha = false)
        var resTodos = await exporter.ExportarStockAsync(soloMarcaYamaha: false);
        Assert.Equal(2, resTodos.TotalRegistrosValidos);
        Assert.Equal(2, resTodos.RegistrosValidos.Count);
        Assert.Contains(resTodos.RegistrosValidos, r => r.CodigoYamaha == "2DP-E4451-00");
        Assert.Contains(resTodos.RegistrosValidos, r => r.CodigoYamaha == "GEN-001");
        Assert.DoesNotContain(resTodos.RegistrosValidos, r => r.CodigoYamaha == "INACT-999");

        // 2. Exportar aislando marca YAMAHA (soloMarcaYamaha = true)
        var resYamaha = await exporter.ExportarStockAsync(soloMarcaYamaha: true);
        Assert.Single(resYamaha.RegistrosValidos);
        Assert.Equal("2DP-E4451-00", resYamaha.RegistrosValidos[0].CodigoYamaha);
        Assert.Equal(15, resYamaha.RegistrosValidos[0].StockDisponible);
        Assert.Equal(45.00m, resYamaha.RegistrosValidos[0].CostoVentaSinIGV);
        Assert.Equal(53.10m, resYamaha.RegistrosValidos[0].CostoVentaConIGV); // 45 * 1.18
    }

    [Fact]
    public async Task VentasExporter_DebeAislarVentasMostradorFrenteATallerYFiltrarPorRangoTemporal()
    {
        using var context = CrearDbContextMemoria();
        var clienteId = Guid.NewGuid();
        var prodId = Guid.NewGuid();

        var cliente = new Cliente
        {
            Id = clienteId,
            NombreCompleto = "Carlos Benavides",
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "45678901",
            Telefono = "954123456",
            Direccion = "Av. Ejército 123",
            Activo = true
        };
        context.Clientes.Add(cliente);

        var producto = new Producto
        {
            Id = prodId,
            CategoriaId = Guid.NewGuid(),
            Codigo = "90793-AJ412",
            Nombre = "Aceite Yamalube 4T 10W-40",
            PrecioVenta = 38.00m,
            Costo = 25.00m,
            StockActual = 50,
            Activo = true
        };
        context.Productos.Add(producto);

        var ventaMostrador = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            OrdenServicioId = null, // MOSTRADOR
            Estado = EstadoVenta.Confirmada,
            Fecha = DateTime.UtcNow.AddDays(-2),
            SubtotalGravado = 32.20m,
            MontoIgv = 5.80m,
            Total = 38.00m,
            Activo = true
        };
        ventaMostrador.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = ventaMostrador.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000101",
            Estado = "Emitido",
            SubtotalGravado = 32.20m,
            MontoIgv = 5.80m,
            Total = 38.00m,
            FechaCreacion = DateTime.UtcNow.AddDays(-2)
        };
        ventaMostrador.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = ventaMostrador.Id,
            ProductoId = prodId,
            Producto = producto,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 32.20m,
            SubtotalGravado = 32.20m,
            MontoIgv = 5.80m,
            Total = 38.00m,
            Activo = true
        });

        var ventaTaller = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            OrdenServicioId = Guid.NewGuid(), // TALLER
            Estado = EstadoVenta.Confirmada,
            Fecha = DateTime.UtcNow.AddDays(-1),
            SubtotalGravado = 32.20m,
            MontoIgv = 5.80m,
            Total = 38.00m,
            Activo = true
        };
        ventaTaller.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = ventaTaller.Id,
            Tipo = "Factura",
            Serie = "F001",
            Numero = "000202",
            Estado = "Emitido",
            SubtotalGravado = 32.20m,
            MontoIgv = 5.80m,
            Total = 38.00m,
            FechaCreacion = DateTime.UtcNow.AddDays(-1)
        };
        ventaTaller.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = ventaTaller.Id,
            ProductoId = prodId,
            Producto = producto,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 32.20m,
            SubtotalGravado = 32.20m,
            MontoIgv = 5.80m,
            Total = 38.00m,
            Activo = true
        });

        context.Ventas.AddRange(ventaMostrador, ventaTaller);
        await context.SaveChangesAsync();

        var mappingService = new YamahaMappingService();
        var settings = Options.Create(new YamahaSettings
        {
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S"
        });

        var exporter = new YamahaVentasExporter(context, mappingService, settings);

        // 1. Aislar mostrador (soloVentasMostrador = true, aislando D-02)
        var resMostrador = await exporter.ExportarVentasAsync(
            desdeUtc: DateTime.UtcNow.AddDays(-5),
            soloVentasMostrador: true);

        Assert.Single(resMostrador.RegistrosValidos);
        Assert.Equal("B001-000101", resMostrador.RegistrosValidos[0].NumeroComprobante);
        Assert.Equal("MOSTRADOR", resMostrador.RegistrosValidos[0].Canal);

        // 2. Incluir ambas
        var resAmbas = await exporter.ExportarVentasAsync(
            desdeUtc: DateTime.UtcNow.AddDays(-5),
            soloVentasMostrador: false);

        Assert.Equal(2, resAmbas.RegistrosValidos.Count);
        Assert.Contains(resAmbas.RegistrosValidos, v => v.Canal == "MOSTRADOR");
        Assert.Contains(resAmbas.RegistrosValidos, v => v.Canal == "TALLER");

        // 3. Filtrar fuera de rango
        var resVacio = await exporter.ExportarVentasAsync(
            desdeUtc: DateTime.UtcNow.AddHours(-1),
            soloVentasMostrador: false);

        Assert.Empty(resVacio.RegistrosValidos);
    }

    [Fact]
    public async Task VentasExporter_ElBordeDesdeEsExclusivoYElBordeHastaInclusivo()
    {
        using var context = CrearDbContextMemoria();
        var instante = new DateTime(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc);

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            NombreCompleto = "Carlos Benavides",
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "45678901",
            Telefono = "954123456",
            Direccion = "Av. Ejército 123",
            Activo = true
        };
        var producto = new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = Guid.NewGuid(),
            Codigo = "90793-AJ412",
            Nombre = "Aceite Yamalube 4T 10W-40",
            PrecioVenta = 38.00m,
            Costo = 25.00m,
            StockActual = 50,
            Activo = true
        };
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Estado = EstadoVenta.Confirmada,
            Fecha = instante,
            SubtotalGravado = 32.20m,
            MontoIgv = 5.80m,
            Total = 38.00m,
            Activo = true
        };
        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000303",
            Estado = "Emitido",
            SubtotalGravado = 32.20m,
            MontoIgv = 5.80m,
            Total = 38.00m,
            FechaCreacion = instante
        };
        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = producto.Id,
            Producto = producto,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 32.20m,
            SubtotalGravado = 32.20m,
            MontoIgv = 5.80m,
            Total = 38.00m,
            Activo = true
        });

        context.Clientes.Add(cliente);
        context.Productos.Add(producto);
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var exporter = new YamahaVentasExporter(
            context,
            new YamahaMappingService(),
            Options.Create(new YamahaSettings { CodigoPuntoVenta = "TB-AQP-01", TipoPuntoVenta = "3S" }));

        // Lote anterior: terminó exactamente en el instante de la venta (hasta inclusivo) → la incluye
        var loteAnterior = await exporter.ExportarVentasAsync(desdeUtc: instante.AddHours(-4), hastaUtc: instante);
        Assert.Single(loteAnterior.RegistrosValidos);

        // Lote siguiente: parte de ese mismo instante (desde exclusivo) → no la repite
        var loteSiguiente = await exporter.ExportarVentasAsync(desdeUtc: instante, hastaUtc: instante.AddHours(4));
        Assert.Empty(loteSiguiente.RegistrosValidos);
    }

    [Fact]
    public async Task ServicioTecnicoExporter_DebeExportarOrdenesFacturadasMapeandoSpYSte()
    {
        using var context = CrearDbContextMemoria();
        var clienteId = Guid.NewGuid();
        var vehiculoId = Guid.NewGuid();

        var cliente = new Cliente
        {
            Id = clienteId,
            NombreCompleto = "Juan Pérez",
            TipoDocumento = TipoDocumentoCliente.DNI,
            NumeroDocumento = "12345678",
            Telefono = "987654321",
            Email = "juan.perez@example.com",
            Direccion = "Calle Mercaderes 101",
            Activo = true
        };
        context.Clientes.Add(cliente);

        var vehiculo = new Vehiculo
        {
            Id = vehiculoId,
            ClienteId = clienteId,
            Placa = "1234-5X",
            NumeroSerieVIN = "9C6KE0920N0123456",
            NumeroMotor = "E3R2E-001234",
            Marca = "YAMAHA",
            Modelo = "FZ-S FI",
            Color = "Azul",
            Anio = 2024,
            Kilometraje = 4500,
            Activo = true
        };
        context.Vehiculos.Add(vehiculo);

        var categoria = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Repuestos" };
        context.CategoriasProducto.Add(categoria);

        var producto = new Producto
        {
            Id = Guid.NewGuid(),
            CategoriaId = categoria.Id,
            Categoria = categoria,
            Codigo = "2DP-E4451-00",
            Nombre = "Filtro de aire",
            Marca = "YAMAHA",
            PrecioVenta = 47.20m,
            Costo = 25m,
            StockActual = 10,
            Activo = true
        };
        context.Productos.Add(producto);

        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000555",
            ClienteId = clienteId,
            Cliente = cliente,
            VehiculoId = vehiculoId,
            Vehiculo = vehiculo,
            KilometrajeIngreso = 4500,
            TipoAtencion = TipoAtencion.MantenimientoPreventivo,
            Estado = EstadoOrdenServicio.Entregada,
            FechaIngreso = DateTime.UtcNow.AddDays(-1),
            Activo = true
        };

        var comp = new Comprobante
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = orden.Id,
            Tipo = "Factura",
            Serie = "F001",
            Numero = "000999",
            Estado = "Emitido",
            SubtotalGravado = 100m,
            MontoIgv = 18m,
            Total = 118m,
            FechaCreacion = DateTime.UtcNow.AddHours(-12)
        };
        orden.Comprobantes.Add(comp);

        // Línea 1: Repuesto (SP)
        orden.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = orden.Id,
            TipoItem = TipoItemServicio.Repuesto,
            ProductoId = producto.Id,
            Producto = producto,
            Descripcion = "Filtro de aire",
            Cantidad = 1,
            PrecioUnitario = 40m,
            SubtotalGravado = 40m,
            Total = 47.20m,
            Activo = true
        });

        // Línea 2: Mano de Obra (STE)
        orden.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = orden.Id,
            TipoItem = TipoItemServicio.ManoDeObra,
            Descripcion = "Mantenimiento preventivo 5000 km",
            Cantidad = 1,
            PrecioUnitario = 60m,
            SubtotalGravado = 60m,
            Total = 70.80m,
            Activo = true
        });

        context.OrdenesServicio.Add(orden);
        await context.SaveChangesAsync();

        var mappingService = new YamahaMappingService();
        var settings = Options.Create(new YamahaSettings
        {
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S"
        });

        var exporter = new YamahaServicioTecnicoExporter(context, mappingService, settings);
        var res = await exporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));

        Assert.Equal(2, res.TotalRegistrosValidos);
        Assert.Equal(2, res.RegistrosValidos.Count);

        // Ambas líneas comparten la cabecera y el correlativo OT-000555
        Assert.All(res.RegistrosValidos, r => Assert.Equal("OT-000555", r.NumeroOrdenTrabajo));
        Assert.All(res.RegistrosValidos, r => Assert.Equal("FACTURADO", r.Estado));
        Assert.All(res.RegistrosValidos, r => Assert.Equal("1234-5X", r.Placa));

        // Sublíneas
        Assert.Contains(res.RegistrosValidos, r => r.SubLineaNegocio == "SP" && r.CodigoYamaha == "2DP-E4451-00");
        Assert.Contains(res.RegistrosValidos, r => r.SubLineaNegocio == "STE" && r.TipoServicio == "MP");
    }

    [Fact]
    public async Task ExportarStockAsync_ConPaginacionDeterminista_DebeRecuperarTodosLosRegistrosSinOmisiones()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);

        var categoria = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Repuestos" };
        context.CategoriasProducto.Add(categoria);

        // Se insertan 7 productos con codigos desordenados
        var codigos = new[] { "Y-007", "Y-002", "Y-005", "Y-001", "Y-004", "Y-003", "Y-006" };
        foreach (var cod in codigos)
        {
            context.Productos.Add(new Producto
            {
                Id = Guid.NewGuid(),
                CategoriaId = categoria.Id,
                Categoria = categoria,
                Codigo = cod,
                Nombre = $"Producto {cod}",
                Marca = "YAMAHA",
                PrecioVenta = 50m,
                Costo = 30m,
                StockActual = 5,
                Activo = true
            });
        }
        await context.SaveChangesAsync();

        var mappingService = new YamahaMappingService();
        // Configuramos un lote pequeño de 3 para forzar multiples páginas (3, 3, 1)
        var settings = Options.Create(new YamahaSettings
        {
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            TamanioLoteExportacion = 3
        });

        var exporter = new YamahaStockExporter(context, mappingService, settings);
        var res = await exporter.ExportarStockAsync();

        Assert.Equal(7, res.TotalEntidadesLeidas);
        Assert.Equal(7, res.TotalRegistrosValidos);
        Assert.Equal(7, res.RegistrosValidos.Count);

        // Verifica que la paginación determinista respeta el orden alfabético de SKU sin perder ninguno
        var codigosOrdenados = res.RegistrosValidos.Select(r => r.CodigoYamaha).ToList();
        var codigosEsperados = codigos.OrderBy(c => c).ToList();
        Assert.Equal(codigosEsperados, codigosOrdenados);
    }
}
