using CRMTeamBenavides.Api.Features.Inventario;
using CRMTeamBenavides.Api.Features.Motos;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Las motos del inventario: cada una con su serie única, entra en stock y sale vendida,
/// y al venderla se crea la unidad del cliente. No se separan con adelanto (05/11/2026).
/// El bloqueo FOR UPDATE y las compras se prueban contra la API con PostgreSQL.
/// </summary>
public class MotosInventarioTests : IDisposable
{
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    private sealed record Escenario(Guid ModeloId, Guid OtroModeloId, Guid ClienteId, Guid MotoA, Guid MotoB);

    private Task<Escenario> EscenarioAsync() => _identity.ConAsync(async sp =>
    {
        var contexto = sp.GetRequiredService<ApplicationDbContext>();
        var categoria = new CategoriaProducto { Nombre = "Motos" };
        var modelo = new Producto { Categoria = categoria, Codigo = "YB125", Nombre = "YB125", Marca = "Yamaha", Tipo = TipoProducto.Vehiculo, StockActual = 2, Costo = 5000m };
        var otro = new Producto { Categoria = categoria, Codigo = "FZ25", Nombre = "FZ 25", Marca = "Yamaha", Tipo = TipoProducto.Vehiculo };
        var cliente = new Cliente { NombreCompleto = "Rosa Mamani" };
        var motoA = new UnidadInventario { Producto = modelo, NumeroSerieVIN = "ME1YB125A001", NumeroMotor = "E3X1", Anio = 2026, Color = "Negro", Dua = "118-2026-10-1234", ItemDua = "3", CostoUnitario = 4900m };
        var motoB = new UnidadInventario { Producto = modelo, NumeroSerieVIN = "ME1YB125A002", CostoUnitario = 5100m };
        contexto.AddRange(categoria, modelo, otro, cliente, motoA, motoB);
        await contexto.SaveChangesAsync();
        return new Escenario(modelo.Id, otro.Id, cliente.Id, motoA.Id, motoB.Id);
    });

    [Fact]
    public void Una_compra_pide_una_serie_por_moto_sin_repetirlas_y_las_guarda_en_mayusculas()
    {
        var (_, faltan) = MotosInventario.ValidarIngreso([new UnidadMotoRequest("A1")], 2, "Línea 1");
        var (_, repetida) = MotosInventario.ValidarIngreso([new UnidadMotoRequest("a1"), new UnidadMotoRequest(" A1 ")], 2, "Línea 1");
        var (motos, error) = MotosInventario.ValidarIngreso([new UnidadMotoRequest(" me1yb125a003 ", "e3x9", 2026, " Azul ", "118-2026", "4")], 1, "Línea 1");

        Assert.Equal("Línea 1: escribe la serie de cada moto. Son 2 y hay 1.", faltan);
        Assert.Equal("Línea 1: la serie A1 está repetida.", repetida);
        Assert.Null(error);
        Assert.Equal(new MotoNormalizada("ME1YB125A003", "E3X9", 2026, "Azul", "118-2026", "4"), motos[0]);
    }

