using CRMTeamBenavides.Api.Configuration.Autorizacion;
using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Un precio que Gerencia rechaza vuelve al de lista y la operación sigue; quien lo pidió
/// se entera. Y el cliente no aprueba un presupuesto con precios por resolver.
/// </summary>
public class RechazoPrecioTests : IDisposable
{
    private const decimal PrecioLista = 50m;
    private const decimal PrecioPedido = 1m;
    private readonly IdentityDePrueba _identity = new();

    public void Dispose() => _identity.Dispose();

    private sealed record Escenario(Guid TecnicoId, Guid GerenciaId, Guid ProductoId, Guid OrdenId, Guid DetalleId, Guid SolicitudId);

    /// <summary>Una orden en diagnóstico con un repuesto que el técnico pidió a S/ 1 en vez de S/ 50.</summary>
    private async Task<Escenario> OrdenConPrecioPedidoAsync()
    {
        await _identity.SembrarRolesAsync();
        var tecnico = await _identity.CrearUsuarioAsync("tecnico@teambenavides.pe", RolesDefinidos.Tecnico);
        var gerencia = await _identity.CrearUsuarioAsync("gerencia@teambenavides.pe", RolesDefinidos.GerenciaAdmin);

        return await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var (cliente, vehiculo, producto) = await DatosBaseAsync(contexto);

            var orden = new OrdenServicio
            {
                NumeroOrden = "OS-000001",
                ClienteId = cliente.Id,
                VehiculoId = vehiculo.Id,
                TecnicoAsignadoId = tecnico.Id,
                Estado = EstadoOrdenServicio.Diagnostico,
                EstadoAprobacionGerencia = EstadoAprobacionGerencia.Pendiente,
                Activo = true
            };
            var detalle = Linea(orden.Id, producto, PrecioPedido);
            orden.Detalles.Add(detalle);
            OrdenServicioService.RecalcularTotalesOrden(orden);
            contexto.OrdenesServicio.Add(orden);

            var solicitud = new SolicitudAprobacion
            {
                Tipo = "CambioPrecio",
                Entidad = "OrdenServicio",
                EntidadId = orden.Id.ToString(),
                UsuarioSolicitanteId = tecnico.Id,
                Estado = EstadoAprobacionGerencia.Pendiente,
                DetalleCambio = "Modificación de precio en repuesto 'Filtro de aceite'",
                DetalleId = detalle.Id,
                ClaveObjetivo = AprobacionService.ClaveDeDetalle(detalle.Id),
                ValorAnterior = PrecioLista,
                ValorSolicitado = PrecioPedido,
                Activo = true
            };
            contexto.SolicitudesAprobacion.Add(solicitud);
            await contexto.SaveChangesAsync();
            return new Escenario(tecnico.Id, gerencia.Id, producto.Id, orden.Id, detalle.Id, solicitud.Id);
        });
    }

    private static async Task<(Cliente, Vehiculo, Producto)> DatosBaseAsync(ApplicationDbContext contexto)
    {
        var cliente = new Cliente { NombreCompleto = "Rosa Quispe", NumeroDocumento = "40123456", Activo = true };
        var vehiculo = new Vehiculo { ClienteId = cliente.Id, Marca = "Yamaha", Modelo = "FZ 2.0", Placa = "ABC-123", Activo = true };
        var categoria = new CategoriaProducto { Nombre = "Filtros", Activo = true };
        var producto = new Producto
        {
            CategoriaId = categoria.Id, Codigo = "FIL-01", Nombre = "Filtro de aceite",
            PrecioVenta = PrecioLista, Costo = 30m, StockActual = 10, Activo = true
        };
        contexto.AddRange(cliente, vehiculo, categoria, producto);
        await contexto.SaveChangesAsync();
        return (cliente, vehiculo, producto);
    }

    private static DetalleServicio Linea(Guid ordenId, Producto producto, decimal precio)
    {
        var (subtotal, igv, total, porcentaje) = OrdenServicioService.CalcularFinanzasLinea(1, precio, TipoAfectacionIgv.Gravado, 18m);
        return new DetalleServicio
        {
            OrdenServicioId = ordenId, TipoItem = TipoItemServicio.Repuesto, ProductoId = producto.Id,
            Descripcion = producto.Nombre, Cantidad = 1, PrecioUnitario = precio, CostoUnitarioHistorico = producto.Costo,
            TipoAfectacionIgv = TipoAfectacionIgv.Gravado, SubtotalGravado = subtotal, MontoIgv = igv, Total = total,
            PorcentajeIgvAplicado = porcentaje, Activo = true
        };
    }

    private Task<ServiceResult<Features.Aprobaciones.SolicitudAprobacionResponse>> ResolverAsync(
        Escenario escenario, EstadoAprobacionGerencia estado, Guid? solicitudId = null) =>
        _identity.ConAsync(sp => sp.GetRequiredService<AprobacionService>().ResolverSolicitudAsync(
            solicitudId ?? escenario.SolicitudId,
            new Features.Aprobaciones.ResolverSolicitudAprobacionRequest(estado, "Revisado"),
            escenario.GerenciaId));

    private Task<OrdenServicio> LeerOrdenAsync(Guid id) =>
        _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().OrdenesServicio
            .AsNoTracking().Include(o => o.Detalles).SingleAsync(o => o.Id == id));

    /// <summary>Los avisos de aprobaciones: el técnico también recibe el de la orden asignada.</summary>
    private Task<List<Notificacion>> AvisosDeAsync(Guid usuarioId) =>
        _identity.ConAsync(sp => sp.GetRequiredService<ApplicationDbContext>().Notificaciones
            .AsNoTracking().Where(n => n.UsuarioId == usuarioId && n.Tipo == TipoNotificacion.SolicitudAprobacion).ToListAsync());

    [Fact]
    public async Task Al_rechazar_el_repuesto_vuelve_al_precio_de_lista_y_la_orden_queda_libre()
    {
        var escenario = await OrdenConPrecioPedidoAsync();

        var resultado = await ResolverAsync(escenario, EstadoAprobacionGerencia.Rechazado);

        Assert.True(resultado.IsSuccess, resultado.Error);
        var orden = await LeerOrdenAsync(escenario.OrdenId);
        var detalle = Assert.Single(orden.Detalles);
        Assert.Equal(PrecioLista, detalle.PrecioUnitario);
        Assert.Equal(59m, detalle.Total);
        Assert.Equal(59m, orden.Total);
        Assert.Equal(EstadoAprobacionGerencia.NoAplica, orden.EstadoAprobacionGerencia);
        // La solicitud sigue en el historial de Gerencia, rechazada.
        var historial = await _identity.ConAsync(sp => sp.GetRequiredService<AprobacionService>().ObtenerHistorialAsync());
        Assert.Equal("Rechazado", Assert.Single(historial).Estado);
    }

    [Fact]
    public async Task Quien_pidio_el_precio_recibe_el_aviso_del_rechazo_y_lleva_a_la_orden()
    {
        var escenario = await OrdenConPrecioPedidoAsync();

        await ResolverAsync(escenario, EstadoAprobacionGerencia.Rechazado);

        var aviso = Assert.Single(await AvisosDeAsync(escenario.TecnicoId));
        Assert.Equal("Precio rechazado", aviso.Titulo);
        Assert.Equal("OS-000001: vuelve al precio de lista · Revisado", aviso.Mensaje);
        Assert.Equal("OrdenServicio", aviso.Entidad);
        Assert.Equal(escenario.OrdenId, aviso.EntidadId);
    }

    [Fact]
    public async Task Al_aprobar_el_precio_pedido_se_mantiene_y_se_avisa_a_quien_lo_pidio()
    {
        var escenario = await OrdenConPrecioPedidoAsync();

        await ResolverAsync(escenario, EstadoAprobacionGerencia.Aprobado);

        var orden = await LeerOrdenAsync(escenario.OrdenId);
        Assert.Equal(PrecioPedido, Assert.Single(orden.Detalles).PrecioUnitario);
        Assert.Equal(EstadoAprobacionGerencia.Aprobado, orden.EstadoAprobacionGerencia);
        Assert.Equal("Precio aprobado", Assert.Single(await AvisosDeAsync(escenario.TecnicoId)).Titulo);
    }

    [Fact]
    public async Task Al_rechazar_el_precio_de_una_venta_vuelve_al_de_lista_sin_mover_el_stock()
    {
        await _identity.SembrarRolesAsync();
        var vendedor = await _identity.CrearUsuarioAsync("vendedor@teambenavides.pe", RolesDefinidos.Vendedor);
        var gerencia = await _identity.CrearUsuarioAsync("gerencia@teambenavides.pe", RolesDefinidos.GerenciaAdmin);
        var (ventaId, solicitudId, productoId) = await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var (cliente, _, producto) = await DatosBaseAsync(contexto);
            var venta = new Venta
            {
                ClienteId = cliente.Id, Estado = EstadoVenta.Confirmada,
                EstadoAprobacionGerencia = EstadoAprobacionGerencia.Pendiente, Activo = true
            };
            venta.Detalles.Add(new DetalleVenta
            {
                VentaId = venta.Id, ProductoId = producto.Id, TipoItem = TipoItemServicio.Repuesto, Cantidad = 2,
                PrecioUnitario = PrecioPedido, CostoUnitarioHistorico = producto.Costo, TipoAfectacionIgv = TipoAfectacionIgv.Gravado,
                SubtotalGravado = 2m, PorcentajeIgvAplicado = 18m, MontoIgv = 0.36m, Total = 2.36m, Activo = true
            });
            venta.SubtotalGravado = 2m;
            venta.MontoIgv = 0.36m;
            venta.Total = 2.36m;
            var solicitud = new SolicitudAprobacion
            {
                Tipo = "CambioPrecio", Entidad = "Venta", EntidadId = venta.Id.ToString(), UsuarioSolicitanteId = vendedor.Id,
                Estado = EstadoAprobacionGerencia.Pendiente, DetalleCambio = "Modificación de precio en venta",
                ClaveObjetivo = AprobacionService.ClaveEntidadVenta, Activo = true
            };
            contexto.AddRange(venta, solicitud);
            await contexto.SaveChangesAsync();
            return (venta.Id, solicitud.Id, producto.Id);
        });

        var resultado = await _identity.ConAsync(sp => sp.GetRequiredService<AprobacionService>().ResolverSolicitudAsync(
            solicitudId, new Features.Aprobaciones.ResolverSolicitudAprobacionRequest(EstadoAprobacionGerencia.Rechazado, null), gerencia.Id));

        Assert.True(resultado.IsSuccess, resultado.Error);
        var (venta, stock) = await _identity.ConAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<ApplicationDbContext>();
            var leida = await contexto.Ventas.AsNoTracking().Include(v => v.Detalles).SingleAsync(v => v.Id == ventaId);
            var producto = await contexto.Productos.AsNoTracking().SingleAsync(p => p.Id == productoId);
            return (leida, producto.StockActual);
        });
        Assert.Equal(PrecioLista, Assert.Single(venta.Detalles).PrecioUnitario);
        Assert.Equal(118m, venta.Total);
        Assert.Equal(EstadoAprobacionGerencia.NoAplica, venta.EstadoAprobacionGerencia);
        Assert.Equal(10, stock);
        Assert.Equal("Precio rechazado", Assert.Single(await AvisosDeAsync(vendedor.Id)).Titulo);
    }

    [Fact]
    public async Task El_cliente_no_aprueba_el_presupuesto_mientras_Gerencia_no_resuelva_y_si_despues()
    {
        var escenario = await OrdenConPrecioPedidoAsync();
        var aprobar = new ResponderPresupuestoClienteRequest(EstadoPresupuestoCliente.Aprobado, "por teléfono");

        var conPendiente = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>()
            .ResponderPresupuestoClienteAsync(escenario.OrdenId, aprobar));
        await ResolverAsync(escenario, EstadoAprobacionGerencia.Rechazado);
        var resuelto = await _identity.ConAsync(sp => sp.GetRequiredService<OrdenServicioService>()
            .ResponderPresupuestoClienteAsync(escenario.OrdenId, aprobar));

        Assert.Equal(ServiceResultStatus.ValidationError, conPendiente.Status);
        Assert.Contains("Gerencia", conPendiente.Error);
        Assert.True(resuelto.IsSuccess, resuelto.Error);
        var orden = await LeerOrdenAsync(escenario.OrdenId);
        Assert.Equal(EstadoOrdenServicio.Aprobada, orden.Estado);
        Assert.Equal(EstadoPresupuestoCliente.Aprobado, orden.EstadoPresupuestoCliente);
    }
}
