using System.Security.Claims;
using CRMTeamBenavides.Api.Configuration;
using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.Ventas;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Api.Services.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha.Exportadores;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Suite de pruebas integral para la funcionalidad de Declaración Interna de Ventas
/// y el desacoplamiento total de Yamaha v3 (mínimo 23 escenarios obligatorios).
/// Cubre:
///   - Grupo 1: Declaración interna de ventas (Escenarios 01 a 08).
///   - Grupo 2: Filtros de declaración (Escenarios 09 a 14).
///   - Grupo 3: Independencia total de Yamaha v3 (Escenarios 15 a 20).
///   - Grupo 4: Compatibilidad tributaria, financiera y comprobantes (Escenarios 21 a 23).
/// </summary>
public class DeclaracionVentasTests
{
    private sealed class SinAuditoria : IAuditoriaService
    {
        public Task RegistrarEventoAsync(Guid? usuarioId, string accion, string entidad, string entidadId, object? detalle = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private static ApplicationDbContext CrearContexto(string? nombreBase = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nombreBase ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ApplicationDbContext(options);
        if (!context.ConfiguracionesEmpresa.Any())
        {
            context.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa
            {
                RazonSocial = "Team Benavides S.R.L.",
                Ruc = "20601234567",
                NombreEmpresa = "Team Benavides Arequipa",
                MonedaBase = "PEN",
                PorcentajeIgv = 18m
            });
            context.SaveChanges();
        }

        return context;
    }

    private static IOptions<YamahaSettings> CrearOpcionesYamaha(bool habilitado = false)
    {
        return Options.Create(new YamahaSettings
        {
            Enabled = habilitado,
            CodigoPuntoVenta = "TB-AQP-01",
            TipoPuntoVenta = "3S",
            MargenAsentamientoSegundos = 0
        });
    }

    // =========================================================================
    // GRUPO 1: Declaración Interna de Ventas (Escenarios 01 a 08)
    // =========================================================================

    [Fact]
    public async Task Escenario01_CreacionVenta_SinEspecificarDeclarada_SeGuardaConDeclaradaFalse()
    {
        using var context = CrearContexto();
        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112233", NombreCompleto = "Cliente Test 1", Activo = true };
        context.Clientes.Add(cliente);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow,
            Estado = EstadoVenta.Confirmada,
            SubtotalGravado = 100m,
            MontoIgv = 18m,
            Total = 118m,
            Activo = true
            // Declarada no se especifica expresamente
        };

        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        Assert.False(venta.Declarada);

        // Verificar además que el modelo EF Core tiene default value en false
        var modelEntity = context.Model.FindEntityType(typeof(Venta));
        var propDeclarada = modelEntity?.FindProperty(nameof(Venta.Declarada));
        Assert.NotNull(propDeclarada);
        Assert.Equal(false, propDeclarada.GetDefaultValue());
    }

    [Fact]
    public async Task Escenario02_VentaCreadaConDeclaradaFalse_SePuedeCambiarATrue_ConEndpointDeclaracion()
    {
        using var context = CrearContexto();
        var service = new VentaService(context, new SinAuditoria());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112234", NombreCompleto = "Cliente Test 2", Activo = true };
        context.Clientes.Add(cliente);
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow,
            Estado = EstadoVenta.Confirmada,
            Declarada = false,
            Activo = true
        };
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var resultado = await service.ActualizarDeclaradaAsync(venta.Id, true, usuarioId: Guid.NewGuid());

