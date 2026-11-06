using System.Net;
using System.Text;
using CRMTeamBenavides.Api.Services.ConsultaDocumento;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Completar nombres por DNI (RENIEC) y razón social por RUC (SUNAT) con Decolecta. Una
/// caída o el fin de la cuota nunca bloquean el registro: el nombre se escribe a mano.
/// </summary>
public class ConsultaDocumentoTests
{
    private sealed class DecolectaFalsa(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Pedidos { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            Pedidos.Add(pedido);
            return Task.FromResult(responder(pedido));
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode estado = HttpStatusCode.OK) =>
        new(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (DecolectaConsultaDocumentoService Servicio, DecolectaFalsa Falsa) Crear(
        Func<HttpRequestMessage, HttpResponseMessage> responder, string? token = "token-de-prueba")
    {
        var falsa = new DecolectaFalsa(responder);
        var http = new HttpClient(falsa) { BaseAddress = new Uri("https://api.decolecta.com/") };
        var servicio = new DecolectaConsultaDocumentoService(
            http,
            Options.Create(new DecolectaSettings { Token = token }),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<DecolectaConsultaDocumentoService>.Instance);
        return (servicio, falsa);
    }

    [Fact]
    public async Task Con_DNI_trae_nombres_y_apellidos_como_se_escriben_los_clientes()
    {
        var (servicio, falsa) = Crear(_ => Json("""
            {"first_name":"ROXANA KARINA","first_last_name":"DELGADO","second_last_name":"HUAMANI",
             "full_name":"DELGADO HUAMANI ROXANA KARINA","document_number":"46027897"}
            """));

        var resultado = await servicio.ConsultarAsync("46027897");

        Assert.Equal(ResultadoConsultaDocumento.Encontrado, resultado.Resultado);
        Assert.Equal("Roxana Karina Delgado Huamani", resultado.Datos!.NombreCompleto);
        Assert.Equal(("DNI", 0, "Delgado", "Huamani"), (resultado.Datos.TipoDocumento, resultado.Datos.TipoDocumentoId,
            resultado.Datos.ApellidoPaterno, resultado.Datos.ApellidoMaterno));
        var pedido = falsa.Pedidos.Single();
        Assert.Equal("https://api.decolecta.com/v1/reniec/dni?numero=46027897", pedido.RequestUri!.ToString());
        Assert.Equal("Bearer token-de-prueba", pedido.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Con_RUC_trae_razon_social_direccion_y_estado()
    {
        var (servicio, falsa) = Crear(_ => Json("""
            {"razon_social":"TEAM BENAVIDES S.R.L.","numero_documento":"20601030013","estado":"ACTIVO","condicion":"HABIDO",
             "direccion":"AV.  EJERCITO NRO 101 ","distrito":"YANAHUARA","provincia":"AREQUIPA","departamento":"AREQUIPA"}
            """));

        var resultado = await servicio.ConsultarAsync("20601030013");

        Assert.Equal(ResultadoConsultaDocumento.Encontrado, resultado.Resultado);
        Assert.Equal(("RUC", 1, "TEAM BENAVIDES S.R.L."), (resultado.Datos!.TipoDocumento, resultado.Datos.TipoDocumentoId, resultado.Datos.NombreCompleto));
        Assert.Equal("AV. EJERCITO NRO 101, YANAHUARA, AREQUIPA, AREQUIPA", resultado.Datos.Direccion);
        Assert.Equal(("ACTIVO", "HABIDO"), (resultado.Datos.Estado, resultado.Datos.Condicion));
        Assert.Equal("https://api.decolecta.com/v1/sunat/ruc?numero=20601030013", falsa.Pedidos.Single().RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ResultadoConsultaDocumento.NoEncontrado)]
    [InlineData(HttpStatusCode.UnprocessableEntity, ResultadoConsultaDocumento.NoEncontrado)]
    [InlineData(HttpStatusCode.TooManyRequests, ResultadoConsultaDocumento.NoDisponible)]
    [InlineData(HttpStatusCode.Unauthorized, ResultadoConsultaDocumento.NoDisponible)]
    [InlineData(HttpStatusCode.InternalServerError, ResultadoConsultaDocumento.NoDisponible)]
    public async Task Sin_datos_avisa_y_nunca_bloquea(HttpStatusCode estado, ResultadoConsultaDocumento esperado)
    {
        var (servicio, _) = Crear(_ => Json("""{"error":"x"}""", estado));

        var resultado = await servicio.ConsultarAsync("46027897");

        Assert.Equal(esperado, resultado.Resultado);
        Assert.NotNull(resultado.Mensaje);
    }

    [Fact]
    public async Task Sin_cuota_lo_dice_y_sin_token_no_llama()
    {
        var (sinCuota, _) = Crear(_ => Json("{}", HttpStatusCode.TooManyRequests));
        var (sinToken, falsa) = Crear(_ => Json("{}"), token: null);

        Assert.Equal(DecolectaConsultaDocumentoService.MensajeSinCuota, (await sinCuota.ConsultarAsync("46027897")).Mensaje);
        Assert.Equal(DecolectaConsultaDocumentoService.MensajeNoConfigurado, (await sinToken.ConsultarAsync("46027897")).Mensaje);
        Assert.Empty(falsa.Pedidos);
    }

    [Fact]
    public async Task Si_no_responde_a_tiempo_se_escribe_a_mano()
    {
        var (servicio, _) = Crear(_ => throw new TaskCanceledException("tiempo agotado"));

        var resultado = await servicio.ConsultarAsync("20601030013");

        Assert.Equal(ResultadoConsultaDocumento.NoDisponible, resultado.Resultado);
        Assert.Equal(DecolectaConsultaDocumentoService.MensajeSinRespuesta, resultado.Mensaje);
    }

    [Fact]
    public async Task El_mismo_documento_no_gasta_dos_consultas()
    {
        var (servicio, falsa) = Crear(_ => Json("""{"first_name":"LUIS","first_last_name":"QUISPE","second_last_name":"MAMANI"}"""));

        await servicio.ConsultarAsync("40123456");
        var segunda = await servicio.ConsultarAsync("40123456");

        Assert.Single(falsa.Pedidos);
        Assert.Equal("Luis Quispe Mamani", segunda.Datos!.NombreCompleto);
    }

    [Fact]
    public async Task Un_numero_que_no_es_DNI_ni_RUC_no_se_consulta()
    {
        var (servicio, falsa) = Crear(_ => Json("{}"));

        var resultado = await servicio.ConsultarAsync("1234567");

        Assert.Equal(ResultadoConsultaDocumento.NoEncontrado, resultado.Resultado);
        Assert.Empty(falsa.Pedidos);
    }
}
