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
/// Reglas estrictas y conservadoras:
/// - Rechaza texto arbitrario, extensiones/anexos (<c>ext</c>, <c>anexo</c>, <c>Tel 1:</c>, <c>REF</c>, etc.).
/// - Acepta formatos peruanos válidos (celular 9 dígitos, 09xxxxxxxx, fijos con 0 inicial, +51/0051/51 con longitud peruana exacta).
/// - Normaliza el 0 troncal tras +51 / 0051 / 51 (ej. +51 0987654321 -> +51987654321, +51 (054) 223344 -> +5154223344).
/// - Rechaza DNI/RUC, celulares peruanos incompletos y fijos sin código de acceso 0 o prefijo +51.
/// - Acepta números extranjeros únicamente con prefijo internacional explícito (<c>+</c> o <c>00</c>),
///   salvo el método dedicado <see cref="NormalizarRemitenteMeta"/> para <c>wa_id</c> entrantes de Meta.
/// </summary>
public static partial class WhatsAppPhoneNormalizer
{
    private static readonly char[] SeparadoresMultiplesTelefonos = ['/', ';', ',', '|'];

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164Regex();

    // Perú E.164 sin '+':
    // - Celular: 51 + 9 + 8 dígitos (11 dígitos en total)
    // - Fijo Lima/Callao: 51 + 1 + 7 dígitos (10 dígitos)
    // - Fijo provincias (códigos de área oficiales vigentes): 51 + 2 dígitos + 6 dígitos (10 dígitos)
    [GeneratedRegex(@"^51(?:9\d{8}|1\d{7}|(?:4[1-4]|5[1-46]|6[1-7]|7[2-46]|8[2-4])\d{6})$", RegexOptions.CultureInvariant)]
    private static partial Regex PeruE164SinMasRegex();

