using System.Globalization;
using System.Text.RegularExpressions;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Domain.Entities;

namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Resultado explícito de una traducción o mapeo determinista.
/// Permite capturar fallos de traducción sin lanzar excepciones no controladas ni ocultar datos con valores arbitrarios.
/// </summary>
/// <typeparam name="T">Tipo del valor resultante.</typeparam>
public readonly record struct ResultadoMapeo<T>(
    bool EsValido,
    T? Valor,
    string? Error)
{
    public static ResultadoMapeo<T> Exitoso(T valor) => new(true, valor, null);
    public static ResultadoMapeo<T> Fallido(string error) => new(false, default, error);
}

/// <summary>
/// Traductores y proyectores deterministas puros entre los modelos de datos del CRM Team Benavides
/// y los catálogos oficiales de la Especificación Técnica de Servicios de Datos de Yamaha Motor del Perú v3.
///
/// Todas las funciones son estáticas, puras, en memoria y sin efectos secundarios sobre las entidades de origen.
/// </summary>
public static partial class YamahaCatalogMapper
{
    [GeneratedRegex(@"^OT-\d+(?:-\d+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoOtValidoRegex();

    [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex SoloDigitosRegex();

    [GeneratedRegex(@"^[A-Za-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AlfanumericoYGuionRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CorreoBasicoRegex();

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EspaciosMultiplesRegex();

    [GeneratedRegex(@"\bOM\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PalabraOmRegex();

    [GeneratedRegex(@"\bATV\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PalabraAtvRegex();

    [GeneratedRegex(@"\bQUAD\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PalabraQuadRegex();

    [GeneratedRegex(@"\bMC\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PalabraMcRegex();

    [GeneratedRegex(@"\bMOTO(S)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PalabraMotoRegex();

    // -------------------------------------------------------------------------
    // 1. Proyección de Correlativo de Orden de Servicio a Orden de Trabajo (D-08)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Proyecta el número de orden visible del CRM (ej. <c>OS-000001</c> o <c>OS-2026-0001</c>)
    /// hacia el formato estricto exigido por Yamaha v3 en la Sección 6.2 (<c>OT-...</c>).
    ///
    /// Regla: No muta la entidad <see cref="OrdenServicio"/>. Si el número no puede proyectarse
    /// a un formato conforme al regex <c>^OT-\d+(?:-\d+)*$</c>, devuelve un fallo explícito.
    /// </summary>
    /// <param name="numeroOrdenOriginal">Cadena con el número original de orden del CRM.</param>
    /// <returns>Resultado con la cadena proyectada para Yamaha o el motivo del rechazo.</returns>
    public static ResultadoMapeo<string> ProyectarNumeroOrden(string? numeroOrdenOriginal)
    {
        if (string.IsNullOrWhiteSpace(numeroOrdenOriginal))
        {
            return ResultadoMapeo<string>.Fallido("El número de orden es nulo o vacío.");
        }

        var limpio = numeroOrdenOriginal.Trim();

        string candidato;
        if (limpio.StartsWith("OS-", StringComparison.OrdinalIgnoreCase))
        {
            candidato = "OT-" + limpio[3..];
        }
        else if (limpio.StartsWith("OT-", StringComparison.OrdinalIgnoreCase))
        {
            candidato = "OT-" + limpio[3..];
        }
        else if (SoloDigitosRegex().IsMatch(limpio))
        {
            candidato = "OT-" + limpio;
        }
        else
        {
            return ResultadoMapeo<string>.Fallido(
                $"El número de orden '{numeroOrdenOriginal}' no inicia con prefijo 'OS-', 'OT-' ni es un correlativo puramente numérico proyectable.");
        }

        if (!FormatoOtValidoRegex().IsMatch(candidato))
        {
            return ResultadoMapeo<string>.Fallido(
                $"El correlativo proyectado '{candidato}' a partir de '{numeroOrdenOriginal}' no cumple con el formato obligatorio de Yamaha v3 (^OT-\\d+(?:-\\d+)*$).");
        }

        return ResultadoMapeo<string>.Exitoso(candidato);
    }

    // -------------------------------------------------------------------------
    // 2. Tipo de Documento de Venta (Sección 4.3)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Traduce el enum <see cref="TipoComprobante"/> del CRM hacia el catálogo de Yamaha v3.
    /// </summary>
    public static ResultadoMapeo<string> TraducirTipoComprobante(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.Factura => ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocumentoVentaFactura),
        TipoComprobante.Boleta => ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocumentoVentaBoleta),
        _ => ResultadoMapeo<string>.Fallido($"El TipoComprobante '{(int)tipo}' ({tipo}) no tiene traducción definida en Yamaha v3.")
    };

    /// <summary>
    /// Traduce una cadena representativa del comprobante (ej. "Boleta", "Factura", "01", "03")
    /// hacia el catálogo de la Sección 4.3 de Yamaha v3.
    /// </summary>
    public static ResultadoMapeo<string> TraducirTipoComprobante(string? tipoStr)
    {
        if (string.IsNullOrWhiteSpace(tipoStr))
        {
            return ResultadoMapeo<string>.Fallido("El tipo de comprobante no puede ser nulo ni vacío.");
        }

        var normalizado = tipoStr.Trim();

        if (string.Equals(normalizado, "Factura", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizado, YamahaV3Catalogos.DocumentoVentaFactura, StringComparison.OrdinalIgnoreCase) ||
            normalizado == "01")
        {
            return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocumentoVentaFactura);
        }

        if (string.Equals(normalizado, "Boleta", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizado, YamahaV3Catalogos.DocumentoVentaBoleta, StringComparison.OrdinalIgnoreCase) ||
            normalizado == "03")
        {
            return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocumentoVentaBoleta);
        }

        if (string.Equals(normalizado, "NotaDeCredito", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizado, "Nota de Credito", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizado, "Nota de Crédito", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizado, YamahaV3Catalogos.DocumentoVentaNotaCredito, StringComparison.OrdinalIgnoreCase) ||
            normalizado == "07")
        {
            return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocumentoVentaNotaCredito);
        }

        if (string.Equals(normalizado, "NotaDeDebito", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizado, "Nota de Debito", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizado, "Nota de Débito", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizado, YamahaV3Catalogos.DocumentoVentaNotaDebito, StringComparison.OrdinalIgnoreCase) ||
            normalizado == "08")
        {
            return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocumentoVentaNotaDebito);
        }

        return ResultadoMapeo<string>.Fallido(
            $"El tipo de comprobante '{tipoStr}' no tiene traducción válida en el catálogo de Yamaha v3.");
    }

    // -------------------------------------------------------------------------
    // 3. Tipo de Documento de Identidad del Cliente (Secciones 4.4 y 6.4)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Traduce el tipo de documento del cliente del CRM hacia los valores permitidos por Yamaha v3:
    /// DNI, CE, RUC o DOC NO DOMICILIADO.
    ///
    /// Aplica la regla de inferencia oficial de las Secciones 6.2 y 6.4 cuando el tipo es Otro o nulo
    /// pero el número cumple con los estándares SUNAT (8 = DNI, 9 = CE, 11 = RUC).
    /// </summary>
    public static ResultadoMapeo<string> TraducirTipoDocumentoCliente(
        TipoDocumentoCliente? tipo,
        string? numeroDocumento = null)
    {
        var numLimpio = numeroDocumento?.Trim();

        if (tipo.HasValue)
        {
            switch (tipo.Value)
            {
                case TipoDocumentoCliente.DNI:
                    return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocClienteDni);

                case TipoDocumentoCliente.RUC:
                    return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocClienteRuc);

                case TipoDocumentoCliente.Otro:
                    if (!string.IsNullOrWhiteSpace(numLimpio))
                    {
                        // En TipoDocumentoCliente.Otro nunca se infiere DNI ni RUC (ej. pasaportes u otros documentos extranjeros de 8 u 11 dígitos).
                        // Si tiene 9 caracteres alfanuméricos corresponde a Carné de Extranjería (CE).
                        if (numLimpio.Length == 9 && numLimpio.All(char.IsLetterOrDigit))
                            return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocClienteCe);

                        // Si no es CE, para documentos extranjeros u otros la Sección 6.4 exige 'DOC NO DOMICILIADO'
                        return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocClienteNoDomiciliado);
                    }

                    return ResultadoMapeo<string>.Fallido(
                        "El tipo de documento es 'Otro' y no se proporcionó número para determinar la clasificación.");

                default:
                    return ResultadoMapeo<string>.Fallido(
                        $"El TipoDocumentoCliente '{(int)tipo.Value}' ({tipo.Value}) no tiene equivalencia.");
            }
        }

        // Si tipo es nulo, inferir según la longitud de numeroDocumento (Sección 6.2)
        if (!string.IsNullOrWhiteSpace(numLimpio))
        {
            if (numLimpio.Length == 8 && numLimpio.All(char.IsAsciiDigit))
                return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocClienteDni);
            if (numLimpio.Length == 11 && numLimpio.All(char.IsAsciiDigit))
                return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocClienteRuc);
            if (numLimpio.Length == 9 && numLimpio.All(char.IsLetterOrDigit))
                return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.DocClienteCe);
        }

        return ResultadoMapeo<string>.Fallido(
            "No se puede determinar el TipoDocumentoCliente sin un tipo explícito o número de documento estándar.");
    }

    // -------------------------------------------------------------------------
    // 4. Tipo de Atención de Taller a Tipo de Servicio Yamaha (Sección 6.3 / D-09)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Traduce el enum <see cref="TipoAtencion"/> del CRM a los códigos de servicio de Yamaha v3.
    /// Códigos admitidos: MP (Mantenimiento Preventivo), R (Reparación), G (Garantía), SG (Servicio Gratuito).
    /// El mantenimiento completo cuenta como preventivo. Con varios tipos se traduce el
    /// principal de la orden (ver <c>TiposAtencionOrden</c>).
    /// </summary>
    public static ResultadoMapeo<string> TraducirTipoAtencion(TipoAtencion tipo) => tipo switch
    {
        TipoAtencion.MantenimientoPreventivo => ResultadoMapeo<string>.Exitoso("MP"),
        TipoAtencion.MantenimientoCompleto => ResultadoMapeo<string>.Exitoso("MP"),
        TipoAtencion.MantenimientoCorrectivo => ResultadoMapeo<string>.Exitoso("R"),
        TipoAtencion.ReclamoGarantia => ResultadoMapeo<string>.Exitoso("G"),
        TipoAtencion.Gratuito => ResultadoMapeo<string>.Exitoso("SG"),
        _ => ResultadoMapeo<string>.Fallido(
            $"El TipoAtencion '{(int)tipo}' ({tipo}) no tiene código equivalente en el catálogo de Yamaha v3 (Sección 6.3).")
    };

    /// <summary>
    /// Obtiene el código de servicio que Yamaha v3 exige para líneas de repuestos (Sección 6.3).
    /// Fijo: <c>-</c>.
    /// </summary>
    public static string ObtenerTipoServicioParaRepuesto() => YamahaV3Catalogos.TipoServicioNoAplica;

    // -------------------------------------------------------------------------
    // 5. Clasificación de Detalle de Servicio en SubLínea de Negocio (Sección 6.2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Clasifica una línea de servicio según su <see cref="TipoItemServicio"/>:
    /// - <see cref="TipoItemServicio.Repuesto"/> -> <c>SP</c> (Spare Parts)
    /// - <see cref="TipoItemServicio.Servicio"/>, <see cref="TipoItemServicio.ManoDeObra"/>, <see cref="TipoItemServicio.Terceros"/> -> <c>STE</c> (Servicio Técnico / Mano de Obra)
    /// </summary>
    public static ResultadoMapeo<string> ClasificarSubLineaServicio(TipoItemServicio tipoItem) => tipoItem switch
    {
        TipoItemServicio.Repuesto => ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaSpareParts),
        TipoItemServicio.Servicio => ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaServicioTecnico),
        TipoItemServicio.ManoDeObra => ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaServicioTecnico),
        TipoItemServicio.Terceros => ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaServicioTecnico),
        _ => ResultadoMapeo<string>.Fallido(
            $"El TipoItemServicio '{(int)tipoItem}' ({tipoItem}) no puede clasificarse en SP ni STE.")
    };

    // -------------------------------------------------------------------------
    // 6. Canal de Venta (Sección 4)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Determina el canal de venta a partir del origen de la transacción en el CRM:
    /// - Si proviene de una orden de servicio (<paramref name="ordenServicioId"/> tiene valor) -> <c>TALLER</c>.
    /// - Si es venta libre de mostrador -> <c>MOSTRADOR</c>.
    /// </summary>
    public static string TraducirCanalVenta(Guid? ordenServicioId) =>
        ordenServicioId.HasValue ? YamahaV3Catalogos.CanalTaller : YamahaV3Catalogos.CanalMostrador;

    // -------------------------------------------------------------------------
    // 7. Tipo de Cliente (Sección 4)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Clasifica el tipo de cliente comercial en Yamaha v3:
    /// - Si cuenta con RUC (11 dígitos) -> <c>CORPORATIVO</c>.
    /// - Consumidor final (DNI / CE) -> <c>CLIENTE FINAL</c>.
    /// </summary>
    public static string TraducirTipoCliente(TipoDocumentoCliente? tipoDocumento, string? numeroDocumento)
    {
        if (tipoDocumento == TipoDocumentoCliente.RUC || (numeroDocumento?.Trim().Length == 11))
        {
            return "CORPORATIVO";
        }

        return "CLIENTE FINAL";
    }

    // -------------------------------------------------------------------------
    // 8. Formateo Invariante de Fechas y Números
    // -------------------------------------------------------------------------

    /// <summary>
    /// Formatea una fecha explícitamente en <c>DD/MM/YYYY</c> con cultura invariante en hora de Perú (UTC-5).
    /// Las marcas de tiempo en UTC o locales del servidor se convierten a la jornada real del concesionario en Perú.
    /// </summary>
    public static string FormatearFecha(DateTime fecha)
    {
        var fechaPeru = fecha.Kind switch
        {
            DateTimeKind.Utc => HoraPeru.DesdeUtc(fecha),
            DateTimeKind.Local => HoraPeru.DesdeUtc(fecha.ToUniversalTime()),
            _ => fecha
        };

        return fechaPeru.ToString(YamahaV3Catalogos.FormatoFecha, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formatea una fecha calendario local (<see cref="DateOnly"/>) en <c>DD/MM/YYYY</c> con cultura invariante.
    /// </summary>
    public static string FormatearFecha(DateOnly fecha) =>
        fecha.ToString(YamahaV3Catalogos.FormatoFecha, CultureInfo.InvariantCulture);

    /// <summary>
    /// Valida el formato de <c>CodigoYamaha</c> conforme a Secciones 4, 5.3, 6.2 y 8:
    /// solo letras, números y guión (-), sin espacios ni símbolos especiales.
    /// </summary>
    public static ResultadoMapeo<string> ValidarCodigoYamaha(string? codigoOriginal, int longitudMaxima = YamahaV3Catalogos.LongitudMaximaVarchar)
    {
        if (string.IsNullOrWhiteSpace(codigoOriginal))
        {
            return ResultadoMapeo<string>.Fallido("El producto no cuenta con código SKU (CodigoYamaha).");
        }

        var limpio = codigoOriginal.Trim();
        if (limpio.Length > longitudMaxima)
        {
            return ResultadoMapeo<string>.Fallido($"El código SKU '{limpio}' excede la longitud máxima de {longitudMaxima} caracteres.");
        }

        if (!AlfanumericoYGuionRegex().IsMatch(limpio))
        {
            return ResultadoMapeo<string>.Fallido(
                $"El código SKU '{limpio}' no cumple el formato Yamaha (solo letras, números y guión '-', sin espacios ni símbolos).");
        }

        return ResultadoMapeo<string>.Exitoso(limpio);
    }

    /// <summary>
    /// Valida el formato de <c>VIN</c> para Ventas y Stock (Secciones 4, 5.3 y 8).
    /// Para MC y ATV exige 15 a 20 caracteres; para OM (motores fuera de borda / acuáticos) exige 1 a 20 caracteres.
    /// </summary>
    public static ResultadoMapeo<string> ValidarVinVehiculo(string? vinOriginal, string lineaNegocio)
    {
        if (string.IsNullOrWhiteSpace(vinOriginal))
        {
            return ResultadoMapeo<string>.Fallido($"El campo VIN/número de serie es obligatorio para la línea '{lineaNegocio}'.");
        }

        var limpio = vinOriginal.Trim();
        if (limpio.Length > YamahaV3Catalogos.LongitudMaximaVin)
        {
            return ResultadoMapeo<string>.Fallido(
                $"El VIN '{limpio}' excede el máximo de {YamahaV3Catalogos.LongitudMaximaVin} caracteres.");
        }

        var requiereMinimo15 = string.Equals(lineaNegocio, YamahaV3Catalogos.LineaMotocicletas, StringComparison.Ordinal)
            || string.Equals(lineaNegocio, YamahaV3Catalogos.LineaCuatrimotos, StringComparison.Ordinal);

        if (requiereMinimo15 && limpio.Length < YamahaV3Catalogos.LongitudMinimaChasis)
        {
            return ResultadoMapeo<string>.Fallido(
                $"El VIN '{limpio}' debe tener entre {YamahaV3Catalogos.LongitudMinimaChasis} y {YamahaV3Catalogos.LongitudMaximaChasis} caracteres para la línea '{lineaNegocio}'.");
        }

        if (!AlfanumericoYGuionRegex().IsMatch(limpio))
        {
            return ResultadoMapeo<string>.Fallido(
                $"El VIN '{limpio}' solo acepta letras, números y guión (-), sin espacios.");
        }

        return ResultadoMapeo<string>.Exitoso(limpio);
    }

    /// <summary>
    /// Sanitiza un celular opcional conforme a Yamaha v3 (9 dígitos sin espacios).
    /// Elimina prefijos +51/51, espacios y guiones; si no resulta en 9 dígitos exactos, retorna vacío con aviso.
    /// </summary>
    public static (string Valor, bool Descartado) SanitizarCelularOpcional(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            return (string.Empty, false);
        }

        var digitos = new string(telefono.Where(char.IsDigit).ToArray());
        if (digitos.Length == 11 && digitos.StartsWith("51", StringComparison.Ordinal))
        {
            digitos = digitos[2..];
        }
        else if (digitos.Length == 13 && digitos.StartsWith("0051", StringComparison.Ordinal))
        {
            digitos = digitos[4..];
        }

        return digitos.Length == 9 && digitos[0] == '9' ? (digitos, false) : (string.Empty, true);
    }

    /// <summary>
    /// Sanitiza un correo opcional; si tiene formato inválido lo omite como vacío sin bloquear el lote.
    /// </summary>
    public static (string Valor, bool Descartado) SanitizarCorreoOpcional(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo))
        {
            return (string.Empty, false);
        }

        var limpio = correo.Trim();
        return CorreoBasicoRegex().IsMatch(limpio) ? (limpio, false) : (string.Empty, true);
    }

    /// <summary>
    /// Sanitiza el campo opcional Color (Sección 4: no puramente numérico ni con espacios dobles).
    /// </summary>
    public static (string Valor, bool Descartado) SanitizarColorOpcional(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return (string.Empty, false);
        }

        var limpio = EspaciosMultiplesRegex().Replace(color.Trim(), " ");
        if (SoloDigitosRegex().IsMatch(limpio))
        {
            return (string.Empty, true);
        }

        return (limpio, false);
    }

    /// <summary>
    /// Proyecta una placa vehicular validando que cumpla el formato estricto de Yamaha v3 (Sección 6.2).
    /// </summary>
    public static ResultadoMapeo<string> ProyectarPlaca(string? placaOriginal)
    {
        if (string.IsNullOrWhiteSpace(placaOriginal))
        {
            return ResultadoMapeo<string>.Fallido("El campo Placa es obligatorio y no puede ser nulo ni vacío.");
        }

        var limpia = placaOriginal.Trim();

        if (limpia.Any(char.IsWhiteSpace) || limpia.Contains("--", StringComparison.Ordinal))
        {
            return ResultadoMapeo<string>.Fallido("La placa no debe contener espacios ni caracteres inválidos como '--'.");
        }

        if (!limpia.Contains('-', StringComparison.Ordinal) || limpia.StartsWith('-') || limpia.EndsWith('-'))
        {
            return ResultadoMapeo<string>.Fallido(
                $"La placa '{limpia}' debe contener caracteres alfanuméricos separados por guión (-) (ej. 1234-5A o ABC-123).");
        }

        var conteoAlfanumericos = limpia.Count(char.IsLetterOrDigit);
        if (conteoAlfanumericos < 4 || conteoAlfanumericos > 10)
        {
            return ResultadoMapeo<string>.Fallido(
                $"La placa '{limpia}' debe tener entre 4 y 10 caracteres alfanuméricos más guión (-).");
        }

        return ResultadoMapeo<string>.Exitoso(limpia);
    }

    /// <summary>
    /// Proyecta el número de chasis (VIN) validando que cumpla con los 15 a 20 caracteres exigidos por Yamaha v3.
    /// </summary>
    public static ResultadoMapeo<string> ProyectarChasis(string? chasisOriginal)
    {
        if (string.IsNullOrWhiteSpace(chasisOriginal))
        {
            return ResultadoMapeo<string>.Fallido("El campo Chasis/VIN es obligatorio en Servicio Técnico.");
        }

        var limpio = chasisOriginal.Trim();

        if (limpio.Any(char.IsWhiteSpace))
        {
            return ResultadoMapeo<string>.Fallido("El número de chasis no debe contener espacios.");
        }

        if (limpio.Length < YamahaV3Catalogos.LongitudMinimaChasis ||
            limpio.Length > YamahaV3Catalogos.LongitudMaximaChasis)
        {
            return ResultadoMapeo<string>.Fallido(
                $"El número de chasis '{limpio}' debe tener entre {YamahaV3Catalogos.LongitudMinimaChasis} y {YamahaV3Catalogos.LongitudMaximaChasis} caracteres.");
        }

        if (!AlfanumericoYGuionRegex().IsMatch(limpio))
        {
            return ResultadoMapeo<string>.Fallido(
                "El número de chasis solo acepta letras, números y guión (-), sin espacios.");
        }

        return ResultadoMapeo<string>.Exitoso(limpio);
    }

    // -------------------------------------------------------------------------
    // 9. Determinación explícita de Línea de Negocio Vehicular: MC / ATV / OM (Sección 3.3)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Clasifica explícitamente la línea de negocio vehicular conforme a la Sección 3.3 de Yamaha v3:
    /// - "MC": Motocicletas
    /// - "ATV": Cuatrimotos
    /// - "OM": Motores fuera de borda y motos acuáticas (Outboard Motors / Marine)
    ///
    /// Reglas de integridad:
    /// 1. Jamás retorna "MV" (código inexistente en Yamaha v3).
    /// 2. No hace fallback silencioso a "MC": si el producto/unidad es Generador, Otro o no tiene
    ///    clasificación reconocible, retorna <see cref="ResultadoMapeo{T}.Fallido"/> para evitar
    ///    envíos erróneos a Yamaha.
    /// 3. Usa coincidencias de palabra completa para evitar falsos positivos (p. ej. "Comercial" != "OM").
    /// </summary>
    public static ResultadoMapeo<string> ClasificarLineaNegocioVehiculo(Producto? producto, Vehiculo? vehiculo)
    {
        // 1. TipoUnidad explícito distinto de Motocicleta en la unidad física/cliente
        if (vehiculo is not null)
        {
            if (vehiculo.TipoUnidad == TipoUnidad.Cuatrimoto)
            {
                return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaCuatrimotos); // "ATV"
            }

            if (vehiculo.TipoUnidad is TipoUnidad.MotoAcuatica or TipoUnidad.MotorFueraDeBorda)
            {
                return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaMotoresFueraDeBorda); // "OM"
            }

            if (vehiculo.TipoUnidad is TipoUnidad.Generador or TipoUnidad.Otro)
            {
                return ResultadoMapeo<string>.Fallido(
                    $"El tipo de unidad '{vehiculo.TipoUnidad}' no corresponde a las líneas vehiculares reportables a Yamaha (MC, ATV, OM).");
            }
        }

        // 2. Categoría maestra del producto (prioritaria sobre TipoUnidad.Motocicleta por compatibilidad con ventas previas)
        if (producto?.Categoria is not null && !string.IsNullOrWhiteSpace(producto.Categoria.Nombre))
        {
            var resCategoria = ClasificarDesdeTextoCategoria(producto.Categoria.Nombre);
            if (resCategoria.HasValue)
            {
                return resCategoria.Value;
            }
        }

        // 3. Nombre y descripción del modelo/producto
        var resTexto = ClasificarDesdeNombreODescripcion(producto?.Nombre, producto?.Descripcion);
        if (resTexto.HasValue)
        {
            return resTexto.Value;
        }

        // 4. TipoUnidad.Motocicleta explícito cuando el producto no contradice la clasificación
        if (vehiculo?.TipoUnidad == TipoUnidad.Motocicleta)
        {
            return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaMotocicletas); // "MC"
        }

        return ResultadoMapeo<string>.Fallido(
            $"No se pudo determinar la línea de negocio Yamaha (MC, ATV u OM) para el producto '{producto?.Codigo ?? "sin código"}' ('{producto?.Nombre ?? "sin nombre"}'). Complete su categoría o clasificación comercial antes de reportar.");
    }

    /// <summary>
    /// Mantiene compatibilidad de firma devolviendo la línea resuelta o cadena vacía si no es clasificable.
    /// Regla taxativa: Jamás retorna "MV", ya que dicho código no existe en Yamaha v3.
    /// </summary>
    public static string DeterminarLineaNegocioVehiculo(Producto? producto, Vehiculo? vehiculo)
    {
        var resultado = ClasificarLineaNegocioVehiculo(producto, vehiculo);
        return resultado.EsValido ? resultado.Valor! : string.Empty;
    }

    /// <summary>
    /// Infiere el <see cref="TipoUnidad"/> del CRM al vender una unidad vehicular desde inventario,
    /// evitando registrar cuatrimotos o productos acuáticos/fuera de borda como <see cref="TipoUnidad.Motocicleta"/>.
    /// </summary>
    public static TipoUnidad InferirTipoUnidadDesdeProducto(Producto? producto)
    {
        var textoCombinado = $"{producto?.Categoria?.Nombre} {producto?.Nombre} {producto?.Descripcion}";
        if (textoCombinado.Contains("Generador", StringComparison.OrdinalIgnoreCase))
        {
            return TipoUnidad.Generador;
        }

        var linea = ClasificarLineaNegocioVehiculo(producto, null);
        if (linea.EsValido)
        {
            if (string.Equals(linea.Valor, YamahaV3Catalogos.LineaCuatrimotos, StringComparison.Ordinal))
            {
                return TipoUnidad.Cuatrimoto;
            }

            if (string.Equals(linea.Valor, YamahaV3Catalogos.LineaMotoresFueraDeBorda, StringComparison.Ordinal))
            {
                return textoCombinado.Contains("Fuera de borda", StringComparison.OrdinalIgnoreCase)
                    || textoCombinado.Contains("Outboard", StringComparison.OrdinalIgnoreCase)
                    || textoCombinado.Contains("Motor marino", StringComparison.OrdinalIgnoreCase)
                    ? TipoUnidad.MotorFueraDeBorda
                    : TipoUnidad.MotoAcuatica;
            }
        }

        return TipoUnidad.Motocicleta;
    }

    private static ResultadoMapeo<string>? ClasificarDesdeTextoCategoria(string categoria)
    {
        if (categoria.Contains("Generador", StringComparison.OrdinalIgnoreCase) ||
            categoria.Contains("Línea de Fuerza", StringComparison.OrdinalIgnoreCase) ||
            categoria.Contains("Linea de Fuerza", StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoMapeo<string>.Fallido(
                $"La categoría '{categoria}' corresponde a línea de fuerza/generadores y no es reportable como MC, ATV u OM.");
        }

        if (EsTextoMarineOm(categoria, permitirAbreviaturaOm: true))
        {
            return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaMotoresFueraDeBorda);
        }

        if (EsTextoAtv(categoria))
        {
            return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaCuatrimotos);
        }

        if (categoria.Contains("Motocicleta", StringComparison.OrdinalIgnoreCase) ||
            PalabraMotoRegex().IsMatch(categoria) ||
            PalabraMcRegex().IsMatch(categoria))
        {
            return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaMotocicletas);
        }

        return null;
    }

    private static ResultadoMapeo<string>? ClasificarDesdeNombreODescripcion(string? nombre, string? descripcion)
    {
        foreach (var texto in new[] { nombre, descripcion })
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                continue;
            }

            if (texto.Contains("Generador", StringComparison.OrdinalIgnoreCase))
            {
                return ResultadoMapeo<string>.Fallido(
                    $"El producto '{texto}' corresponde a un generador y no pertenece a las líneas vehiculares MC, ATV u OM.");
            }

            if (EsTextoMarineOm(texto, permitirAbreviaturaOm: true))
            {
                return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaMotoresFueraDeBorda);
            }

            if (EsTextoAtv(texto))
            {
                return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaCuatrimotos);
            }
        }

        foreach (var texto in new[] { nombre, descripcion })
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                continue;
            }

            if (EsTextoMotocicleta(texto))
            {
                return ResultadoMapeo<string>.Exitoso(YamahaV3Catalogos.LineaMotocicletas);
            }
        }

        return null;
    }

    private static bool EsTextoMarineOm(string texto, bool permitirAbreviaturaOm) =>
        texto.Contains("Acuatica", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Acuática", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Marino", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Nautica", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Náutica", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Fuera de Borda", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Outboard", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("WaveRunner", StringComparison.OrdinalIgnoreCase) ||
        (permitirAbreviaturaOm && PalabraOmRegex().IsMatch(texto));

    private static bool EsTextoAtv(string texto) =>
        texto.Contains("Cuatrimoto", StringComparison.OrdinalIgnoreCase) ||
        PalabraAtvRegex().IsMatch(texto) ||
        PalabraQuadRegex().IsMatch(texto) ||
        texto.Contains("Grizzly", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Kodiak", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Raptor", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Wolverine", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Viking", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("YFZ", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("YFM", StringComparison.OrdinalIgnoreCase);

    private static bool EsTextoMotocicleta(string texto) =>
        texto.Contains("Motocicleta", StringComparison.OrdinalIgnoreCase) ||
        PalabraMotoRegex().IsMatch(texto) ||
        PalabraMcRegex().IsMatch(texto) ||
        texto.Contains("YZF", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("MT-03", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("MT-07", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("MT-09", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("MT-15", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("MTN", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("FZN", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("FZ-S", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("FZS", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("FZ25", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("FZ-25", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("FZ-X", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("FZ15", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("FZ16", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("NMAX", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("GPD155", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("XTZ", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("YB125", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("YBR", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Crypton", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("RayZR", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Cygnus", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("LCG125", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("WR155", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Tenere", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Ténéré", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("XMAX", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("XSR", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Tracer", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("BWS", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Aerox", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Fascino", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("R15", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("R3", StringComparison.OrdinalIgnoreCase);
}