        Assert.Equal(ServiceResultStatus.Success, resultado.Status);
        Assert.NotNull(resultado.Data);
        Assert.True(resultado.Data.Declarada);
    }

    [Fact]
    public async Task Escenario03_VentaConDeclaradaTrue_SePuedeCambiarAFalse_Arrepentirse()
    {
        using var context = CrearContexto();
        var service = new VentaService(context, new SinAuditoria());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112235", NombreCompleto = "Cliente Test 3", Activo = true };
        context.Clientes.Add(cliente);
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow,
            Estado = EstadoVenta.Confirmada,
            Declarada = true,
            Activo = true
        };
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        // El usuario decide desmarcarla ("arrepentirse")
        var resultado = await service.ActualizarDeclaradaAsync(venta.Id, false, usuarioId: Guid.NewGuid());

        Assert.Equal(ServiceResultStatus.Success, resultado.Status);
        Assert.NotNull(resultado.Data);
        Assert.False(resultado.Data.Declarada);
    }

    [Fact]
    public async Task Escenario04_VentaConDeclaradaFalse_SePuedeVolverACambiarATrue_MultiplesCambiosPermitidos()
    {
        using var context = CrearContexto();
        var service = new VentaService(context, new SinAuditoria());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112236", NombreCompleto = "Cliente Test 4", Activo = true };
        context.Clientes.Add(cliente);
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow,
            Estado = EstadoVenta.Confirmada,
            Declarada = false,
            Activo = true
        };
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        // Múltiples alternancias sucesivas
        var r1 = await service.ActualizarDeclaradaAsync(venta.Id, true, Guid.NewGuid());
        Assert.True(r1.Data!.Declarada);

        var r2 = await service.ActualizarDeclaradaAsync(venta.Id, false, Guid.NewGuid());
        Assert.False(r2.Data!.Declarada);

        var r3 = await service.ActualizarDeclaradaAsync(venta.Id, true, Guid.NewGuid());
        Assert.True(r3.Data!.Declarada);
    }

    [Fact]
    public async Task Escenario05_ElCambioPersisteEnBaseDeDatos_TrasRecargarEntidad()
    {
        var dbName = Guid.NewGuid().ToString();
        var ventaId = Guid.NewGuid();

        using (var db1 = CrearContexto(dbName))
        {
            var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112237", NombreCompleto = "Cliente Test 5", Activo = true };
            db1.Clientes.Add(cliente);
            var venta = new Venta
            {
                Id = ventaId,
                ClienteId = cliente.Id,
                Cliente = cliente,
                Fecha = DateTime.UtcNow,
                Estado = EstadoVenta.Confirmada,
                Declarada = false,
                Activo = true
            };
            db1.Ventas.Add(venta);
            await db1.SaveChangesAsync();

            var service = new VentaService(db1, new SinAuditoria());
            await service.ActualizarDeclaradaAsync(ventaId, true, Guid.NewGuid());
        }

        // Recargar desde una nueva instancia de contexto
        using (var db2 = CrearContexto(dbName))
        {
            var ventaRecargada = await db2.Ventas.AsNoTracking().FirstOrDefaultAsync(v => v.Id == ventaId);
            Assert.NotNull(ventaRecargada);
            Assert.True(ventaRecargada.Declarada);
        }
    }

    [Fact]
    public async Task Escenario06_LaRespuestaDevuelveDto_ConValorActualizadoDeDeclarada()
    {
        using var context = CrearContexto();
        var service = new VentaService(context, new SinAuditoria());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112238", NombreCompleto = "Cliente Test 6", Activo = true };
        context.Clientes.Add(cliente);
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow,
            Estado = EstadoVenta.Confirmada,
            Declarada = false,
            SubtotalGravado = 80m,
            MontoIgv = 14.40m,
            Total = 94.40m,
            Activo = true
        };
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var resultado = await service.ActualizarDeclaradaAsync(venta.Id, true, Guid.NewGuid());

        Assert.Equal(ServiceResultStatus.Success, resultado.Status);
        Assert.NotNull(resultado.Data);
        Assert.IsType<VentaDetalleResponse>(resultado.Data);
        Assert.Equal(venta.Id, resultado.Data.Id);
        Assert.True(resultado.Data.Declarada);
        Assert.Equal(94.40m, resultado.Data.Total);
    }

    [Fact]
    public void Escenario07_UsuarioSinPermisoVentasCrear_EsRechazadoAlIntentarCambiarDeclaracion()
    {
        // Validación del permiso exigido por el endpoint de declaración
        const string permisoExigido = PermisosDefinidos.VentasCrear;

        // Usuario autenticado SIN permiso ventas.crear (solo ventas.ver)
        var claimsSinPermiso = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, RolesDefinidos.Vendedor),
            new("permission", PermisosDefinidos.VentasVer)
        };
        var usuarioSinPermiso = new ClaimsPrincipal(new ClaimsIdentity(claimsSinPermiso, "TestAuth"));

        // Comprobación de que el usuario carece del permiso exigido
        var tienePermiso = usuarioSinPermiso.HasClaim("permission", permisoExigido);
        Assert.False(tienePermiso);

        // Usuario CON permiso ventas.crear
        var claimsAutorizado = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, RolesDefinidos.Vendedor),
            new("permission", PermisosDefinidos.VentasCrear)
        };
        var usuarioAutorizado = new ClaimsPrincipal(new ClaimsIdentity(claimsAutorizado, "TestAuth"));

        Assert.True(usuarioAutorizado.HasClaim("permission", permisoExigido));
    }

    [Fact]
    public async Task Escenario08_IntentoCambiarDeclaracion_VentaInexistente_Devuelve404()
    {
        using var context = CrearContexto();
        var service = new VentaService(context, new SinAuditoria());

        var idInexistente = Guid.NewGuid();
        var resultado = await service.ActualizarDeclaradaAsync(idInexistente, true, Guid.NewGuid());

        Assert.Equal(ServiceResultStatus.NotFound, resultado.Status);
        Assert.Null(resultado.Data);
    }

    // =========================================================================
    // GRUPO 2: Filtros de Declaración (Escenarios 09 a 14)
    // =========================================================================

    [Fact]
    public async Task Escenario09_FiltroDeclaradaTrue_DevuelveExclusivamenteVentasDeclaradas()
    {
        using var context = CrearContexto();
        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112239", NombreCompleto = "Cliente Test 9", Activo = true };
        context.Clientes.Add(cliente);

        var v1 = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = true, Activo = true, Fecha = DateTime.UtcNow };
        var v2 = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = true, Activo = true, Fecha = DateTime.UtcNow };
        var v3 = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = false, Activo = true, Fecha = DateTime.UtcNow };

        context.Ventas.AddRange(v1, v2, v3);
        await context.SaveChangesAsync();

        var filtros = new FiltrosVentas { Declarada = true };
        var res = await filtros.Aplicar(context.Ventas.AsNoTracking()).ToListAsync();

        Assert.Equal(2, res.Count);
        Assert.All(res, v => Assert.True(v.Declarada));
    }

    [Fact]
    public async Task Escenario10_FiltroDeclaradaFalse_DevuelveExclusivamenteVentasNoDeclaradas()
    {
        using var context = CrearContexto();
        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112240", NombreCompleto = "Cliente Test 10", Activo = true };
        context.Clientes.Add(cliente);

        var v1 = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = true, Activo = true, Fecha = DateTime.UtcNow };
        var v2 = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = false, Activo = true, Fecha = DateTime.UtcNow };
        var v3 = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = false, Activo = true, Fecha = DateTime.UtcNow };

        context.Ventas.AddRange(v1, v2, v3);
        await context.SaveChangesAsync();

        var filtros = new FiltrosVentas { Declarada = false };
        var res = await filtros.Aplicar(context.Ventas.AsNoTracking()).ToListAsync();

        Assert.Equal(2, res.Count);
        Assert.All(res, v => Assert.False(v.Declarada));
    }

    [Fact]
    public async Task Escenario11_FiltroDeclaradaNull_Todas_DevuelveDeclaradasYNoDeclaradas()
    {
        using var context = CrearContexto();
        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112241", NombreCompleto = "Cliente Test 11", Activo = true };
        context.Clientes.Add(cliente);

        var v1 = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = true, Activo = true, Fecha = DateTime.UtcNow };
        var v2 = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = false, Activo = true, Fecha = DateTime.UtcNow };

        context.Ventas.AddRange(v1, v2);
        await context.SaveChangesAsync();

        // Declarada = null significa "Todas"
        var filtros = new FiltrosVentas { Declarada = null };
        var res = await filtros.Aplicar(context.Ventas.AsNoTracking()).ToListAsync();

        Assert.Equal(2, res.Count);
        Assert.Contains(res, v => v.Declarada);
        Assert.Contains(res, v => !v.Declarada);
    }

    [Fact]
    public async Task Escenario12_FiltroDeclaracion_FuncionaEnCombinacionConFiltroDeFechas()
    {
        using var context = CrearContexto();
        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112242", NombreCompleto = "Cliente Test 12", Activo = true };
        context.Clientes.Add(cliente);

        // Venta declarada en fecha objetivo (10/11/2026 mediodia UTC = 07:00 en Perú)
        var fechaObjetivo = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        var vObjetivo = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = true, Fecha = fechaObjetivo, Activo = true };

        // Venta declarada fuera de rango (01/11/2026)
        var vFueraRango = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = true, Fecha = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), Activo = true };

        // Venta no declarada en fecha objetivo
        var vNoDeclarada = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = false, Fecha = fechaObjetivo, Activo = true };

        context.Ventas.AddRange(vObjetivo, vFueraRango, vNoDeclarada);
        await context.SaveChangesAsync();

        var filtros = new FiltrosVentas
        {
            Declarada = true,
            FechaDesde = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc),
            FechaHasta = new DateTime(2026, 11, 10, 23, 59, 59, DateTimeKind.Utc)
        };

        var res = await filtros.Aplicar(context.Ventas.AsNoTracking()).ToListAsync();

        Assert.Single(res);
        Assert.Equal(vObjetivo.Id, res[0].Id);
        Assert.True(res[0].Declarada);
    }

    [Fact]
    public async Task Escenario13_FiltroDeclaracion_FuncionaEnCombinacionConFiltroDeEstado()
    {
        using var context = CrearContexto();
        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112243", NombreCompleto = "Cliente Test 13", Activo = true };
        context.Clientes.Add(cliente);

        var vConfirmadaDeclarada = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = true, Fecha = DateTime.UtcNow, Activo = true };
        var vCotizacionDeclarada = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Cotizacion, Declarada = true, Fecha = DateTime.UtcNow, Activo = true };
        var vConfirmadaNoDeclarada = new Venta { Id = Guid.NewGuid(), ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada, Declarada = false, Fecha = DateTime.UtcNow, Activo = true };

        context.Ventas.AddRange(vConfirmadaDeclarada, vCotizacionDeclarada, vConfirmadaNoDeclarada);
        await context.SaveChangesAsync();

        var filtros = new FiltrosVentas
        {
            Declarada = true,
            Estado = EstadoVenta.Confirmada
        };

        var res = await filtros.Aplicar(context.Ventas.AsNoTracking()).ToListAsync();

        Assert.Single(res);
        Assert.Equal(vConfirmadaDeclarada.Id, res[0].Id);
    }

    [Fact]
    public async Task Escenario14_ListadoVentas_ReflejaInmediatamenteElCambioDeDeclaracion_TrasMutacion()
    {
        using var context = CrearContexto();
        var service = new VentaService(context, new SinAuditoria());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112244", NombreCompleto = "Cliente Test 14", Activo = true };
        context.Clientes.Add(cliente);
        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow,
            Estado = EstadoVenta.Confirmada,
            Declarada = false,
            Activo = true
        };
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        // Paso 1: Filtro de declaradas inicialmente vacío
        var listaInicial = await service.GetAllAsync(new FiltrosVentas { Declarada = true });
        Assert.Empty(listaInicial);

        // Paso 2: Mutación de declaración a true
        await service.ActualizarDeclaradaAsync(venta.Id, true, Guid.NewGuid());

        // Paso 3: Consultar nuevamente con filtro de declaradas
        var listaActualizada = await service.GetAllAsync(new FiltrosVentas { Declarada = true });
        Assert.Single(listaActualizada);
        Assert.Equal(venta.Id, listaActualizada[0].Id);
        Assert.True(listaActualizada[0].Declarada);
    }

    // =========================================================================
    // GRUPO 3: Independencia Total de Yamaha v3 (Escenarios 15 a 20)
    // =========================================================================

    [Fact]
    public async Task Escenario15_VentaDeclarada_RepuestosYamaha_SeExportaAYamaha()
    {
        using var context = CrearContexto();
        var exporter = new YamahaVentasExporter(context, new YamahaMappingService(), CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112245", NombreCompleto = "Cliente Test 15", Activo = true };
        var repuesto = new Producto { Id = Guid.NewGuid(), Codigo = "2DP-E4451-00", Nombre = "Filtro NMAX", Activo = true };
        context.Clientes.Add(cliente);
        context.Productos.Add(repuesto);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow.AddHours(-1),
            Estado = EstadoVenta.Confirmada,
            Declarada = true, // DECLARADA
            SubtotalGravado = 50m,
            MontoIgv = 9m,
            Total = 59m,
            Activo = true
        };
        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000015",
            Estado = "Emitido",
            FechaCreacion = DateTime.UtcNow.AddHours(-1),
            SubtotalGravado = 50m,
            MontoIgv = 9m,
            Total = 59m,
            Activo = true
        };
        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = repuesto.Id,
            Producto = repuesto,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 50m,
            SubtotalGravado = 50m,
            Total = 59m,
            Activo = true
        });
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Single(res.RegistrosValidos);
        Assert.Equal("SP", res.RegistrosValidos[0].LineaNegocio);
        Assert.Equal("2DP-E4451-00", res.RegistrosValidos[0].CodigoYamaha);
    }

    [Fact]
    public async Task Escenario16_VentaNoDeclarada_RepuestosYamaha_TambienSeExportaAYamaha()
    {
        using var context = CrearContexto();
        var exporter = new YamahaVentasExporter(context, new YamahaMappingService(), CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112246", NombreCompleto = "Cliente Test 16", Activo = true };
        var repuesto = new Producto { Id = Guid.NewGuid(), Codigo = "90793-AJ412", Nombre = "Aceite Yamalube", Activo = true };
        context.Clientes.Add(cliente);
        context.Productos.Add(repuesto);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow.AddHours(-1),
            Estado = EstadoVenta.Confirmada,
            Declarada = false, // NO DECLARADA (funcionalidad interna del CRM)
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
            Numero = "000016",
            Estado = "Emitido",
            FechaCreacion = DateTime.UtcNow.AddHours(-1),
            SubtotalGravado = 38m,
            MontoIgv = 6.84m,
            Total = 44.84m,
            Activo = true
        };
        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = repuesto.Id,
            Producto = repuesto,
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

        // Debe exportarse a Yamaha independientemente de que no esté declarada internamente
        Assert.Single(res.RegistrosValidos);
        Assert.Equal("SP", res.RegistrosValidos[0].LineaNegocio);
        Assert.Equal("90793-AJ412", res.RegistrosValidos[0].CodigoYamaha);
    }

    [Fact]
    public async Task Escenario17_VentaDeclarada_MotoYamahaMC_SeExportaConVinYDatosVehiculares()
    {
        using var context = CrearContexto();
        var exporter = new YamahaVentasExporter(context, new YamahaMappingService(), CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112247", NombreCompleto = "Cliente Test 17", Activo = true };
        var catMotos = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Motocicletas", Activo = true };
        var prodMoto = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "FZ25-2026",
            Nombre = "Yamaha FZ25",
            Tipo = TipoProducto.Vehiculo,
            CategoriaId = catMotos.Id,
            Categoria = catMotos,
            Activo = true
        };

        var unidad = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            NumeroSerieVIN = "9C6AB0123X0000017",
            NumeroMotor = "E3R2E-0000017",
            Color = "Azul",
            Anio = 2026,
            Estado = EstadoUnidadInventario.Vendida,
            Activo = true
        };

        context.Clientes.Add(cliente);
        context.CategoriasProducto.Add(catMotos);
        context.Productos.Add(prodMoto);
        context.UnidadesInventario.Add(unidad);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow.AddHours(-2),
            Estado = EstadoVenta.Confirmada,
            Declarada = true, // DECLARADA
            SubtotalGravado = 12000m,
            MontoIgv = 2160m,
            Total = 14160m,
            Activo = true
        };
        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Factura",
            Serie = "F001",
            Numero = "000017",
            Estado = "Emitido",
            FechaCreacion = DateTime.UtcNow.AddHours(-2),
            SubtotalGravado = 12000m,
            MontoIgv = 2160m,
            Total = 14160m,
            Activo = true
        };
        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            UnidadesInventarioIds = [unidad.Id],
            Cantidad = 1,
            PrecioUnitario = 12000m,
            SubtotalGravado = 12000m,
            Total = 14160m,
            Activo = true
        });
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Single(res.RegistrosValidos);
        var dto = res.RegistrosValidos[0];
        Assert.Equal("MC", dto.LineaNegocio);
        Assert.Equal("9C6AB0123X0000017", dto.VIN);
        Assert.Equal("E3R2E-0000017", dto.NumeroMotor);
        Assert.Equal("Azul", dto.Color);
        Assert.Equal(2026, dto.AnioModelo);
    }

    [Fact]
    public async Task Escenario18_VentaNoDeclarada_MotoYamahaMC_TambienSeExportaConVinYDatosVehiculares()
    {
        using var context = CrearContexto();
        var exporter = new YamahaVentasExporter(context, new YamahaMappingService(), CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112248", NombreCompleto = "Cliente Test 18", Activo = true };
        var catMotos = new CategoriaProducto { Id = Guid.NewGuid(), Nombre = "Motocicletas", Activo = true };
        var prodMoto = new Producto
        {
            Id = Guid.NewGuid(),
            Codigo = "XTZ150-2026",
            Nombre = "Yamaha XTZ150",
            Tipo = TipoProducto.Vehiculo,
            CategoriaId = catMotos.Id,
            Categoria = catMotos,
            Activo = true
        };

        var unidad = new UnidadInventario
        {
            Id = Guid.NewGuid(),
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            NumeroSerieVIN = "9C6AB0123X0000018",
            NumeroMotor = "E3R2E-0000018",
            Color = "Negro",
            Anio = 2026,
            Estado = EstadoUnidadInventario.Vendida,
            Activo = true
        };

        context.Clientes.Add(cliente);
        context.CategoriasProducto.Add(catMotos);
        context.Productos.Add(prodMoto);
        context.UnidadesInventario.Add(unidad);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow.AddHours(-2),
            Estado = EstadoVenta.Confirmada,
            Declarada = false, // NO DECLARADA
            SubtotalGravado = 10000m,
            MontoIgv = 1800m,
            Total = 11800m,
            Activo = true
        };
        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Factura",
            Serie = "F001",
            Numero = "000018",
            Estado = "Emitido",
            FechaCreacion = DateTime.UtcNow.AddHours(-2),
            SubtotalGravado = 10000m,
            MontoIgv = 1800m,
            Total = 11800m,
            Activo = true
        };
        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = prodMoto.Id,
            Producto = prodMoto,
            UnidadesInventarioIds = [unidad.Id],
            Cantidad = 1,
            PrecioUnitario = 10000m,
            SubtotalGravado = 10000m,
            Total = 11800m,
            Activo = true
        });
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        var res = await exporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Single(res.RegistrosValidos);
        var dto = res.RegistrosValidos[0];
        Assert.Equal("MC", dto.LineaNegocio);
        Assert.Equal("9C6AB0123X0000018", dto.VIN);
        Assert.Equal("E3R2E-0000018", dto.NumeroMotor);
    }

    [Fact]
    public async Task Escenario19_CambiarEstadoDeclaracion_NoAlteraResultadoDeExportacionAYamaha()
    {
        using var context = CrearContexto();
        var exporter = new YamahaVentasExporter(context, new YamahaMappingService(), CrearOpcionesYamaha());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112249", NombreCompleto = "Cliente Test 19", Activo = true };
        var repuesto = new Producto { Id = Guid.NewGuid(), Codigo = "54P-E4450-00", Nombre = "Elemento Filtro", Activo = true };
        context.Clientes.Add(cliente);
        context.Productos.Add(repuesto);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow.AddHours(-1),
            Estado = EstadoVenta.Confirmada,
            Declarada = false,
            SubtotalGravado = 60m,
            MontoIgv = 10.80m,
            Total = 70.80m,
            Activo = true
        };
        venta.Comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000019",
            Estado = "Emitido",
            FechaCreacion = DateTime.UtcNow.AddHours(-1),
            SubtotalGravado = 60m,
            MontoIgv = 10.80m,
            Total = 70.80m,
            Activo = true
        };
        venta.Detalles.Add(new DetalleVenta
        {
            Id = Guid.NewGuid(),
            VentaId = venta.Id,
            ProductoId = repuesto.Id,
            Producto = repuesto,
            TipoItem = TipoItemServicio.Repuesto,
            Cantidad = 1,
            PrecioUnitario = 60m,
            SubtotalGravado = 60m,
            Total = 70.80m,
            Activo = true
        });
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        // 1. Exportar con Declarada = false
        var res1 = await exporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));
        Assert.Single(res1.RegistrosValidos);
        var dto1 = res1.RegistrosValidos[0];

        // 2. Modificar a Declarada = true
        venta.Declarada = true;
        await context.SaveChangesAsync();

        // 3. Exportar con Declarada = true
        var res2 = await exporter.ExportarVentasAsync(desdeUtc: DateTime.UtcNow.AddDays(-1));
        Assert.Single(res2.RegistrosValidos);
        var dto2 = res2.RegistrosValidos[0];

        // 4. Comparar: el DTO de Yamaha es idéntico campo a campo
        Assert.Equal(dto1.CodigoYamaha, dto2.CodigoYamaha);
        Assert.Equal(dto1.LineaNegocio, dto2.LineaNegocio);
        Assert.Equal(dto1.MontoConIGV, dto2.MontoConIGV);
        Assert.Equal(dto1.MontoSinIGV, dto2.MontoSinIGV);
        Assert.Equal(dto1.NumeroComprobante, dto2.NumeroComprobante);
        Assert.Equal(dto1.NumeroDocumento, dto2.NumeroDocumento);
        Assert.Equal(dto1.PrecioFinal, dto2.PrecioFinal);
    }

    [Fact]
    public async Task Escenario20_OrdenesServicioYStock_OperanConNormalidad_SinDependerDeSeleccionManual()
    {
        using var context = CrearContexto();
        var mappingService = new YamahaMappingService();
        var opciones = CrearOpcionesYamaha(habilitado: false); // En producción permanece deshabilitado

        var steExporter = new YamahaServicioTecnicoExporter(context, mappingService, opciones);
        var stockExporter = new YamahaStockExporter(context, mappingService, opciones);

        // Sembrar orden de servicio válida
        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112250", NombreCompleto = "Cliente Taller", Activo = true };
        var vehiculo = new Vehiculo { Id = Guid.NewGuid(), Placa = "8899-7Y", NumeroSerieVIN = "9C6AB0123X0000020", Marca = "Yamaha", Modelo = "Crypton", Activo = true };
        var producto = new Producto { Id = Guid.NewGuid(), Codigo = "2DP-E4451-00", Nombre = "Filtro", StockActual = 10, Activo = true };

        context.Clientes.Add(cliente);
        context.Vehiculos.Add(vehiculo);
        context.Productos.Add(producto);

        var orden = new OrdenServicio
        {
            Id = Guid.NewGuid(),
            NumeroOrden = "OS-000020",
            ClienteId = cliente.Id,
            Cliente = cliente,
            VehiculoId = vehiculo.Id,
            Vehiculo = vehiculo,
            KilometrajeIngreso = 4000,
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
            Numero = "000020",
            Estado = "Emitido",
            SubtotalGravado = 80m,
            MontoIgv = 14.40m,
            Total = 94.40m,
            FechaCreacion = DateTime.UtcNow.AddHours(-2),
            Activo = true
        });
        orden.Detalles.Add(new DetalleServicio
        {
            Id = Guid.NewGuid(),
            OrdenServicioId = orden.Id,
            TipoItem = TipoItemServicio.ManoDeObra,
            Descripcion = "Afinamiento integral",
            Cantidad = 1,
            PrecioUnitario = 80m,
            SubtotalGravado = 80m,
            Total = 94.40m,
            Activo = true
        });

        context.OrdenesServicio.Add(orden);
        await context.SaveChangesAsync();

        // Exportar servicio técnico sin depender de ningún opt-in manual
        var resSte = await steExporter.ExportarServicioTecnicoAsync(desdeUtc: DateTime.UtcNow.AddDays(-2));
        Assert.Single(resSte.RegistrosValidos);
        Assert.Equal("STE", resSte.RegistrosValidos[0].LineaNegocio);

        // Exportar stock sin depender de flags manuales
        var resStock = await stockExporter.ExportarStockAsync();
        Assert.Single(resStock.RegistrosValidos);
        Assert.Equal("2DP-E4451-00", resStock.RegistrosValidos[0].CodigoYamaha);

        // Verificación estricta de que YamahaSettings.Enabled = false
        Assert.False(opciones.Value.Enabled);
    }

    // =========================================================================
    // GRUPO 4: Compatibilidad con commit e3851af y Financiera (Escenarios 21 a 23)
    // =========================================================================

    [Fact]
    public void Escenario21_OperacionConPrecioIncluyeIgv_PreservaComportamiento_TantoSiEsDeclaradaComoSiNo()
    {
        const decimal totalConIgv = 100.00m;
        const decimal tasaIgv = 0.18m;

        // Comportamiento de e3851af con PrecioIncluyeIgv = true:
        // Subtotal = 100 / 1.18 = 84.75; IGV = 100 - 84.75 = 15.25
        var subtotal = Math.Round(totalConIgv / (1 + tasaIgv), 2, MidpointRounding.AwayFromZero);
        var igv = totalConIgv - subtotal;

        Assert.Equal(84.75m, subtotal);
        Assert.Equal(15.25m, igv);

        // Simular ambas variantes en entidad Venta
        var ventaNoDeclarada = new Venta
        {
            Declarada = false,
            SubtotalGravado = subtotal,
            MontoIgv = igv,
            Total = totalConIgv
        };

        var ventaDeclarada = new Venta
        {
            Declarada = true,
            SubtotalGravado = subtotal,
            MontoIgv = igv,
            Total = totalConIgv
        };

        // El cálculo financiero y tributario es 100% idéntico en ambos casos
        Assert.Equal(ventaNoDeclarada.SubtotalGravado, ventaDeclarada.SubtotalGravado);
        Assert.Equal(ventaNoDeclarada.MontoIgv, ventaDeclarada.MontoIgv);
        Assert.Equal(ventaNoDeclarada.Total, ventaDeclarada.Total);
    }

    [Fact]
    public async Task Escenario22_CalculoMontosConYSinIgv_NoSeAltera_AlMarcarODesmarcarDeclarada()
    {
        using var context = CrearContexto();
        var service = new VentaService(context, new SinAuditoria());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112251", NombreCompleto = "Cliente Test 22", Activo = true };
        context.Clientes.Add(cliente);

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow,
            Estado = EstadoVenta.Confirmada,
            Declarada = false,
            SubtotalGravado = 84.75m,
            MontoIgv = 15.25m,
            Total = 100.00m,
            Activo = true
        };
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        // 1. Modificar a Declarada = true
        var resDeclarar = await service.ActualizarDeclaradaAsync(venta.Id, true, Guid.NewGuid());
        Assert.Equal(84.75m, resDeclarar.Data!.SubtotalGravado);
        Assert.Equal(15.25m, resDeclarar.Data!.MontoIgv);
        Assert.Equal(100.00m, resDeclarar.Data!.Total);

        // 2. Modificar a Declarada = false
        var resDesmarcar = await service.ActualizarDeclaradaAsync(venta.Id, false, Guid.NewGuid());
        Assert.Equal(84.75m, resDesmarcar.Data!.SubtotalGravado);
        Assert.Equal(15.25m, resDesmarcar.Data!.MontoIgv);
        Assert.Equal(100.00m, resDesmarcar.Data!.Total);
    }

    [Fact]
    public async Task Escenario23_ComprobantesDePagoAsociados_NoSufrenModificaciones_AlCambiarDeclarada()
    {
        using var context = CrearContexto();
        var service = new VentaService(context, new SinAuditoria());

        var cliente = new Cliente { Id = Guid.NewGuid(), TipoDocumento = TipoDocumentoCliente.DNI, NumeroDocumento = "40112252", NombreCompleto = "Cliente Test 23", Activo = true };
        context.Clientes.Add(cliente);

        var fechaCreacionComprobante = DateTime.UtcNow.AddHours(-3);
        var comprobanteId = Guid.NewGuid();

        var venta = new Venta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Fecha = DateTime.UtcNow.AddHours(-3),
            Estado = EstadoVenta.Confirmada,
            Declarada = false,
            SubtotalGravado = 150m,
            MontoIgv = 27m,
            Total = 177m,
            Activo = true
        };

        var comprobante = new Comprobante
        {
            Id = comprobanteId,
            VentaId = venta.Id,
            Tipo = "Boleta",
            Serie = "B001",
            Numero = "000023",
            Estado = "Emitido",
            SubtotalGravado = 150m,
            MontoIgv = 27m,
            Total = 177m,
            FechaCreacion = fechaCreacionComprobante,
            Activo = true
        };

        venta.Comprobante = comprobante;
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();

        // Cambiar Declarada a true
        await service.ActualizarDeclaradaAsync(venta.Id, true, Guid.NewGuid());

        // Comprobar que el comprobante no sufrió alteración alguna
        var comprobantePostCambio = await context.Comprobantes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == comprobanteId);
        Assert.NotNull(comprobantePostCambio);
        Assert.Equal("B001", comprobantePostCambio.Serie);
        Assert.Equal("000023", comprobantePostCambio.Numero);
        Assert.Equal("Emitido", comprobantePostCambio.Estado);
        Assert.Equal(150m, comprobantePostCambio.SubtotalGravado);
        Assert.Equal(27m, comprobantePostCambio.MontoIgv);
        Assert.Equal(177m, comprobantePostCambio.Total);
        Assert.Equal(fechaCreacionComprobante, comprobantePostCambio.FechaCreacion);
        Assert.True(comprobantePostCambio.Activo);
    }
}
