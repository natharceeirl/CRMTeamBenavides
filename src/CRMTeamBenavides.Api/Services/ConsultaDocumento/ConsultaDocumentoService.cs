using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CRMTeamBenavides.Api.Features.ConsultaDocumento;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.ConsultaDocumento;

/// <summary>
/// Configuración de Decolecta (sección «Decolecta»). El token va en variables de entorno
/// (Decolecta__Token) o en dotnet user-secrets, nunca en el repositorio.
/// </summary>
public sealed class DecolectaSettings
{
    public const string SectionName = "Decolecta";

    public string BaseUrl { get; set; } = "https://api.decolecta.com/";
    public string? Token { get; set; }
    public int TimeoutSeconds { get; set; } = 5;
}

public enum ResultadoConsultaDocumento
{
    Encontrado,
    NoEncontrado,
    /// <summary>Sin token, sin respuesta a tiempo o sin cuota: el nombre se escribe a mano.</summary>
    NoDisponible
}

public sealed record ConsultaDocumentoResultado(
    ResultadoConsultaDocumento Resultado, ConsultaDocumentoResponse? Datos = null, string? Mensaje = null);

public interface IConsultaDocumentoService
{
    /// <summary>Busca un DNI (8 dígitos) en RENIEC o un RUC (11 dígitos) en SUNAT.</summary>
    Task<ConsultaDocumentoResultado> ConsultarAsync(string numero, CancellationToken ct = default);
}

/// <summary>
/// Consulta DNI y RUC con Decolecta. Una caída o el fin de la cuota nunca bloquean el
/// registro: se responde «no disponible» y el nombre se escribe a mano. Cada documento
/// se guarda un día en memoria para no gastar consultas.
/// </summary>
public sealed class DecolectaConsultaDocumentoService : IConsultaDocumentoService
{
    public const string MensajeNoConfigurado = "La consulta de DNI y RUC no está configurada: escribe el nombre a mano.";
    public const string MensajeSinRespuesta = "RENIEC y SUNAT no respondieron a tiempo: escribe el nombre a mano.";
    public const string MensajeSinCuota = "Se acabaron las consultas de DNI y RUC del mes: escribe el nombre a mano.";
    public const string MensajeNoDisponible = "La consulta de DNI y RUC no está disponible ahora: escribe el nombre a mano.";

    private static readonly TextInfo Texto = new CultureInfo("es-PE").TextInfo;

    private readonly HttpClient _http;
    private readonly DecolectaSettings _opciones;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DecolectaConsultaDocumentoService> _log;

    public DecolectaConsultaDocumentoService(
        HttpClient http, IOptions<DecolectaSettings> opciones, IMemoryCache cache, ILogger<DecolectaConsultaDocumentoService> log)
    {
        _http = http;
        _opciones = opciones.Value;
        _cache = cache;
        _log = log;
    }

    public static bool EsDni(string numero) => numero.Length == 8 && numero.All(char.IsAsciiDigit);

    public static bool EsRuc(string numero) => numero.Length == 11 && numero.All(char.IsAsciiDigit);

