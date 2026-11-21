using System.Security.Cryptography;
using System.Text;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

/// <summary>
/// Primitivas criptográficas de seguridad para el webhook de Meta WhatsApp Cloud API:
/// comparación en tiempo constante de tokens y validación HMAC-SHA256 sobre el cuerpo crudo.
/// </summary>
public static class WhatsAppWebhookSecurity
{
    public const string HeaderFirmaMeta = "X-Hub-Signature-256";
    private const string PrefijoSha256 = "sha256=";

    /// <summary>
    /// Valida el handshake GET de Meta (<c>hub.mode=subscribe</c> y <c>hub.verify_token</c>)
    /// usando comparación en tiempo constante para evitar ataques de temporización.
    /// </summary>
    public static bool ValidarVerificacionGet(
        string? mode,
        string? verifyTokenRecibido,
        string? challenge,
        string? verifyTokenConfigurado)
    {
        if (!string.Equals(mode, "subscribe", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(challenge) || challenge.Length > 256)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(verifyTokenConfigurado) || string.IsNullOrWhiteSpace(verifyTokenRecibido))
        {
            return false;
        }

        var recibidoBytes = Encoding.UTF8.GetBytes(verifyTokenRecibido);
        var esperadoBytes = Encoding.UTF8.GetBytes(verifyTokenConfigurado);
        return recibidoBytes.Length == esperadoBytes.Length
            && CryptographicOperations.FixedTimeEquals(recibidoBytes, esperadoBytes);
    }

    /// <summary>
    /// Valida la firma <c>X-Hub-Signature-256</c> de Meta mediante HMAC-SHA256 calculado
    /// estrictamente sobre los bytes crudos de la petición original (nunca sobre JSON re-serializado).
    /// </summary>
    public static bool ValidarFirmaHmacSha256(
        ReadOnlySpan<byte> rawBody,
        string? headerFirma,
        string? appSecretConfigurado)
    {
        if (string.IsNullOrWhiteSpace(appSecretConfigurado) || string.IsNullOrWhiteSpace(headerFirma))
        {
            return false;
        }

        var headerLimpio = headerFirma.Trim();
        if (!headerLimpio.StartsWith(PrefijoSha256, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hexFirma = headerLimpio[PrefijoSha256.Length..];
        if (hexFirma.Length != 64)
        {
            return false;
        }

        byte[] firmaRecibida;
        try
        {
            firmaRecibida = Convert.FromHexString(hexFirma);
        }
        catch (FormatException)
        {
            return false;
        }

        var claveBytes = Encoding.UTF8.GetBytes(appSecretConfigurado);
        var firmaCalculada = HMACSHA256.HashData(claveBytes, rawBody);

        return CryptographicOperations.FixedTimeEquals(firmaCalculada, firmaRecibida);
    }

    /// <summary>Calcula el encabezado <c>sha256=...</c> sobre bytes crudos (útil para pruebas).</summary>
    public static string CalcularFirmaHmacSha256(ReadOnlySpan<byte> rawBody, string appSecret)
    {
        var claveBytes = Encoding.UTF8.GetBytes(appSecret);
        var hash = HMACSHA256.HashData(claveBytes, rawBody);
        return PrefijoSha256 + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Calcula el hash SHA-256 en hexadecimal minúscula del payload para trazabilidad.</summary>
    public static string CalcularSha256Hex(ReadOnlySpan<byte> data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Elimina cualquier rastro de secretos o tokens antes de guardar o registrar un mensaje de error.</summary>
    public static string SanitizarMensajeError(string? texto, params string?[] secretos)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var resultado = texto.Trim();
        foreach (var secreto in secretos)
        {
            if (!string.IsNullOrWhiteSpace(secreto) && secreto.Length >= 4)
            {
                resultado = resultado.Replace(secreto, "[REDACTED]", StringComparison.Ordinal);
            }
        }

        return resultado.Length > 500 ? resultado[..500] : resultado;
    }
}
