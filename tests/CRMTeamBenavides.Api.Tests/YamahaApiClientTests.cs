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

        /// <summary>Bytes exactos del cuerpo de cada petición, leídos antes de que el cliente la libere.</summary>
        public List<byte[]> CuerposRecibidos { get; } = new();
        public List<string?> TiposContenidoRecibidos { get; } = new();

        public void EncolarRespuesta(HttpStatusCode status, string cuerpo = "{\"status\":\"ok\"}")
        {
            _respuestas.Enqueue(new HttpResponseMessage(status)
            {
                Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            PeticionesRecibidas.Add(request);
            CuerposRecibidos.Add(request.Content is null
                ? Array.Empty<byte>()
                : await request.Content.ReadAsByteArrayAsync(cancellationToken));
            TiposContenidoRecibidos.Add(request.Content?.Headers.ContentType?.ToString());

            if (_respuestas.Count > 0)
            {
                return _respuestas.Dequeue();
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"ok\"}", Encoding.UTF8, "application/json")
            };
        }
    }

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    private static List<YamahaStockV3Dto> LoteStockDePrueba() => new()
    {
        new() { CodigoYamaha = "90793-AJ412", Descripcion = "Aceite Yamalube 4T – ñandú \"10W-40\"", StockDisponible = 20 },
        new() { CodigoYamaha = "2DP-E4451-00", Descripcion = "Filtro de aire NMAX 😀", StockDisponible = 0 }
    };

    [Fact]
    public async Task EnviarStockAsync_DebeInformarElHashDeLosBytesQueRealmenteEnvio()
    {
        var settings = new YamahaSettings
        {
            Enabled = true,
            StockEndpointUrl = "https://azure.example.com/api/ReceiveRetailStock"
        };

        var handler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = new YamahaApiClient(httpClient, NullLogger<YamahaApiClient>.Instance, Options.Create(settings));

        var resultado = await client.EnviarStockAsync(LoteStockDePrueba());

        var cuerpo = Assert.Single(handler.CuerposRecibidos);
        Assert.Equal(Sha256Hex(cuerpo), resultado.PayloadHashEnviado);
        Assert.Equal("application/json; charset=utf-8", handler.TiposContenidoRecibidos[0]);
        Assert.False(cuerpo.Length >= 3 && cuerpo[0] == 0xEF && cuerpo[1] == 0xBB && cuerpo[2] == 0xBF, "El cuerpo no debe llevar BOM.");
    }

    [Fact]
    public async Task EnviarStockAsync_ConReintento_DebeEnviarElMismoCuerpoEnCadaIntento()
    {
        var settings = new YamahaSettings
        {
            Enabled = true,
            StockEndpointUrl = "https://azure.example.com/api/ReceiveRetailStock"
        };

        var handler = new MockHttpMessageHandler();
        handler.EncolarRespuesta(HttpStatusCode.ServiceUnavailable, "Temporary Azure error");
        handler.EncolarRespuesta(HttpStatusCode.OK, "{\"status\":\"ok\"}");

        using var httpClient = new HttpClient(handler);
        var client = new YamahaApiClient(httpClient, NullLogger<YamahaApiClient>.Instance, Options.Create(settings));

        var resultado = await client.EnviarStockAsync(LoteStockDePrueba());

        Assert.True(resultado.EsExitoso);
        Assert.Equal(2, handler.CuerposRecibidos.Count);
        Assert.Equal(handler.CuerposRecibidos[0], handler.CuerposRecibidos[1]);
        Assert.Equal(Sha256Hex(handler.CuerposRecibidos[1]), resultado.PayloadHashEnviado);
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

    // -------------------------------------------------------------------------
    // Pruebas específicas de construcción robusta de URLs (Yamaha v3)
    // -------------------------------------------------------------------------

    [Fact]
    public void ConstruirUriConClave_UrlSinParametros_AgregaExactamenteUnParametroCode()
    {
        var urlBase = "https://azure.example.com/api/ReceiveRetailVentas";
        var fakeKey = "test-function-key-123";

        var uri = YamahaApiClient.ConstruirUriConClave(urlBase, fakeKey, "ReceiveRetailVentas");

        Assert.Equal("https://azure.example.com/api/ReceiveRetailVentas?code=test-function-key-123", uri.AbsoluteUri);
        Assert.Equal("?code=test-function-key-123", uri.Query);
    }

    [Fact]
    public void ConstruirUriConClave_UrlQueYaContieneCodeVacio_ReemplazaSinDuplicar()
    {
        var urlBase = "https://azure.example.com/api/ReceiveRetailVentas?code=";
        var fakeKey = "test-function-key-123";

        var uri = YamahaApiClient.ConstruirUriConClave(urlBase, fakeKey, "ReceiveRetailVentas");

        Assert.Equal("https://azure.example.com/api/ReceiveRetailVentas?code=test-function-key-123", uri.AbsoluteUri);
        Assert.DoesNotContain("code=&code=", uri.AbsoluteUri);
    }

    [Fact]
    public void ConstruirUriConClave_UrlConCodeValorAnterior_ReemplazaPorClaveConfigurada()
    {
        var urlBase = "https://azure.example.com/api/ReceiveRetailStock?code=valor-anterior-obsoleto";
        var fakeKey = "clave-nueva-456";

        var uri = YamahaApiClient.ConstruirUriConClave(urlBase, fakeKey, "ReceiveRetailStock");

        Assert.Equal("https://azure.example.com/api/ReceiveRetailStock?code=clave-nueva-456", uri.AbsoluteUri);
        Assert.DoesNotContain("valor-anterior-obsoleto", uri.AbsoluteUri);
    }

    [Fact]
    public void ConstruirUriConClave_UrlConOtrosParametros_PreservaParametrosYAgregaCode()
    {
        var urlBase = "https://azure.example.com/api/ReceiveRetailServicioTecnico?version=3&formato=json";
        var fakeKey = "clave-ste-789";

        var uri = YamahaApiClient.ConstruirUriConClave(urlBase, fakeKey, "ReceiveRetailServicioTecnico");

        Assert.Contains("version=3", uri.Query);
        Assert.Contains("formato=json", uri.Query);
        Assert.Contains("code=clave-ste-789", uri.Query);
        Assert.StartsWith("https://azure.example.com/api/ReceiveRetailServicioTecnico?", uri.AbsoluteUri);
    }

    [Fact]
    public void ConstruirUriConClave_UrlConParametrosCodeRepetidos_EliminaTodosLosCodePreviosYDejaUnoSolo()
    {
        var urlBase = "https://azure.example.com/api/ReceiveRetailVentas?code=&code=old1&modulo=ventas&code=old2";
        var fakeKey = "clave-unica-999";

        var uri = YamahaApiClient.ConstruirUriConClave(urlBase, fakeKey, "ReceiveRetailVentas");

        Assert.Contains("modulo=ventas", uri.Query);
        Assert.Contains("code=clave-unica-999", uri.Query);
        Assert.DoesNotContain("old1", uri.AbsoluteUri);
        Assert.DoesNotContain("old2", uri.AbsoluteUri);
        // Validar que sólo hay una aparición de 'code='
        var countCode = uri.Query.Split("code=").Length - 1;
        Assert.Equal(1, countCode);
    }

    [Fact]
    public void ConstruirUriConClave_FunctionKeyConCaracteresEspeciales_CodificaCorrectamenteUrl()
    {
        var urlBase = "https://azure.example.com/api/ReceiveRetailStock";
        var fakeKeyConEspeciales = "fake+key/with=special&chars==";

        var uri = YamahaApiClient.ConstruirUriConClave(urlBase, fakeKeyConEspeciales, "ReceiveRetailStock");

        Assert.Contains("code=fake%2Bkey%2Fwith%3Dspecial%26chars%3D%3D", uri.AbsoluteUri);
        Assert.DoesNotContain("&chars", uri.Query); // No debe desbordar como un nuevo parámetro de consulta
    }

    [Theory]
    [InlineData("https://yamaha-data-api-gwcjcgabesdzc8fw.brazilsouth-01.azurewebsites.net/api/ReceiveRetailVentas?code=")]
    [InlineData("https://yamaha-data-api-gwcjcgabesdzc8fw.brazilsouth-01.azurewebsites.net/api/ReceiveRetailStock?code=")]
    [InlineData("https://yamaha-data-api-gwcjcgabesdzc8fw.brazilsouth-01.azurewebsites.net/api/ReceiveRetailServicioTecnico?code=")]
    public void ConstruirUriConClave_EndpointsOficialesYamaha_GeneranUrisExactasSinDuplicarCode(string endpointOficialConCode)
    {
        var fakeKey = "dummy-testing-key-xyz";

        var uri = YamahaApiClient.ConstruirUriConClave(endpointOficialConCode, fakeKey, "ServicioOficial");

        Assert.True(uri.IsAbsoluteUri);
        Assert.EndsWith("?code=dummy-testing-key-xyz", uri.AbsoluteUri);
        Assert.DoesNotContain("&code=", uri.AbsoluteUri);
        Assert.DoesNotContain("code=&", uri.AbsoluteUri);
    }

    [Fact]
    public async Task EnviarVentasAsync_ConUrlConfiguradaConCodeVacio_EnviaPeticionConUnicoParametroCode()
    {
        var settings = new YamahaSettings
        {
            Enabled = true,
            VentasEndpointUrl = "https://azure.example.com/api/ReceiveRetailVentas?code=",
            VentasFunctionKey = "clave-despacho-123"
        };

        var handler = new MockHttpMessageHandler();
        handler.EncolarRespuesta(System.Net.HttpStatusCode.OK, "{\"status\":\"ok\"}");

        using var httpClient = new HttpClient(handler);
        var client = new YamahaApiClient(
            httpClient,
            NullLogger<YamahaApiClient>.Instance,
            Options.Create(settings));

        var resultado = await client.EnviarVentasAsync(new List<YamahaVentaV3Dto>());

        Assert.True(resultado.EsExitoso);
        var peticion = Assert.Single(handler.PeticionesRecibidas);
        var uriFinal = peticion.RequestUri!.ToString();
        Assert.Equal("https://azure.example.com/api/ReceiveRetailVentas?code=clave-despacho-123", uriFinal);
        Assert.DoesNotContain("&code=", uriFinal);
    }
[Fact]
    public void ConstruirUriConClave_FunctionKeyVacia_ConUrlQueContieneCodeValorAnterior_EliminaCodeSinDejarCredencialResidual()
    {
        var urlBase = "https://azure.example.com/api/ReceiveRetailVentas?code=old-stale-key-12345";
        var fakeKeyVacia = "";

        var uri = YamahaApiClient.ConstruirUriConClave(urlBase, fakeKeyVacia, "ReceiveRetailVentas");

        Assert.Equal("https://azure.example.com/api/ReceiveRetailVentas", uri.AbsoluteUri);
        Assert.Empty(uri.Query);
        Assert.DoesNotContain("old-stale-key-12345", uri.AbsoluteUri);
        Assert.DoesNotContain("code", uri.AbsoluteUri);
    }

    [Fact]
    public void ConstruirUriConClave_FunctionKeyVacia_ConUrlQueContieneOtrosParametrosYCode_PreservaOtrosParametrosYEliminaCode()
    {
        var urlBase = "https://azure.example.com/api/ReceiveRetailVentas?version=3&code=old-key&modulo=ventas";
        string? fakeKeyNula = null;

        var uri = YamahaApiClient.ConstruirUriConClave(urlBase, fakeKeyNula, "ReceiveRetailVentas");

        Assert.Contains("version=3", uri.Query);
        Assert.Contains("modulo=ventas", uri.Query);
        Assert.DoesNotContain("code", uri.Query);
        Assert.DoesNotContain("old-key", uri.AbsoluteUri);
    }

    [Fact]
    public void ConstruirUriConClave_FunctionKeyEspaciosEnBlanco_ConUrlQueContieneCodeVacio_EliminaParametroCodeCompletamente()
    {
        var urlBase = "https://azure.example.com/api/ReceiveRetailVentas?code=";
        var fakeKeyEspacios = "   ";

        var uri = YamahaApiClient.ConstruirUriConClave(urlBase, fakeKeyEspacios, "ReceiveRetailVentas");

        Assert.Equal("https://azure.example.com/api/ReceiveRetailVentas", uri.AbsoluteUri);
        Assert.Empty(uri.Query);
    }
}
