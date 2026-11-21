using System.Text.RegularExpressions;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

public readonly record struct ResultadoNormalizacionTelefono(
    bool EsValido,
    string? TelefonoE164,
    string? WhatsAppId,
    string? Error);

/// <summary>
/// Normaliza y valida números telefónicos al estándar internacional E.164 (<c>+[código país][número]</c>)
/// y su equivalente <c>wa_id</c> de Meta WhatsApp (solo dígitos, sin signo '+').
/// </summary>
public static partial class WhatsAppPhoneNormalizer
{
    [GeneratedRegex(@"^\+[1-9]\d{7,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164Regex();

    public static ResultadoNormalizacionTelefono NormalizarE164(
        string? telefonoOriginal,
        string defaultCountryCode = "51")
    {
        if (string.IsNullOrWhiteSpace(telefonoOriginal))
        {
            return new ResultadoNormalizacionTelefono(false, null, null, "El número telefónico es obligatorio.");
        }

        var limpio = telefonoOriginal.Trim();
        var tienePrefijoMas = limpio.StartsWith('+');
        var digitos = new string(limpio.Where(char.IsDigit).ToArray());

        if (digitos.Length == 0)
        {
            return new ResultadoNormalizacionTelefono(false, null, null, "El número telefónico no contiene dígitos válidos.");
        }

        // Prefijo internacional "00" (ej. 0051987654321)
        if (!tienePrefijoMas && digitos.StartsWith("00", StringComparison.Ordinal) && digitos.Length > 2)
        {
            digitos = digitos[2..];
            tienePrefijoMas = true;
        }

        var codigoPais = new string((defaultCountryCode ?? "51").Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(codigoPais))
        {
            codigoPais = "51";
        }

        // Celular local peruano de 9 dígitos que empieza con 9 (ej. 987654321 -> +51987654321)
        if (!tienePrefijoMas && codigoPais == "51" && digitos.Length == 9 && digitos[0] == '9')
        {
            digitos = codigoPais + digitos;
        }

        var e164 = "+" + digitos;
        if (!E164Regex().IsMatch(e164))
        {
            return new ResultadoNormalizacionTelefono(
                false,
                null,
                null,
                "El número telefónico no cumple el formato internacional E.164 (8 a 15 dígitos con código de país).");
        }

        return new ResultadoNormalizacionTelefono(true, e164, digitos, null);
    }
}
