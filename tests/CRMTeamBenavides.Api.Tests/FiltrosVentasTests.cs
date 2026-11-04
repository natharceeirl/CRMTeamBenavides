using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Los filtros del registro de ventas contra PostgreSQL: la búsqueda usa ILIKE y los días
/// son los del taller (UTC-5), dos cosas que la base en memoria no reproduce.
/// </summary>
[Collection("PostgresIntegrationTests")]
[Trait("Category", "Integration")]
public class FiltrosVentasTests : IAsyncLifetime
{
    private readonly PostgresQaFixture _fixture;
    private readonly string _sufijo = Guid.NewGuid().ToString("N")[..6];
    private Cliente _ana = null!;
    private Cliente _beto = null!;
    private Venta _emitida = null!;
    private Venta _anulada = null!;
    private Venta _nocturna = null!;
    private Venta _cotizacion = null!;

    public FiltrosVentasTests(PostgresQaFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.LimpiarDatosDePruebaAsync();
        await using var contexto = _fixture.CreateDbContext();

        _ana = new Cliente { NombreCompleto = $"Ana Filtro {_sufijo}", NumeroDocumento = $"71{_sufijo}" };
        _beto = new Cliente { NombreCompleto = $"Beto Filtro {_sufijo}", NumeroDocumento = $"72{_sufijo}" };
        var mediodia = new DateTime(2026, 11, 4, 17, 0, 0, DateTimeKind.Utc); // 12:00 en Arequipa

        _emitida = new Venta { Cliente = _ana, Estado = EstadoVenta.Confirmada, Fecha = mediodia, Total = 35m };
        _emitida.Comprobante = new Comprobante { Venta = _emitida, Tipo = "Boleta", Serie = "B001", Numero = "000123", Estado = "Emitido" };
        _anulada = new Venta { Cliente = _ana, Estado = EstadoVenta.Confirmada, Fecha = mediodia, Total = 20m };
        _anulada.Comprobante = new Comprobante { Venta = _anulada, Tipo = "Boleta", Serie = "B001", Numero = "000124", Estado = "Anulado" };
        // 01:00 UTC del 04/11 son las 20:00 del 03/11 en Arequipa: es una venta del 03/11.
        _nocturna = new Venta { Cliente = _beto, Estado = EstadoVenta.Confirmada, Fecha = new DateTime(2026, 11, 4, 1, 0, 0, DateTimeKind.Utc), Total = 50m };
        _cotizacion = new Venta { Cliente = _beto, Estado = EstadoVenta.Cotizacion, Fecha = mediodia, Total = 10m };

        contexto.Ventas.AddRange(_emitida, _anulada, _nocturna, _cotizacion);
        await contexto.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.LimpiarDatosDePruebaAsync();
        await using var contexto = _fixture.CreateDbContext();
        await contexto.Clientes.Where(c => c.Id == _ana.Id || c.Id == _beto.Id).ExecuteDeleteAsync();
    }

    private async Task<HashSet<Guid>> FiltrarAsync(FiltrosVentas filtros)
    {
        await using var contexto = _fixture.CreateDbContext();
        var mias = new[] { _emitida.Id, _anulada.Id, _nocturna.Id, _cotizacion.Id };
        var ids = await filtros.Aplicar(contexto.Ventas.Where(v => mias.Contains(v.Id))).Select(v => v.Id).ToListAsync();
        return ids.ToHashSet();
    }

    [Fact]
    public async Task Las_fechas_son_dias_del_taller()
    {
        var del03 = await FiltrarAsync(new FiltrosVentas(FechaDesde: new DateTime(2026, 11, 3), FechaHasta: new DateTime(2026, 11, 3)));
        var del04 = await FiltrarAsync(new FiltrosVentas(FechaDesde: new DateTime(2026, 11, 4), FechaHasta: new DateTime(2026, 11, 4)));

        Assert.Equal([_nocturna.Id], del03);
        Assert.Equal(new HashSet<Guid> { _emitida.Id, _anulada.Id, _cotizacion.Id }, del04);
    }

    [Fact]
    public async Task El_comprobante_anulado_no_vuelve_a_pendiente()
    {
        Assert.Equal([_emitida.Id], await FiltrarAsync(new FiltrosVentas(Comprobante: FiltroComprobanteVenta.Emitido)));
        Assert.Equal([_anulada.Id], await FiltrarAsync(new FiltrosVentas(Comprobante: FiltroComprobanteVenta.Anulado)));
        // Pendiente es la confirmada sin comprobante: la cotización no cuenta.
        Assert.Equal([_nocturna.Id], await FiltrarAsync(new FiltrosVentas(Comprobante: FiltroComprobanteVenta.Pendiente)));
    }

    [Theory]
    [InlineData("ana filtro")]
    [InlineData("71")]
    public async Task Busca_por_cliente_o_documento_sin_importar_mayusculas(string inicio)
    {
        var texto = inicio == "71" ? $"71{_sufijo}" : $"{inicio} {_sufijo}";
        Assert.Equal(new HashSet<Guid> { _emitida.Id, _anulada.Id }, await FiltrarAsync(new FiltrosVentas(Busqueda: texto)));
    }

    [Fact]
    public async Task Busca_por_comprobante_y_por_la_referencia_de_la_venta()
    {
        Assert.Equal([_emitida.Id], await FiltrarAsync(new FiltrosVentas(Busqueda: "B001-000123")));
        var referencia = "#" + _cotizacion.Id.ToString()[..8].ToUpperInvariant();
        Assert.Equal([_cotizacion.Id], await FiltrarAsync(new FiltrosVentas(Busqueda: referencia)));
    }
}
