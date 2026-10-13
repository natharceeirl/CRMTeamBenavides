using System.Text.RegularExpressions;

namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Utilidad de seguridad para redactar credenciales de autenticación (<c>Function_Key</c>, tokens, claves API)
/// antes de registrar URLs, mensajes de error o cuerpos de respuesta en <see cref="ILogger"/> o auditoría.
/// </summary>
/// <remarks>
/// La especificación técnica de Yamaha v3 (Sección 7) utiliza Azure Functions con el parámetro
/// de consulta <c>?code={Function_Key}</c>. Registrar un <see cref="Uri"/> sin sanitizar expondría
/// el secreto del concesionario en los archivos de log o consolas de monitoreo.
/// </remarks>
public static partial class YamahaSecurityHelper
{
    public const string MascaraSecreto = "***REDACTED***";

    /// <summary>
    /// Detecta parámetros sensibles en query strings como <c>code=...</c>, <c>key=...</c>,
    /// <c>token=...</c>, <c>sig=...</c>, <c>api_key=...</c>, <c>apikey=...</c> o <c>x-functions-key=...</c>,
    /// tanto con delimitadores planos (<c>?</c>, <c>&amp;</c>, <c>=</c>) como codificados (<c>%3F</c>, <c>%26</c>, <c>%3D</c>).
    /// </summary>
    [GeneratedRegex(@"(?i)(?<=[?&]|%3[fF]|%26)(code|key|token|sig|api_key|apikey|x-functions-key|client_secret)(=|%3[dD])([^&#\s""']+)", RegexOptions.CultureInvariant)]
    private static partial Regex QuerySecretRegex();

    /// <summary>
    /// Sanitiza un <see cref="Uri"/> enmascarando cualquier parámetro sensible en su query string
    /// y cualquier credencial embebida en <c>UserInfo</c>.
    /// </summary>
    /// <param name="uri">URI a sanitizar.</param>
    /// <param name="secretosAdicionales">Lista opcional de secretos literales a redactar si aparecen en la ruta.</param>
    /// <returns>Cadena con la URL sanitizada apta para logs.</returns>
    public static string SanitizarUri(Uri? uri, IEnumerable<string?>? secretosAdicionales = null)
    {
        if (uri is null)
        {
            return "(uri-nula)";
        }

        var textoUri = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;

        if (uri.IsAbsoluteUri && !string.IsNullOrEmpty(uri.UserInfo))
        {
            var builder = new UriBuilder(uri)
            {
                UserName = MascaraSecreto,
                Password = string.Empty
            };
            textoUri = builder.Uri.AbsoluteUri;
        }

        return SanitizarTexto(textoUri, secretosAdicionales);
    }

    /// <summary>
    /// Sanitiza cualquier cadena de texto (URL, cuerpo de error HTTP o mensaje de excepción)
    /// reemplazando parámetros sensibles de query string y cualquier coincidencia literal
    /// con los secretos configurados.
    /// </summary>
    /// <param name="texto">Texto original.</param>
    /// <param name="secretosAdicionales">Lista opcional de secretos configurados en <see cref="YamahaSettings"/>.</param>
    /// <returns>Texto seguro sin credenciales en texto plano.</returns>
    public static string SanitizarTexto(string? texto, IEnumerable<string?>? secretosAdicionales = null)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        // 1. Redactar parámetros sensibles en query strings (?code=..., &code=..., %3Fcode%3D..., etc.)
        var resultado = QuerySecretRegex().Replace(texto, $"$1$2{MascaraSecreto}");

        // 2. Redactar coincidencias literales de secretos configurados (tanto en texto plano como URL-encoded)
        if (secretosAdicionales is not null)
        {
            foreach (var secreto in secretosAdicionales)
            {
                if (string.IsNullOrWhiteSpace(secreto))
                {
                    continue;
                }

                var valorLimpio = secreto.Trim();
                if (valorLimpio.Length < 3)
                {
                    continue;
                }

                resultado = resultado.Replace(valorLimpio, MascaraSecreto, StringComparison.Ordinal);

                var valorUrlEncoded = Uri.EscapeDataString(valorLimpio);
                if (!string.Equals(valorUrlEncoded, valorLimpio, StringComparison.Ordinal))
                {
                    resultado = resultado.Replace(valorUrlEncoded, MascaraSecreto, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        return resultado;
    }
}