    [GeneratedRegex(@"^[1-9]\d{7,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex SoloDigitosInternacionalesRegex();

    public static ResultadoNormalizacionTelefono NormalizarE164(
        string? telefonoOriginal,
        string defaultCountryCode = "51")
    {
        if (string.IsNullOrWhiteSpace(telefonoOriginal))
        {
            return new ResultadoNormalizacionTelefono(false, null, null, "El número telefónico es obligatorio.");
        }

        var limpio = telefonoOriginal.Trim();
        if (!ContieneSoloCaracteresTelefonicosPermitidos(limpio))
        {
            return new ResultadoNormalizacionTelefono(
                false,
                null,
                null,
                "El número telefónico contiene caracteres, texto o anexos no permitidos.");
        }

        var tienePrefijoMas = limpio[0] == '+';
        var digitos = new string(limpio.Where(char.IsAsciiDigit).ToArray());

        if (digitos.Length == 0)
        {
            return new ResultadoNormalizacionTelefono(false, null, null, "El número telefónico no contiene dígitos válidos.");
        }

        var tienePrefijoInternacionalExplicito = tienePrefijoMas;

        // Prefijo internacional "00" (ej. 0051987654321, 001 555 123 4567)
        if (!tienePrefijoMas && digitos.StartsWith("00", StringComparison.Ordinal) && digitos.Length > 2)
        {
            digitos = digitos[2..];
            tienePrefijoInternacionalExplicito = true;
        }

        var codigoPais = new string((defaultCountryCode ?? "51").Where(char.IsAsciiDigit).ToArray());
        if (string.IsNullOrEmpty(codigoPais))
        {
            codigoPais = "51";
        }

        if (tienePrefijoInternacionalExplicito)
        {
            // Para prefijo explícito de Perú (+51 / 0051), normalizar 0 troncal opcional y validar longitud peruana exacta.
            if (digitos.StartsWith("51", StringComparison.Ordinal))
            {
                digitos = QuitarCeroTroncalPeruanoTras51(digitos);
                if (!PeruE164SinMasRegex().IsMatch(digitos))
                {
                    return new ResultadoNormalizacionTelefono(
                        false,
                        null,
                        null,
                        "El número peruano con prefijo +51 no tiene una estructura válida (celular de 9 dígitos o fijo con código de área).");
                }
            }

            var e164Explicito = "+" + digitos;
            if (!E164Regex().IsMatch(e164Explicito))
            {
                return new ResultadoNormalizacionTelefono(
                    false,
                    null,
                    null,
                    "El número telefónico no cumple el formato internacional E.164 (8 a 15 dígitos con código de país).");
            }

            return new ResultadoNormalizacionTelefono(true, e164Explicito, digitos, null);
        }

        // Sin prefijo internacional explícito (+ ni 00):
        // Solo se admiten formatos peruanos reconocibles con certeza para evitar interpretar DNI/RUC o fijos como números extranjeros.
        if (codigoPais == "51")
        {
            string? digitosPeruanos = null;

            // 1. Celular peruano con 0 troncal antepuesto (ej. 0987654321 -> 51987654321)
            if (digitos.Length == 10 && digitos[0] == '0' && digitos[1] == '9')
            {
                digitosPeruanos = "51" + digitos[1..];
            }
            // 2. Celular local peruano de 9 dígitos que empieza con 9 (ej. 987654321 -> 51987654321)
            else if (digitos.Length == 9 && digitos[0] == '9')
            {
                digitosPeruanos = "51" + digitos;
            }
            // 3. Teléfono fijo peruano con 0 inicial (ej. 054223344 -> 5154223344, 012345678 -> 5112345678)
            else if (digitos.Length == 9 && digitos[0] == '0' && digitos[1] != '0' && digitos[1] != '9')
            {
                digitosPeruanos = "51" + digitos[1..];
            }
            // 4. Número peruano escrito con código de país 51 sin '+' (ej. 51987654321, (51) 987-654-321, 51 054 223344)
            else if (digitos.StartsWith("51", StringComparison.Ordinal) && digitos.Length is >= 10 and <= 12)
            {
                digitosPeruanos = QuitarCeroTroncalPeruanoTras51(digitos);
            }

            if (digitosPeruanos is not null && PeruE164SinMasRegex().IsMatch(digitosPeruanos))
            {
                return new ResultadoNormalizacionTelefono(true, "+" + digitosPeruanos, digitosPeruanos, null);
            }
        }

        return new ResultadoNormalizacionTelefono(
            false,
            null,
            null,
            "Formato telefónico no reconocido. Use un formato peruano válido o prefijo internacional explícito (+ o 00).");
    }

    /// <summary>
    /// Normaliza el campo <c>from</c> (<c>wa_id</c>) proveniente de webhooks de Meta WhatsApp Cloud API.
    /// Meta envía siempre dígitos internacionales sin signo '+' (ej. <c>51987654321</c>, <c>56987654321</c>, <c>15551234567</c>).
    /// </summary>
    public static ResultadoNormalizacionTelefono NormalizarRemitenteMeta(
        string? waIdOriginal,
        string defaultCountryCode = "51")
    {
        if (string.IsNullOrWhiteSpace(waIdOriginal))
        {
            return new ResultadoNormalizacionTelefono(false, null, null, "El número de origen de Meta es obligatorio.");
        }

        var limpio = waIdOriginal.Trim();
        if (limpio.StartsWith('+') || limpio.StartsWith("00", StringComparison.Ordinal))
        {
            return NormalizarE164(limpio, defaultCountryCode);
        }

        // Un wa_id de Meta son únicamente dígitos en formato internacional sin '+'.
        if (SoloDigitosInternacionalesRegex().IsMatch(limpio))
        {
            return NormalizarE164("+" + limpio, defaultCountryCode);
        }

        return NormalizarE164(limpio, defaultCountryCode);
    }

    /// <summary>
    /// Extrae todos los números válidos en formato E.164 contenidos en el campo <c>Cliente.Telefono</c>
    /// (por ejemplo cuando el taller registró dos números separados por '/' o ',').
    /// Cada segmento se valida íntegramente para evitar coincidencias parciales peligrosas.
    /// </summary>
    public static IReadOnlyList<string> ExtraerTelefonosNormalizadosE164(
        string? campoTelefono,
        string defaultCountryCode = "51")
    {
        if (string.IsNullOrWhiteSpace(campoTelefono))
        {
            return Array.Empty<string>();
        }

        var segmentos = campoTelefono.Split(
            SeparadoresMultiplesTelefonos,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var resultado = new List<string>(segmentos.Length);
        foreach (var segmento in segmentos)
        {
            var norm = NormalizarE164(segmento, defaultCountryCode);
            if (norm.EsValido && !string.IsNullOrEmpty(norm.TelefonoE164) &&
                !resultado.Contains(norm.TelefonoE164, StringComparer.Ordinal))
            {
                resultado.Add(norm.TelefonoE164);
            }
        }

        return resultado;
    }

    /// <summary>Enmascara un documento de identidad (ej. 45678901 -> ****8901) para respuestas con privacidad reducida.</summary>
    public static string? EnmascararDocumento(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento)) return null;
        var limpio = documento.Trim();
        if (limpio.Length <= 4) return new string('*', limpio.Length);
        return new string('*', limpio.Length - 4) + limpio[^4..];
    }

    /// <summary>
    /// Enmascara un VIN/serie conservando solo una porción mínima al final y garantizando
    /// que jamás se devuelva la serie completa, incluso cuando tiene 6 caracteres o menos.
    /// </summary>
    public static string? EnmascararSerie(string? serie)
    {
        if (string.IsNullOrWhiteSpace(serie)) return null;
        var limpio = serie.Trim();
        if (limpio.Length <= 4) return new string('*', limpio.Length);
        if (limpio.Length <= 6) return new string('*', limpio.Length - 2) + limpio[^2..];
        return new string('*', limpio.Length - 4) + limpio[^4..];
    }

    /// <summary>Enmascara un teléfono (ej. +51987654321 -> *******4321) para evitar exposición innecesaria en búsquedas.</summary>
    public static string? EnmascararTelefono(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return null;
        var limpio = telefono.Trim();
        if (limpio.Length <= 4) return new string('*', limpio.Length);
        return new string('*', limpio.Length - 4) + limpio[^4..];
    }

    private static bool ContieneSoloCaracteresTelefonicosPermitidos(string valor)
    {
        for (int i = 0; i < valor.Length; i++)
        {
            var ch = valor[i];
            if (char.IsAsciiDigit(ch)) continue;
            if (ch == '+' && i == 0) continue;
            if (ch is ' ' or '\t' or '-' or '(' or ')' or '.') continue;
            return false;
        }

        return true;
    }

    private static string QuitarCeroTroncalPeruanoTras51(string digitos)
    {
        // +51 0987654321 (12 dígitos: 51 + 0 + 9xxxxxxxx) -> 51987654321
        if (digitos.Length == 12 && digitos.StartsWith("5109", StringComparison.Ordinal))
        {
            return "51" + digitos[3..];
        }

        // +51 (054) 223344 o +51 (01) 2345678 (11 dígitos: 51 + 0 + [1-8]xxxxxxx) -> 5154223344 / 5112345678
        if (digitos.Length == 11 && digitos.StartsWith("510", StringComparison.Ordinal) && digitos[3] != '0' && digitos[3] != '9')
        {
            return "51" + digitos[3..];
        }

        return digitos;
    }
}
