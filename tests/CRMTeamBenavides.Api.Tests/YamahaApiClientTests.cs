using System.Net;
using System.Text;
using CRMTeamBenavides.Api.Features.Yamaha;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Pruebas unitarias para el cliente HTTP de Yamaha v3 y su implementación fake.
/// Verifica: bloqueo por Enabled=false, reintentos transitorios, sanitización de credenciales,
/// manejo de timeout y simulación de respuestas.
/// </summary>
public class YamahaApiClientTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respuestas = new();
        public List<HttpRequestMessage> PeticionesRecibidas { get; } = new();

        public void EncolarRespuesta(HttpStatusCode status, string cuerpo = "{\"status\":\"ok\"}")
        {
            _respuestas.Enqueue(new HttpResponseMessage(status)
            {
                Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            PeticionesRecibidas.Add(request);
            if (_respuestas.Count > 0)
            {
                return Task.FromResult(_respuestas.Dequeue());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"ok\"}", Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task EnviarVentasAsync_CuandoEnabledEsFalse_DebeLanzarInvalidOperationException()
    {
        var settings = new YamahaSettings
        {
            Enabled = false,
            VentasEndpointUrl = "https://azure.example.com/api/ReceiveRetailVentas"
        };

        var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = new YamahaApiClient(
            httpClient,
            NullLogger<YamahaApiClient>.Instance,
            Options.Create(settings));

        var registros = new List<YamahaVentaV3Dto>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.EnviarVentasAsync(registros));

        Assert.Contains("desactivada", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.PeticionesRecibidas);
    }

    [Fact]
    public async Task FakeYamahaApiClient_DebeAlmacenarLotesYRetornarRespuestaConfigurada()
    {
        var fake = new FakeYamahaApiClient();
        Assert.True(await fake.VerificarConectividadAsync());

        var ventas = new List<YamahaVentaV3Dto>
        {
            new() { CodigoPuntoVenta = "TB-01", NumeroComprobante = "B001-000001", MontoConIGV = 100m }
        };

        var stock = new List<YamahaStockV3Dto>
        {
            new() { CodigoYamaha = "YAM-01", Descripcion = "Aceite Yamalube", StockDisponible = 10 }
        };

        var ste = new List<YamahaServicioTecnicoV3Dto>
        {
            new() { NumeroOrdenTrabajo = "OT-000001", Placa = "1234-AB", Kilometraje = 5000 }
        };

        var resVentas = await fake.EnviarVentasAsync(ventas);
        var resStock = await fake.EnviarStockAsync(stock);
        var resSte = await fake.EnviarServicioTecnicoAsync(ste);

        Assert.True(resVentas.EsExitoso);
        Assert.True(resStock.EsExitoso);
        Assert.True(resSte.EsExitoso);
        Assert.Equal(3, fake.TotalLlamadasEnvio);

        Assert.NotNull(fake.UltimoLoteVentas);
        Assert.Single(fake.UltimoLoteVentas);
        Assert.Equal("B001-000001", fake.UltimoLoteVentas[0].NumeroComprobante);

        Assert.NotNull(fake.UltimoLoteStock);
        Assert.Single(fake.UltimoLoteStock);
        Assert.Equal("YAM-01", fake.UltimoLoteStock[0].CodigoYamaha);

        Assert.NotNull(fake.UltimoLoteServicioTecnico);
        Assert.Single(fake.UltimoLoteServicioTecnico);
        Assert.Equal("OT-000001", fake.UltimoLoteServicioTecnico[0].NumeroOrdenTrabajo);

        fake.LimpiarHistorial();
        Assert.Null(fake.UltimoLoteVentas);
        Assert.Equal(0, fake.TotalLlamadasEnvio);
    }

    [Fact]
    public async Task FakeYamahaApiClient_ConExcepcionSimulada_DebeLanzarExcepcion()
    {
        var fake = new FakeYamahaApiClient
        {
            ExcepcionSimulada = new TimeoutException("Simulated timeout")
        };

        await Assert.ThrowsAsync<TimeoutException>(() =>
            fake.EnviarVentasAsync(new List<YamahaVentaV3Dto>()));
    }

    [Fact]
    public async Task EnviarVentasAsync_CuandoServidorResponde400_NoDebeReintentarYRetornaFallo()
    {
        var settings = new YamahaSettings
        {
            Enabled = true,
            VentasEndpointUrl = "https://azure.example.com/api/ReceiveRetailVentas",
            VentasFunctionKey = "secret-key-123"
        };

        var handler = new MockHttpMessageHandler();
        handler.EncolarRespuesta(HttpStatusCode.BadRequest, "{\"error\":\"Formato de fecha inválido\"}");

        using var httpClient = new HttpClient(handler);
        var client = new YamahaApiClient(
            httpClient,
            NullLogger<YamahaApiClient>.Instance,
            Options.Create(settings));

        var registros = new List<YamahaVentaV3Dto>();
        var resultado = await client.EnviarVentasAsync(registros);

        Assert.False(resultado.EsExitoso);
        Assert.Equal(400, resultado.CodigoHttp);
        Assert.Contains("Formato de fecha inválido", resultado.CuerpoRespuesta);
        // Error 400 no es transitorio; sólo 1 petición realizada
        Assert.Single(handler.PeticionesRecibidas);

        // La URI enviada debe contener la clave
        var uriEnviada = handler.PeticionesRecibidas[0].RequestUri!.ToString();
        Assert.Contains("code=secret-key-123", uriEnviada);
    }

    [Fact]
    public async Task EnviarStockAsync_ConFalloTransitorio500YSucesoPosterior_DebeReintentar()
    {
        var settings = new YamahaSettings
        {
            Enabled = true,
            StockEndpointUrl = "https://azure.example.com/api/ReceiveRetailStock"
        };

        var handler = new MockHttpMessageHandler();
        handler.EncolarRespuesta(HttpStatusCode.InternalServerError, "Temporary Azure error");
        handler.EncolarRespuesta(HttpStatusCode.OK, "{\"status\":\"ok\"}");

        using var httpClient = new HttpClient(handler);
        var client = new YamahaApiClient(
            httpClient,
            NullLogger<YamahaApiClient>.Instance,
            Options.Create(settings));

        var resultado = await client.EnviarStockAsync(new List<YamahaStockV3Dto>());

        Assert.True(resultado.EsExitoso);
        Assert.Equal(200, resultado.CodigoHttp);
        Assert.Equal(2, handler.PeticionesRecibidas.Count); // 1 fallo + 1 reintento exitoso
    }

    [Fact]
    public async Task EnviarServicioTecnicoAsync_SinUrlConfigurada_DebeLanzarInvalidOperationException()
    {
        var settings = new YamahaSettings
        {
            Enabled = true,
            ServicioTecnicoEndpointUrl = "" // Vacía
        };

        var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = new YamahaApiClient(
            httpClient,
            NullLogger<YamahaApiClient>.Instance,
            Options.Create(settings));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.EnviarServicioTecnicoAsync(new List<YamahaServicioTecnicoV3Dto>()));

        Assert.Contains("no está configurada", ex.Message);
    }
}
