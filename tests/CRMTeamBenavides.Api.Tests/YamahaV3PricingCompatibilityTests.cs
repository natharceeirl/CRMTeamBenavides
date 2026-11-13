using System.Net;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Aprobaciones;
using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Api.Features.Servicios;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha.Exportadores;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Pruebas exhaustivas de compatibilidad entre el commit e3851af (precios con/sin IGV en mano de obra,
/// servicios y terceros) y la integración Yamaha v3 (exportación de SP, STE, MC, ATV y selección explícita).
/// Valida que no existan duplicaciones de IGV, discrepancias con comprobantes ni alteraciones de históricos.
/// </summary>
public class YamahaV3PricingCompatibilityTests : IDisposable
{
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

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

    private async Task<(Guid OrdenId, Guid ClienteId, Guid VehiculoId, Guid GerenciaId)> CrearOrdenBaseAsync(bool reportarAYamaha = true)
    {
        return await _identity.ConAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();

            if (!await db.ConfiguracionesEmpresa.AnyAsync())
            {
                db.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa
                {
                    RazonSocial = "Team Benavides S.R.L.",
                    Ruc = "20601234567",
                    NombreEmpresa = "Team Benavides Arequipa",
                    MonedaBase = "PEN",
                    PorcentajeIgv = 18m
                });
            }

            var cliente = new Cliente
            {
                TipoDocumento = TipoDocumentoCliente.DNI,
                NumeroDocumento = "40123456",
                NombreCompleto = "Juan Pérez",
                Telefono = "959123456",
                Email = "juan@example.com",
                Activo = true
            };
            db.Clientes.Add(cliente);

            var vehiculo = new Vehiculo
            {
                Cliente = cliente,
                Marca = "Yamaha", // Debe ser Yamaha para STE
                Modelo = "FZ-25",
                Placa = "1234-5X",
                NumeroSerieVIN = "9C6AB0123X0000001",
                Anio = 2026,
                Kilometraje = 5000,
                Activo = true
            };
            db.Vehiculos.Add(vehiculo);

            var gerencia = new Usuario
            {
                NombreCompleto = "Gerente de Taller",
                Email = "gerencia@teambenavides.pe",
                Activo = true
            };
            db.Usuarios.Add(gerencia);

            var orden = new OrdenServicio
            {
                NumeroOrden = "OS-001001",
                Cliente = cliente,
                Vehiculo = vehiculo,
                TipoAtencion = TipoAtencion.MantenimientoPreventivo,
                Estado = EstadoOrdenServicio.Diagnostico,
                ReportarAYamaha = reportarAYamaha,
                FechaIngreso = DateTime.UtcNow.AddDays(-1),
                KilometrajeIngreso = 5000,
                Activo = true
            };
            db.OrdenesServicio.Add(orden);

            await db.SaveChangesAsync();
            return (orden.Id, cliente.Id, vehiculo.Id, gerencia.Id);
        });
    }

    private async Task EmitirComprobanteAsync(Guid ordenId, decimal subtotalGravado, decimal montoIgv, decimal total)
    {
        await _identity.ConAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var orden = await db.OrdenesServicio.Include(o => o.Comprobantes).FirstAsync(o => o.Id == ordenId);
            orden.Estado = EstadoOrdenServicio.Entregada; // Completada/Entregada para facturación

            var venta = new Venta
            {
                ClienteId = orden.ClienteId,
                OrdenServicioId = orden.Id,
                Estado = EstadoVenta.Confirmada,
                ReportarAYamaha = orden.ReportarAYamaha,
                SubtotalGravado = subtotalGravado,
                MontoIgv = montoIgv,
                Total = total,
                Activo = true
            };
            db.Ventas.Add(venta);

            var comp = new Comprobante
            {
                Id = Guid.NewGuid(),
                VentaId = venta.Id,
                Venta = venta,
                OrdenServicioId = orden.Id,
                Tipo = "Boleta",
                Serie = "B001",
                Numero = "000123",
                Estado = "Emitido",
                SubtotalGravado = subtotalGravado,
                MontoIgv = montoIgv,
                Total = total,
                FechaCreacion = DateTime.UtcNow.AddHours(-1),
                Activo = true
            };
            db.Comprobantes.Add(comp);
            orden.Comprobantes.Add(comp);
            await db.SaveChangesAsync();
            return true;
        });
    }

    // =========================================================================
    // Prueba 1: Mano de obra con PrecioIncluyeIgv = true exportada a STE
    // =========================================================================
    [Fact]
    public async Task Prueba01_ManoDeObra_ConPrecioIncluyeIgv_ExportadaASTE_MapeaCorrectamenteSinDuplicarIgv()
    {
        var (ordenId, _, _, gerenciaId) = await CrearOrdenBaseAsync(reportarAYamaha: true);

        // Se escribe S/ 80.00 con IGV incluido (base 67.80, IGV 12.20, Total 80.00)
        var resAdd = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            ordenId,
            new AgregarDetalleServicioRequest(
                Descripcion: "Ajuste de cadena y calibración",
                PrecioUnitario: 80.00m,
                TipoItem: TipoItemServicio.ManoDeObra,
                TipoAfectacionIgv: TipoAfectacionIgv.Gravado,
                PrecioIncluyeIgv: true),
            true, null, gerenciaId));

        Assert.True(resAdd.IsSuccess, resAdd.Error);
        Assert.Equal(80.00m, resAdd.Data!.PrecioUnitarioConIgv);
        Assert.Equal(67.80m, resAdd.Data.PrecioUnitario);
        Assert.Equal(67.80m, resAdd.Data.SubtotalGravado);
        Assert.Equal(12.20m, resAdd.Data.MontoIgv);
        Assert.Equal(80.00m, resAdd.Data.Total);

        await EmitirComprobanteAsync(ordenId, 67.80m, 12.20m, 80.00m);

        // Exportar a Yamaha Servicio 3 (Servicio Técnico)
        var exportacion = await _identity.ConAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var exporter = new YamahaServicioTecnicoExporter(db, new YamahaMappingService(), CrearOpcionesYamaha());
            return await exporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));
        });

        Assert.Single(exportacion.RegistrosValidos);
        var ste = exportacion.RegistrosValidos[0];

        Assert.Equal("STE", ste.LineaNegocio);
        Assert.Equal("STE", ste.SubLineaNegocio);
        Assert.Equal(67.80m, ste.MontoSinIGV);
        Assert.Equal(80.00m, ste.MontoConIGV);
        Assert.Equal(80.00m, ste.TotalConIGV);
        Assert.Null(ste.PrecioUnit); // En STE la especificación v3 no proyecta PrecioUnit
        Assert.Null(ste.Cantidad);   // En STE la especificación v3 no proyecta Cantidad
        Assert.Equal(0.00m, ste.Descuento);
        Assert.Equal("MP", ste.TipoServicio);
    }

    // =========================================================================
    // Prueba 2: Mano de obra histórica o sin IGV exportada a STE mantiene importes
    // =========================================================================
    [Fact]
    public async Task Prueba02_ManoDeObra_SinPrecioIncluyeIgv_ExportadaASTE_MantieneComportamientoHistorico()
    {
        var (ordenId, _, _, gerenciaId) = await CrearOrdenBaseAsync(reportarAYamaha: true);

        // Se escribe S/ 67.80 sin IGV (como se hacía antes) -> se le suma el 18% = Total 80.00
        var resAdd = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            ordenId,
            new AgregarDetalleServicioRequest(
                Descripcion: "Mantenimiento 5000km",
                PrecioUnitario: 67.80m,
                TipoItem: TipoItemServicio.ManoDeObra,
                TipoAfectacionIgv: TipoAfectacionIgv.Gravado,
                PrecioIncluyeIgv: false),
            true, null, gerenciaId));

        Assert.True(resAdd.IsSuccess, resAdd.Error);
        Assert.Null(resAdd.Data!.PrecioUnitarioConIgv); // Comportamiento histórico: nulo
        Assert.Equal(67.80m, resAdd.Data.PrecioUnitario);
        Assert.Equal(67.80m, resAdd.Data.SubtotalGravado);
        Assert.Equal(12.20m, resAdd.Data.MontoIgv);
        Assert.Equal(80.00m, resAdd.Data.Total);

        await EmitirComprobanteAsync(ordenId, 67.80m, 12.20m, 80.00m);

        var exportacion = await _identity.ConAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var exporter = new YamahaServicioTecnicoExporter(db, new YamahaMappingService(), CrearOpcionesYamaha());
            return await exporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));
        });

        Assert.Single(exportacion.RegistrosValidos);
        var ste = exportacion.RegistrosValidos[0];

        Assert.Equal(67.80m, ste.MontoSinIGV);
        Assert.Equal(80.00m, ste.MontoConIGV);
        Assert.Equal(80.00m, ste.TotalConIGV);
    }

    // =========================================================================
    // Prueba 3: Servicio de catálogo con PrecioIncluyeIgv = true exportado a STE
    // =========================================================================
    [Fact]
    public async Task Prueba03_ServicioCatalogo_ConPrecioIncluyeIgv_ExportadaASTE_MapeaCorrectamente()
    {
        var (ordenId, _, _, gerenciaId) = await CrearOrdenBaseAsync(reportarAYamaha: true);

        // Crear servicio en catálogo con PrecioIncluyeIgv = true (S/ 180.00 con IGV -> base 152.54)
        var servicioId = await _identity.ConAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var serv = new Servicio
            {
                Nombre = "Mantenimiento General FZ",
                PrecioSugerido = 180.00m,
                PrecioIncluyeIgv = true,
                TipoAfectacionIgv = TipoAfectacionIgv.Gravado,
                Activo = true
            };
            db.Servicios.Add(serv);
            await db.SaveChangesAsync();
            return serv.Id;
        });

        var resAdd = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            ordenId,
            new AgregarDetalleServicioRequest(
                ServicioId: servicioId,
                TipoItem: TipoItemServicio.Servicio,
                PrecioUnitario: null), // Toma precio de lista
            true, null, gerenciaId));

        Assert.True(resAdd.IsSuccess, resAdd.Error);
        Assert.Equal(180.00m, resAdd.Data!.PrecioUnitarioConIgv);
        Assert.Equal(152.54m, resAdd.Data.PrecioUnitario);
        Assert.Equal(152.54m, resAdd.Data.SubtotalGravado);
        Assert.Equal(27.46m, resAdd.Data.MontoIgv);
        Assert.Equal(180.00m, resAdd.Data.Total);

        await EmitirComprobanteAsync(ordenId, 152.54m, 27.46m, 180.00m);

        var exportacion = await _identity.ConAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var exporter = new YamahaServicioTecnicoExporter(db, new YamahaMappingService(), CrearOpcionesYamaha());
            return await exporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));
        });

        Assert.Single(exportacion.RegistrosValidos);
        var ste = exportacion.RegistrosValidos[0];

        Assert.Equal("STE", ste.SubLineaNegocio);
        Assert.Equal(152.54m, ste.MontoSinIGV);
        Assert.Equal(180.00m, ste.MontoConIGV);
        Assert.Equal(180.00m, ste.TotalConIGV);
    }

    // =========================================================================
    // Prueba 4: Terceros con PrecioIncluyeIgv = true y exonerado exportados a STE
    // =========================================================================
    [Fact]
    public async Task Prueba04_Terceros_GravadoYExonerado_ExportadosASTE_CalculosCorrectos()
    {
        var (ordenId, _, _, gerenciaId) = await CrearOrdenBaseAsync(reportarAYamaha: true);

        // 1. Tercero gravado con IGV: S/ 118.00 (base 100.00, IGV 18.00)
        var res1 = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            ordenId,
            new AgregarDetalleServicioRequest(
                Descripcion: "Rectificación de cilindro",
                PrecioUnitario: 118.00m,
                TipoItem: TipoItemServicio.Terceros,
                TipoAfectacionIgv: TipoAfectacionIgv.Gravado,
                PrecioIncluyeIgv: true),
            true, null, gerenciaId));
        Assert.True(res1.IsSuccess);

        // 2. Tercero exonerado: S/ 50.00 (sin IGV)
        var res2 = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            ordenId,
            new AgregarDetalleServicioRequest(
                Descripcion: "Peritaje técnico externo",
                PrecioUnitario: 50.00m,
                TipoItem: TipoItemServicio.Terceros,
                TipoAfectacionIgv: TipoAfectacionIgv.Exonerado,
                PrecioIncluyeIgv: false),
            true, null, gerenciaId));
        Assert.True(res2.IsSuccess);

        await EmitirComprobanteAsync(ordenId, 100.00m, 18.00m, 168.00m);

        var exportacion = await _identity.ConAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var exporter = new YamahaServicioTecnicoExporter(db, new YamahaMappingService(), CrearOpcionesYamaha());
            return await exporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));
        });

        Assert.Equal(2, exportacion.TotalRegistrosValidos);

        var regGravado = exportacion.RegistrosValidos.First(r => r.Actividad.Contains("Rectificación"));
        Assert.Equal(100.00m, regGravado.MontoSinIGV);
        Assert.Equal(118.00m, regGravado.MontoConIGV);
        Assert.Equal(118.00m, regGravado.TotalConIGV);

        var regExonerado = exportacion.RegistrosValidos.First(r => r.Actividad.Contains("Peritaje"));
        Assert.Equal(50.00m, regExonerado.MontoSinIGV);
        Assert.Equal(50.00m, regExonerado.MontoConIGV);
        Assert.Equal(50.00m, regExonerado.TotalConIGV);
    }

    // =========================================================================
    // Prueba 5: Edición de orden preserva los precios efectivamente cobrados
    // =========================================================================
    [Fact]
    public async Task Prueba05_EdicionDeOrden_PreservaPreciosEfectivamenteCobrados_YCoincideConYamaha()
    {
        var (ordenId, _, _, gerenciaId) = await CrearOrdenBaseAsync(reportarAYamaha: true);

        // Crear con S/ 80.00 con IGV
        var resAdd = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            ordenId,
            new AgregarDetalleServicioRequest(
                Descripcion: "Mano de obra diagnóstico",
                PrecioUnitario: 80.00m,
                TipoItem: TipoItemServicio.ManoDeObra,
                TipoAfectacionIgv: TipoAfectacionIgv.Gravado,
                PrecioIncluyeIgv: true),
            true, null, gerenciaId));
        Assert.True(resAdd.IsSuccess);

        // Editar a S/ 100.00 con IGV
        var resEdit = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().ActualizarDetalleAsync(
            ordenId,
            resAdd.Data!.Id,
            new ActualizarDetalleServicioRequest(PrecioUnitario: 100.00m, Cantidad: 1),
            true, null, gerenciaId));

        Assert.True(resEdit.IsSuccess);
        Assert.Equal(100.00m, resEdit.Data!.PrecioUnitarioConIgv);
        Assert.Equal(84.75m, resEdit.Data.PrecioUnitario);
        Assert.Equal(84.75m, resEdit.Data.SubtotalGravado);
        Assert.Equal(15.25m, resEdit.Data.MontoIgv);
        Assert.Equal(100.00m, resEdit.Data.Total);

        await EmitirComprobanteAsync(ordenId, 84.75m, 15.25m, 100.00m);

        var exportacion = await _identity.ConAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var exporter = new YamahaServicioTecnicoExporter(db, new YamahaMappingService(), CrearOpcionesYamaha());
            return await exporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));
        });

        Assert.Single(exportacion.RegistrosValidos);
        var ste = exportacion.RegistrosValidos[0];
        Assert.Equal(84.75m, ste.MontoSinIGV);
        Assert.Equal(100.00m, ste.MontoConIGV);
        Assert.Equal(100.00m, ste.TotalConIGV);
    }

    // =========================================================================
    // Prueba 6: Operación sin comprobante emitido se excluye de Yamaha
    // =========================================================================
    [Fact]
    public async Task Prueba06_OperacionConPrecioIncluyeIgv_SinComprobanteEmitido_SeExcluyeTotalmente()
    {
        var (ordenId, _, _, gerenciaId) = await CrearOrdenBaseAsync(reportarAYamaha: false);

        var resAdd = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>().AgregarDetalleAsync(
            ordenId,
            new AgregarDetalleServicioRequest(
                Descripcion: "Mano de obra privada",
                PrecioUnitario: 100.00m,
                TipoItem: TipoItemServicio.ManoDeObra,
                TipoAfectacionIgv: TipoAfectacionIgv.Gravado,
                PrecioIncluyeIgv: true),
            true, null, gerenciaId));
        Assert.True(resAdd.IsSuccess);

        // Sin comprobante emitido, la orden no es reportable para Yamaha
        var exportacion = await _identity.ConAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var exporter = new YamahaServicioTecnicoExporter(db, new YamahaMappingService(), CrearOpcionesYamaha());
            return await exporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));
        });

        Assert.Equal(0, exportacion.TotalRegistrosValidos);
        Assert.Empty(exportacion.RegistrosValidos);
    }

    // =========================================================================
    // Prueba 7: Compatibilidad de las dos migraciones en el modelo de base de datos
    // =========================================================================
    [Fact]
    public void Prueba07_CompatibilidadDeMigraciones_AmbosCamposConvivenSinConflictos()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var modelo = context.Model;

        // Migración 1: AgregarSeleccionExplicitaYamaha
        var ordenEntity = modelo.FindEntityType(typeof(OrdenServicio))!;
        var ventaEntity = modelo.FindEntityType(typeof(Venta))!;

        var propReportarOrden = ordenEntity.FindProperty(nameof(OrdenServicio.ReportarAYamaha))!;
        var propReportarVenta = ventaEntity.FindProperty(nameof(Venta.ReportarAYamaha))!;

        Assert.False(propReportarOrden.IsNullable);
        Assert.False(propReportarVenta.IsNullable);
        Assert.Equal(false, propReportarOrden.GetDefaultValue());
        Assert.Equal(false, propReportarVenta.GetDefaultValue());

        // Migración 2: AgregarIgvAlPrecioDelServicio
        var servicioEntity = modelo.FindEntityType(typeof(Servicio))!;
        var propServicioIgv = servicioEntity.FindProperty(nameof(Servicio.PrecioIncluyeIgv))!;

        Assert.False(propServicioIgv.IsNullable);
        Assert.Equal(false, propServicioIgv.GetDefaultValue());
    }
}