    [Fact]
    public async Task Una_serie_del_inventario_o_de_un_cliente_no_vuelve_a_entrar()
    {
        await EscenarioAsync();
        await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var cliente = await contexto.Clientes.FirstAsync();
            contexto.Vehiculos.Add(new Vehiculo { Cliente = cliente, Marca = "Yamaha", Modelo = "MT-03", NumeroSerieVIN = "ME1MT03X9" });
            return await contexto.SaveChangesAsync();
        });

        var enInventario = await _identity.ConAsync(sp => MotosInventario.SerieOcupadaAsync(sp.GetRequiredService<ApplicationDbContext>(), ["ME1YB125A001"]));
        var deCliente = await _identity.ConAsync(sp => MotosInventario.SerieOcupadaAsync(sp.GetRequiredService<ApplicationDbContext>(), ["ME1MT03X9"]));
        var libre = await _identity.ConAsync(sp => MotosInventario.SerieOcupadaAsync(sp.GetRequiredService<ApplicationDbContext>(), ["NUEVA1"]));

        Assert.Equal("La moto con serie ME1YB125A001 ya está registrada en el inventario.", enInventario);
        Assert.Equal("La serie ME1MT03X9 ya es la unidad de un cliente: revisa Unidades.", deCliente);
        Assert.Null(libre);
    }

    [Fact]
    public async Task Al_vender_se_elige_una_moto_por_unidad_del_mismo_modelo()
    {
        var e = await EscenarioAsync();

        var (errorPocas, errorOtro, repuesto) = await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var modelo = await contexto.Productos.FindAsync(e.ModeloId);
            var otro = await contexto.Productos.FindAsync(e.OtroModeloId);
            var (_, p) = await MotosInventario.ValidarSeleccionAsync(contexto, modelo!, 2, [e.MotoA]);
            var (_, o) = await MotosInventario.ValidarSeleccionAsync(contexto, otro!, 1, [e.MotoA]);
            var filtro = new Producto { Nombre = "Filtro de aceite", Tipo = TipoProducto.Repuesto };
            var (_, r) = await MotosInventario.ValidarSeleccionAsync(contexto, filtro, 1, [e.MotoA]);
            return (p, o, r);
        });

        Assert.Equal("Elige la serie de cada moto «YB125»: son 2 y elegiste 1.", errorPocas);
        Assert.Equal("La moto ME1YB125A001 no es del modelo «FZ 25».", errorOtro);
        Assert.Equal("«Filtro de aceite» no es una moto: no lleva serie.", repuesto);
        Assert.Equal("La misma moto está elegida en dos líneas de la venta.", MotosInventario.MotoRepetida([[e.MotoA], [e.MotoA]]));
    }

    [Fact]
    public async Task Vender_crea_la_unidad_del_cliente_con_la_garantia_desde_hoy_y_anular_la_devuelve_al_stock()
    {
        var e = await EscenarioAsync();
        var ventaId = Guid.NewGuid();

        var error = await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var modelo = (await contexto.Productos.FindAsync(e.ModeloId))!;
            var venta = new Venta { Id = ventaId, ClienteId = e.ClienteId, Estado = EstadoVenta.Confirmada };
            var detalle = new DetalleVenta { Venta = venta, ProductoId = modelo.Id, Cantidad = 2, UnidadesInventarioIds = [e.MotoA, e.MotoB] };
            contexto.Ventas.Add(venta);
            var resultado = await MotosInventario.VenderAsync(contexto, venta, detalle, modelo);
            venta.Detalles.Add(detalle);
            await contexto.SaveChangesAsync();
            Assert.Equal(5000m, detalle.CostoUnitarioHistorico);
            return resultado;
        });
        Assert.Null(error);

        var (motoA, unidad) = await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var moto = await contexto.UnidadesInventario.SingleAsync(u => u.Id == e.MotoA);
            return (moto, await contexto.Vehiculos.SingleAsync(v => v.Id == moto.VehiculoId));
        });
        Assert.Equal(EstadoUnidadInventario.Vendida, motoA.Estado);
        Assert.Equal(ventaId, motoA.VentaId);
        Assert.Equal(e.ClienteId, unidad.ClienteId);
        Assert.Equal(("Yamaha", "YB125", "ME1YB125A001", "E3X1", "118-2026-10-1234"), (unidad.Marca, unidad.Modelo, unidad.NumeroSerieVIN, unidad.NumeroMotor, unidad.Dua));
        Assert.Equal(HoraPeru.Hoy(), unidad.FechaInicioGarantia);
        Assert.Null(unidad.NumeroTarjetaPropiedad);

        // Otra venta de la misma moto ya no pasa.
        var otraVez = await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var modelo = (await contexto.Productos.FindAsync(e.ModeloId))!;
            var (_, err) = await MotosInventario.ValidarSeleccionAsync(contexto, modelo, 1, [e.MotoA]);
            return err;
        });
        Assert.Equal("La moto ME1YB125A001 ya se vendió: elige otra.", otraVez);

        // Anular: vuelve al stock y la unidad sin órdenes se da de baja.
        await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            await MotosInventario.RevertirVentaAsync(contexto, ventaId);
            return await contexto.SaveChangesAsync();
        });
        var (devuelta, baja) = await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var moto = await contexto.UnidadesInventario.SingleAsync(u => u.Id == e.MotoA);
            return (moto, await contexto.Vehiculos.SingleAsync(v => v.Id == unidad.Id));
        });
        Assert.Equal(EstadoUnidadInventario.EnStock, devuelta.Estado);
        Assert.Null(devuelta.VentaId);
        Assert.Null(devuelta.VehiculoId);
        Assert.False(baja.Activo);
    }

    [Fact]
    public async Task El_stock_de_una_moto_no_se_mueve_a_mano()
    {
        var e = await EscenarioAsync();

        var entrada = await _identity.ConAsync(sp => new InventarioService(sp.GetRequiredService<ApplicationDbContext>(), sp.GetRequiredService<IAuditoriaService>())
            .RegistrarEntradaAsync(e.ModeloId, new RegistrarEntradaRequest(1, "Llegó una moto", null)));

        Assert.False(entrada.IsSuccess);
        Assert.Equal(MotosInventario.MensajeStockManual, entrada.Error);
    }

    [Fact]
    public async Task Un_modelo_con_motos_en_stock_no_se_da_de_baja_y_uno_sin_motos_si()
    {
        var e = await EscenarioAsync();
        InventarioService Servicio(IServiceProvider sp) =>
            new(sp.GetRequiredService<ApplicationDbContext>(), sp.GetRequiredService<IAuditoriaService>());

        var conMotos = await _identity.ConAsync(sp => Servicio(sp).DeleteProductoAsync(e.ModeloId));
        var sinMotos = await _identity.ConAsync(sp => Servicio(sp).DeleteProductoAsync(e.OtroModeloId));

        Assert.False(conMotos.IsSuccess);
        Assert.Equal("El modelo tiene 2 motos en stock: véndelas o anula su compra antes de darlo de baja.", conMotos.Error);
        Assert.True(sinMotos.IsSuccess, sinMotos.Error);
    }

    [Fact]
    public async Task El_codigo_de_un_modelo_dado_de_baja_se_puede_volver_a_usar()
    {
        var e = await EscenarioAsync();
        InventarioService Servicio(IServiceProvider sp) =>
            new(sp.GetRequiredService<ApplicationDbContext>(), sp.GetRequiredService<IAuditoriaService>());
        var categoriaId = await _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().CategoriasProducto.Select(c => c.Id).FirstAsync());
        var nuevo = new CreateProductoRequest(categoriaId, "FZ25", "Motocicleta Yamaha FZ 25", null, "unidad", 15000m, 0, Tipo: TipoProducto.Vehiculo);

        var mientrasEstaActivo = await _identity.ConAsync(sp => Servicio(sp).CreateProductoAsync(nuevo));
        await _identity.ConAsync(sp => Servicio(sp).DeleteProductoAsync(e.OtroModeloId));
        var despuesDeLaBaja = await _identity.ConAsync(sp => Servicio(sp).CreateProductoAsync(nuevo));

        Assert.Equal("Ya existe un producto registrado con el código 'FZ25'.", mientrasEstaActivo.Error);
        Assert.True(despuesDeLaBaja.IsSuccess, despuesDeLaBaja.Error);
    }

    [Fact]
    public async Task Una_moto_que_ya_estaba_en_la_tienda_se_ingresa_con_su_serie()
    {
        var e = await EscenarioAsync();

        var ingreso = await _identity.ConAsync(sp => new MotosService(
                sp.GetRequiredService<ApplicationDbContext>(), sp.GetRequiredService<IConfiguracionService>(), sp.GetRequiredService<IAuditoriaService>())
            .IngresarAsync(e.OtroModeloId, new IngresarMotoRequest(new UnidadMotoRequest("me1fz25b001", Anio: 2025, Color: "Azul"), 9800m), usuarioId: null));

        Assert.True(ingreso.IsSuccess, ingreso.Error);
        Assert.Equal(("ME1FZ25B001", "En stock"), (ingreso.Data!.NumeroSerieVIN, ingreso.Data.Estado));
        var modelo = await _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().Productos.SingleAsync(p => p.Id == e.OtroModeloId));
        Assert.Equal((1, 9800m), (modelo.StockActual, modelo.Costo));
    }
}
