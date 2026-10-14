using System.Globalization;
using System.Text.RegularExpressions;
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
    /// </summary>
    public static ResultadoMapeo<string> TraducirTipoAtencion(TipoAtencion tipo) => tipo switch
    {
        TipoAtencion.MantenimientoPreventivo => ResultadoMapeo<string>.Exitoso("MP"),
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
    /// Formatea una fecha explícitamente en <c>DD/MM/YYYY</c> con cultura invariante.
    /// </summary>
    public static string FormatearFecha(DateTime fecha) =>
        fecha.ToString(YamahaV3Catalogos.FormatoFecha, CultureInfo.InvariantCulture);

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
}