    public async Task<ConsultaDocumentoResultado> ConsultarAsync(string numero, CancellationToken ct = default)
    {
        var dni = EsDni(numero);
        if (!dni && !EsRuc(numero))
            return new(ResultadoConsultaDocumento.NoEncontrado, Mensaje: "El DNI tiene 8 dígitos y el RUC 11.");

        if (string.IsNullOrWhiteSpace(_opciones.Token))
            return new(ResultadoConsultaDocumento.NoDisponible, Mensaje: MensajeNoConfigurado);

        var clave = $"decolecta:{numero}";
        if (_cache.TryGetValue(clave, out ConsultaDocumentoResultado? guardado) && guardado is not null)
            return guardado;

        var ruta = dni ? $"v1/reniec/dni?numero={numero}" : $"v1/sunat/ruc?numero={numero}";
        using var pedido = new HttpRequestMessage(HttpMethod.Get, ruta);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opciones.Token);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.SendAsync(pedido, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "Decolecta no respondió al consultar un {Tipo}", dni ? "DNI" : "RUC");
            return new(ResultadoConsultaDocumento.NoDisponible, Mensaje: MensajeSinRespuesta);
        }

        using (respuesta)
        {
            var resultado = respuesta.StatusCode switch
            {
                HttpStatusCode.OK => await LeerAsync(respuesta, numero, dni, ct),
                HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity =>
                    new(ResultadoConsultaDocumento.NoEncontrado, Mensaje: dni ? "RENIEC no tiene ese DNI." : "SUNAT no tiene ese RUC."),
                HttpStatusCode.TooManyRequests or HttpStatusCode.PaymentRequired =>
                    new(ResultadoConsultaDocumento.NoDisponible, Mensaje: MensajeSinCuota),
                _ => new ConsultaDocumentoResultado(ResultadoConsultaDocumento.NoDisponible, Mensaje: MensajeNoDisponible),
            };

            if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                _log.LogWarning("Decolecta rechazó el token ({Estado}): revisa Decolecta__Token", (int)respuesta.StatusCode);
            else if (resultado.Resultado == ResultadoConsultaDocumento.NoDisponible)
                _log.LogWarning("Decolecta respondió {Estado} al consultar un {Tipo}", (int)respuesta.StatusCode, dni ? "DNI" : "RUC");

            // Lo encontrado se guarda un día; lo que no existe, una hora. Lo no disponible no se guarda.
            if (resultado.Resultado != ResultadoConsultaDocumento.NoDisponible)
            {
                _cache.Set(clave, resultado,
                    resultado.Resultado == ResultadoConsultaDocumento.Encontrado ? TimeSpan.FromDays(1) : TimeSpan.FromHours(1));
            }

            return resultado;
        }
    }

    private static async Task<ConsultaDocumentoResultado> LeerAsync(HttpResponseMessage respuesta, string numero, bool dni, CancellationToken ct)
    {
        using var json = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var raiz = json.RootElement;

        if (dni)
        {
            var nombres = Leer(raiz, "first_name");
            var paterno = Leer(raiz, "first_last_name");
            var materno = Leer(raiz, "second_last_name");
            if (nombres is null && paterno is null)
                return new(ResultadoConsultaDocumento.NoEncontrado, Mensaje: "RENIEC no tiene ese DNI.");

            var completo = string.Join(' ', new[] { nombres, paterno, materno }.Where(p => !string.IsNullOrWhiteSpace(p)));
            return new(ResultadoConsultaDocumento.Encontrado, new ConsultaDocumentoResponse(
                "DNI", (int)TipoDocumentoCliente.DNI, numero, Titulo(completo),
                Nombres: TituloOpcional(nombres), ApellidoPaterno: TituloOpcional(paterno), ApellidoMaterno: TituloOpcional(materno)));
        }

        var razonSocial = Leer(raiz, "razon_social");
        if (razonSocial is null)
            return new(ResultadoConsultaDocumento.NoEncontrado, Mensaje: "SUNAT no tiene ese RUC.");

        var direccion = string.Join(", ", new[]
            {
                Leer(raiz, "direccion"), Leer(raiz, "distrito"), Leer(raiz, "provincia"), Leer(raiz, "departamento"),
            }
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != "-"));

        return new(ResultadoConsultaDocumento.Encontrado, new ConsultaDocumentoResponse(
            "RUC", (int)TipoDocumentoCliente.RUC, numero, razonSocial,
            RazonSocial: razonSocial,
            Direccion: direccion.Length > 0 ? direccion : null,
            Estado: Leer(raiz, "estado"),
            Condicion: Leer(raiz, "condicion")));
    }

    private static string? Leer(JsonElement raiz, string propiedad) =>
        raiz.ValueKind == JsonValueKind.Object
        && raiz.TryGetProperty(propiedad, out var valor)
        && valor.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(valor.GetString())
            ? System.Text.RegularExpressions.Regex.Replace(valor.GetString()!.Trim(), @"\s+", " ")
            : null;

    /// <summary>«DELGADO HUAMANI» → «Delgado Huamani», como se escriben los clientes.</summary>
    private static string Titulo(string texto) => Texto.ToTitleCase(texto.ToLower(CultureInfo.InvariantCulture));

    private static string? TituloOpcional(string? texto) => texto is null ? null : Titulo(texto);
}
