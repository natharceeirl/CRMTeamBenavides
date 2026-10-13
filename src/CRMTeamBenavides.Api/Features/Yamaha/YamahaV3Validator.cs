using System.Globalization;
using System.Text.RegularExpressions;

namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Severidad de un hallazgo de validación local previa al envío a Yamaha.
/// </summary>
public enum SeveridadValidacionYamaha
{
    /// <summary>
    /// Incumplimiento de contrato que causaría el rechazo del registro o lote por el API de Yamaha.
    /// </summary>
    Error = 1,

    /// <summary>
    /// Observación no bloqueante o condición sujeta a confirmación funcional.
    /// </summary>
    Advertencia = 2
}

/// <summary>
/// Representa un hallazgo individual (error bloqueante o advertencia) detectado durante
/// la validación previa al envío de un lote hacia Yamaha v3.
/// </summary>
/// <param name="IndiceRegistro">Índice base cero del registro dentro del lote evaluado.</param>
/// <param name="IdentificadorRegistro">Identificador de negocio no sensible (comprobante, SKU o número de OT).</param>
/// <param name="Campo">Nombre exacto del campo según la especificación v3.</param>
/// <param name="CodigoRegla">Código interno trazable de la regla aplicada.</param>
/// <param name="Severidad">Indica si el hallazgo es bloqueante (<see cref="SeveridadValidacionYamaha.Error"/>) o informativo.</param>
/// <param name="Mensaje">Descripción en español del motivo del rechazo o advertencia.</param>
public sealed record YamahaValidacionHallazgoDto(
    int IndiceRegistro,
    string IdentificadorRegistro,
    string Campo,
    string CodigoRegla,
    SeveridadValidacionYamaha Severidad,
    string Mensaje);

/// <summary>
/// Resultado consolidado de la validación local de un lote para cualquiera de los tres servicios de Yamaha v3.
/// </summary>
public sealed record YamahaResultadoValidacionLoteDto(
    string Servicio,
    int TotalRegistros,
    int RegistrosValidos,
    int RegistrosConErrores,
    int TotalErroresBloqueantes,
    int TotalAdvertencias,
    bool EsLoteValido,
    IReadOnlyList<YamahaValidacionHallazgoDto> Hallazgos);

/// <summary>
/// Motor puro y determinista de validación de contratos de datos para Yamaha Motor del Perú v3.
/// Evalúa las reglas explícitas de las Secciones 3, 4, 5, 6 y 8 sin modificar los objetos de entrada
/// ni realizar llamadas externas.
/// </summary>
public static partial class YamahaV3Validator
{
    [GeneratedRegex(@"^\d{11}$", RegexOptions.CultureInvariant)]
    private static partial Regex Ruc11DigitosRegex();

    [GeneratedRegex(@"^\d{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex Dni8DigitosRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex Ce9CaracteresRegex();

    [GeneratedRegex(@"^\d{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex Celular9DigitosRegex();

    [GeneratedRegex(@"^[A-Za-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AlfanumericoYGuionSinEspaciosRegex();

    [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex SoloNumerosRegex();

    [GeneratedRegex(@"^OT-\d+(?:-\d+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NumeroOrdenTrabajoOtRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailBasicoRegex();

    // -------------------------------------------------------------------------
    // Servicio 1 — Ventas (Sección 4)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Valida un lote de registros del Servicio 1 — Ventas conforme a la Sección 4 y Sección 8.
    /// </summary>
    public static YamahaResultadoValidacionLoteDto ValidarLoteVentas(IReadOnlyList<YamahaVentaV3Dto> registros)
    {
        ArgumentNullException.ThrowIfNull(registros);

        var hallazgos = new List<YamahaValidacionHallazgoDto>();

        for (var i = 0; i < registros.Count; i++)
        {
            var r = registros[i];
            if (r is null)
            {
                AgregarError(hallazgos, i, $"Fila-{i + 1}", "Registro", "YAM-GEN-NULL-001",
                    $"El elemento en la posición {i} del lote de Ventas es nulo.");
                continue;
            }

            var idReg = ObtenerIdentificadorSeguro(r.NumeroComprobante, r.CodigoYamaha, r.VIN, i);

            ValidarCabeceraConcesionario(i, idReg, r.RazonSocial, r.RUC, r.CodigoPuntoVenta, r.NombreComercialPuntoVenta, hallazgos);

            // LineaNegocio (Sección 3.3 y 4)
            if (string.IsNullOrWhiteSpace(r.LineaNegocio))
            {
                AgregarError(hallazgos, i, idReg, "LineaNegocio", "YAM-V-LINEA-001",
                    "El campo LineaNegocio es obligatorio (valores permitidos: MC, ATV, OM, SP).");
            }
            else if (!YamahaV3Catalogos.LineasVentasYStock.Contains(r.LineaNegocio))
            {
                AgregarError(hallazgos, i, idReg, "LineaNegocio", "YAM-V-LINEA-002",
                    $"El valor '{r.LineaNegocio}' no es válido para el Servicio de Ventas. Valores permitidos: MC, ATV, OM, SP.");
            }
            else
            {
                ValidarCondicionalesLineaVentas(i, idReg, r, hallazgos);
            }

            // Color (opcional, no puramente numérico ni con espacios anómalos)
            if (!string.IsNullOrEmpty(r.Color))
            {
                ValidarLongitudVarchar(i, idReg, "Color", r.Color, hallazgos);
                if (SoloNumerosRegex().IsMatch(r.Color.Trim()))
                {
                    AgregarError(hallazgos, i, idReg, "Color", "YAM-V-COLOR-001",
                        "El campo Color no debe ser puramente numérico (Sección 4).");
                }

                if (!string.Equals(r.Color, r.Color.Trim(), StringComparison.Ordinal) ||
                    r.Color.Contains("  ", StringComparison.Ordinal))
                {
                    AgregarError(hallazgos, i, idReg, "Color", "YAM-V-COLOR-002",
                        "El campo Color no debe contener espacios al inicio/final ni espacios múltiples internos (Sección 4).");
                }
            }

            // FechaFacturacion (obligatoria, DD/MM/YYYY)
            ValidarFechaObligatoria(i, idReg, "FechaFacturacion", r.FechaFacturacion, "YAM-V-FECHA-001", hallazgos);

            // NumeroComprobante (obligatorio)
            ValidarObligatorioVarchar(i, idReg, "NumeroComprobante", r.NumeroComprobante, "YAM-V-COMP-001", hallazgos);

            // TipoDocumentoVenta y Regla 4.2 (Notas de Crédito)
            if (string.IsNullOrWhiteSpace(r.TipoDocumentoVenta))
            {
                AgregarError(hallazgos, i, idReg, "TipoDocumentoVenta", "YAM-V-TDOCV-001",
                    "El campo TipoDocumentoVenta es obligatorio.");
            }
            else if (!YamahaV3Catalogos.TiposDocumentoVentaPermitidos.Contains(r.TipoDocumentoVenta))
            {
                AgregarError(hallazgos, i, idReg, "TipoDocumentoVenta", "YAM-V-TDOCV-002",
                    $"El valor '{r.TipoDocumentoVenta}' no es válido en TipoDocumentoVenta. Valores permitidos: FACTURA, BOLETA DE VENTA, NOTA DE CREDITO, NOTA DE DEBITO.");
            }
            else
            {
                ValidarSignosMontosYCantidadVentas(i, idReg, r, hallazgos);
            }

            // Precisión DECIMAL(10,2) y DECIMAL(5,2) en Ventas (Sección 4 y 8)
            ValidarDecimal10_2(i, idReg, "MontoSinIGV", r.MontoSinIGV, hallazgos);
            ValidarDecimal10_2(i, idReg, "MontoConIGV", r.MontoConIGV, hallazgos);
            ValidarDecimal10_2(i, idReg, "PrecioFinal", r.PrecioFinal, hallazgos);
            ValidarEscalaDosDecimales(i, idReg, "DescuentoPorcentaje", r.DescuentoPorcentaje, hallazgos);
            ValidarDecimal10_2(i, idReg, "DescuentoSoles", r.DescuentoSoles, hallazgos);
            ValidarDecimal10_2(i, idReg, "PrecioUnitarioConIGV", r.PrecioUnitarioConIGV, hallazgos);

            // TipoPago y EntidadFinanciera (Sección 4.1)
            ValidarPagoYEntidadFinanciera(i, idReg, r.TipoPago, r.EntidadFinanciera, hallazgos);

            // Canal (opcional: MOSTRADOR / TALLER)
            if (!string.IsNullOrEmpty(r.Canal) && !YamahaV3Catalogos.CanalesVentaPermitidos.Contains(r.Canal))
            {
                AgregarError(hallazgos, i, idReg, "Canal", "YAM-V-CANAL-001",
                    $"El valor '{r.Canal}' no es válido en Canal. Valores permitidos: MOSTRADOR, TALLER.");
            }

            // TipoPuntoVenta (opcional, valor fijo: 3S)
            if (!string.IsNullOrEmpty(r.TipoPuntoVenta) &&
                !string.Equals(r.TipoPuntoVenta, YamahaV3Catalogos.TipoPuntoVentaFijo, StringComparison.Ordinal))
            {
                AgregarError(hallazgos, i, idReg, "TipoPuntoVenta", "YAM-V-TPV-001",
                    "El campo TipoPuntoVenta solo admite el valor fijo '3S'.");
            }

            // Moneda (opcional, VARCHAR(3): PEN / USD)
            if (!string.IsNullOrEmpty(r.Moneda) && !YamahaV3Catalogos.MonedasPermitidas.Contains(r.Moneda))
            {
                AgregarError(hallazgos, i, idReg, "Moneda", "YAM-V-MONEDA-001",
                    $"El valor '{r.Moneda}' no es válido en Moneda. Valores permitidos: PEN, USD.");
            }

            // Descuentos y PrecioUnitarioConIGV
            if (r.DescuentoPorcentaje < 0m || r.DescuentoPorcentaje > 100m)
            {
                AgregarError(hallazgos, i, idReg, "DescuentoPorcentaje", "YAM-V-DESC-001",
                    "El campo DescuentoPorcentaje debe estar entre 0.00 y 100.00.");
            }

            if (r.DescuentoSoles < 0m)
            {
                AgregarError(hallazgos, i, idReg, "DescuentoSoles", "YAM-V-DESC-002",
                    "El campo DescuentoSoles no puede ser negativo.");
            }

            if (r.PrecioUnitarioConIGV < 0m)
            {
                AgregarError(hallazgos, i, idReg, "PrecioUnitarioConIGV", "YAM-V-PRECIO-001",
                    "El campo PrecioUnitarioConIGV no puede ser negativo.");
            }

            // Datos del cliente (Sección 4 y 4.4)
            if (!string.IsNullOrEmpty(r.TipoCliente) && !YamahaV3Catalogos.TiposClientePermitidos.Contains(r.TipoCliente))
            {
                AgregarError(hallazgos, i, idReg, "TipoCliente", "YAM-V-TCLI-001",
                    $"El valor '{r.TipoCliente}' no es válido en TipoCliente. Valores permitidos: CLIENTE FINAL, TALLERISTA, MINORISTA, CORPORATIVO.");
            }

            ValidarLongitudVarchar(i, idReg, "NombreCompletoCliente", r.NombreCompletoCliente, hallazgos);

            ValidarDocumentoCliente(
                i,
                idReg,
                r.TipoDocumentoCliente,
                r.NumeroDocumentoCliente,
                "YAM-V-DOCCLI",
                hallazgos);

            if (!string.IsNullOrEmpty(r.CanalContactoCliente) &&
                !YamahaV3Catalogos.CanalesContactoClientePermitidos.Contains(r.CanalContactoCliente))
            {
                AgregarError(hallazgos, i, idReg, "CanalContactoCliente", "YAM-V-CANALCLI-001",
                    $"El valor '{r.CanalContactoCliente}' no es válido en CanalContactoCliente. Valores permitidos: PRESENCIAL, WEB, TELEFÓNICO.");
            }

            ValidarLongitudVarchar(i, idReg, "DistritoCliente", r.DistritoCliente, hallazgos);
            ValidarLongitudVarchar(i, idReg, "CiudadCliente", r.CiudadCliente, hallazgos);
            ValidarLongitudVarchar(i, idReg, "DepartamentoCliente", r.DepartamentoCliente, hallazgos);
            ValidarLongitudVarchar(i, idReg, "DireccionCliente", r.DireccionCliente, hallazgos);
            ValidarLongitudVarchar(i, idReg, "MotoActualCliente", r.MotoActualCliente, hallazgos);

            ValidarCelularOpcional(i, idReg, "CelularCliente", r.CelularCliente, "YAM-V-CEL-001", hallazgos);
            ValidarCorreoOpcional(i, idReg, "CorreoCliente", r.CorreoCliente, "YAM-V-MAIL-001", hallazgos);
        }

        return ConstruirResultado("Ventas", registros.Count, hallazgos);
    }

    private static void ValidarCondicionalesLineaVentas(
        int indice,
        string idReg,
        YamahaVentaV3Dto r,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        var esUnidadVehicular = r.LineaNegocio is YamahaV3Catalogos.LineaMotocicletas
            or YamahaV3Catalogos.LineaCuatrimotos
            or YamahaV3Catalogos.LineaMotoresFueraDeBorda;

        var esMotoOCuatrimoto = r.LineaNegocio is YamahaV3Catalogos.LineaMotocicletas
            or YamahaV3Catalogos.LineaCuatrimotos;

        var esSpareParts = r.LineaNegocio == YamahaV3Catalogos.LineaSpareParts;

        if (esUnidadVehicular)
        {
            if (string.IsNullOrWhiteSpace(r.VIN))
            {
                AgregarError(hallazgos, indice, idReg, "VIN", "YAM-V-VIN-001",
                    $"El campo VIN es obligatorio para la línea de negocio '{r.LineaNegocio}'.");
            }
            else
            {
                if (r.VIN.Length > YamahaV3Catalogos.LongitudMaximaVin)
                {
                    AgregarError(hallazgos, indice, idReg, "VIN", "YAM-V-VIN-002",
                        $"El campo VIN no debe exceder {YamahaV3Catalogos.LongitudMaximaVin} caracteres.");
                }

                if (!AlfanumericoYGuionSinEspaciosRegex().IsMatch(r.VIN))
                {
                    AgregarError(hallazgos, indice, idReg, "VIN", "YAM-V-VIN-003",
                        "El campo VIN solo acepta letras, números y guión (-), sin espacios.");
                }

                if (!string.IsNullOrEmpty(r.CodigoYamaha) &&
                    string.Equals(r.VIN, r.CodigoYamaha, StringComparison.OrdinalIgnoreCase))
                {
                    AgregarError(hallazgos, indice, idReg, "VIN", "YAM-V-VIN-004",
                        "El campo VIN no puede tener el mismo valor que CodigoYamaha.");
                }
            }

            ValidarObligatorioVarchar(indice, idReg, "Modelo", r.Modelo, "YAM-V-MODELO-001", hallazgos);
        }

        if (esMotoOCuatrimoto)
        {
            if (!r.AnioModelo.HasValue || r.AnioModelo.Value <= 1900)
            {
                AgregarError(hallazgos, indice, idReg, "AnioModelo", "YAM-V-ANIO-001",
                    $"El campo AnioModelo es obligatorio para la línea '{r.LineaNegocio}'.");
            }

            ValidarObligatorioVarchar(indice, idReg, "NumeroMotor", r.NumeroMotor, "YAM-V-MOTOR-001", hallazgos);
        }
        else
        {
            if (r.AnioModelo.HasValue)
            {
                AgregarAdvertencia(hallazgos, indice, idReg, "AnioModelo", "YAM-V-ANIO-002",
                    $"El campo AnioModelo no aplica para la línea '{r.LineaNegocio}'; según Sección 3.4 debe enviarse vacío.");
            }

            if (!string.IsNullOrEmpty(r.NumeroMotor))
            {
                AgregarAdvertencia(hallazgos, indice, idReg, "NumeroMotor", "YAM-V-MOTOR-002",
                    $"El campo NumeroMotor no aplica para la línea '{r.LineaNegocio}'; según Sección 3.4 debe enviarse vacío.");
            }
        }

        if (esSpareParts)
        {
            if (string.IsNullOrWhiteSpace(r.CodigoYamaha))
            {
                AgregarError(hallazgos, indice, idReg, "CodigoYamaha", "YAM-V-SKU-001",
                    "El campo CodigoYamaha es obligatorio cuando LineaNegocio es 'SP'.");
            }
            else
            {
                ValidarLongitudVarchar(indice, idReg, "CodigoYamaha", r.CodigoYamaha, hallazgos);

                if (!AlfanumericoYGuionSinEspaciosRegex().IsMatch(r.CodigoYamaha))
                {
                    AgregarError(hallazgos, indice, idReg, "CodigoYamaha", "YAM-V-SKU-002",
                        "El campo CodigoYamaha solo acepta letras, números y guión (-), sin espacios.");
                }

                if (!string.IsNullOrEmpty(r.VIN) &&
                    string.Equals(r.CodigoYamaha, r.VIN, StringComparison.OrdinalIgnoreCase))
                {
                    AgregarError(hallazgos, indice, idReg, "CodigoYamaha", "YAM-V-SKU-003",
                        "El campo CodigoYamaha no puede ser igual al VIN.");
                }
            }

            ValidarObligatorioVarchar(indice, idReg, "Descripcion", r.Descripcion, "YAM-V-DESC-003", hallazgos);

            if (!string.IsNullOrEmpty(r.VIN))
            {
                AgregarAdvertencia(hallazgos, indice, idReg, "VIN", "YAM-V-VIN-005",
                    "El campo VIN no aplica cuando LineaNegocio es 'SP'; según la Sección 3.4 debe enviarse como cadena vacía.");
            }

            if (!string.IsNullOrEmpty(r.Modelo))
            {
                AgregarAdvertencia(hallazgos, indice, idReg, "Modelo", "YAM-V-MODELO-002",
                    "El campo Modelo no aplica cuando LineaNegocio es 'SP'; se recomienda enviar cadena vacía.");
            }
        }
        else
        {
            // Aun en líneas MC/ATV/OM, si se envían CodigoYamaha o Descripcion deben cumplir formato y longitud VARCHAR(255)
            if (!string.IsNullOrEmpty(r.CodigoYamaha))
            {
                ValidarLongitudVarchar(indice, idReg, "CodigoYamaha", r.CodigoYamaha, hallazgos);
                if (!AlfanumericoYGuionSinEspaciosRegex().IsMatch(r.CodigoYamaha))
                {
                    AgregarError(hallazgos, indice, idReg, "CodigoYamaha", "YAM-V-SKU-002",
                        "El campo CodigoYamaha solo acepta letras, números y guión (-), sin espacios.");
                }
            }

            ValidarLongitudVarchar(indice, idReg, "Descripcion", r.Descripcion, hallazgos);
        }
    }

    private static void ValidarSignosMontosYCantidadVentas(
        int indice,
        string idReg,
        YamahaVentaV3Dto r,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        var esNotaCredito = string.Equals(
            r.TipoDocumentoVenta,
            YamahaV3Catalogos.DocumentoVentaNotaCredito,
            StringComparison.Ordinal);

        if (esNotaCredito)
        {
            // Sección 4.2 — Regla Especial Notas de Crédito: MontoSinIGV, MontoConIGV, PrecioFinal y CantidadVendida negativos.
            if (r.MontoSinIGV >= 0m)
            {
                AgregarError(hallazgos, indice, idReg, "MontoSinIGV", "YAM-V-NC-001",
                    "Cuando TipoDocumentoVenta es 'NOTA DE CREDITO', MontoSinIGV debe enviarse con signo negativo (< 0).");
            }

            if (r.MontoConIGV >= 0m)
            {
                AgregarError(hallazgos, indice, idReg, "MontoConIGV", "YAM-V-NC-002",
                    "Cuando TipoDocumentoVenta es 'NOTA DE CREDITO', MontoConIGV debe enviarse con signo negativo (< 0).");
            }

            if (r.PrecioFinal >= 0m)
            {
                AgregarError(hallazgos, indice, idReg, "PrecioFinal", "YAM-V-NC-003",
                    "Cuando TipoDocumentoVenta es 'NOTA DE CREDITO', PrecioFinal debe enviarse con signo negativo (< 0).");
            }

            if (r.CantidadVendida >= 0)
            {
                AgregarError(hallazgos, indice, idReg, "CantidadVendida", "YAM-V-NC-004",
                    "Cuando TipoDocumentoVenta es 'NOTA DE CREDITO', CantidadVendida debe enviarse con signo negativo (< 0).");
            }
        }
        else
        {
            if (r.CantidadVendida <= 0)
            {
                AgregarError(hallazgos, indice, idReg, "CantidadVendida", "YAM-V-CANT-001",
                    $"Para '{r.TipoDocumentoVenta}', CantidadVendida debe ser un entero mayor a 0.");
            }

            if (r.MontoSinIGV < 0m)
            {
                AgregarError(hallazgos, indice, idReg, "MontoSinIGV", "YAM-V-MONTO-001",
                    $"Para '{r.TipoDocumentoVenta}', MontoSinIGV no puede ser negativo.");
            }

            if (r.MontoConIGV < 0m)
            {
                AgregarError(hallazgos, indice, idReg, "MontoConIGV", "YAM-V-MONTO-002",
                    $"Para '{r.TipoDocumentoVenta}', MontoConIGV no puede ser negativo.");
            }

            if (r.PrecioFinal < 0m)
            {
                AgregarError(hallazgos, indice, idReg, "PrecioFinal", "YAM-V-MONTO-003",
                    $"Para '{r.TipoDocumentoVenta}', PrecioFinal no puede ser negativo.");
            }
        }
    }

    private static void ValidarPagoYEntidadFinanciera(
        int indice,
        string idReg,
        string? tipoPago,
        string? entidadFinanciera,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (string.IsNullOrWhiteSpace(tipoPago))
        {
            AgregarError(hallazgos, indice, idReg, "TipoPago", "YAM-V-PAGO-001",
                "El campo TipoPago es obligatorio (CONTADO / CREDITO).");
            return;
        }

        if (!YamahaV3Catalogos.TiposPagoPermitidos.Contains(tipoPago))
        {
            AgregarError(hallazgos, indice, idReg, "TipoPago", "YAM-V-PAGO-002",
                $"El valor '{tipoPago}' no es válido en TipoPago. Valores permitidos: CONTADO, CREDITO.");
            return;
        }

        ValidarLongitudVarchar(indice, idReg, "EntidadFinanciera", entidadFinanciera, hallazgos);

        if (string.Equals(tipoPago, YamahaV3Catalogos.TipoPagoContado, StringComparison.Ordinal))
        {
            if (!string.Equals(entidadFinanciera, YamahaV3Catalogos.TipoPagoContado, StringComparison.Ordinal))
            {
                AgregarError(hallazgos, indice, idReg, "EntidadFinanciera", "YAM-V-PAGO-003",
                    "Según la Sección 4.1, cuando TipoPago es 'CONTADO', el campo EntidadFinanciera debe enviarse como 'CONTADO'.");
            }
        }
        else if (string.Equals(tipoPago, YamahaV3Catalogos.TipoPagoCredito, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(entidadFinanciera) ||
                string.Equals(entidadFinanciera, YamahaV3Catalogos.TipoPagoContado, StringComparison.Ordinal))
            {
                AgregarError(hallazgos, indice, idReg, "EntidadFinanciera", "YAM-V-PAGO-004",
                    "Según la Sección 4.1, cuando TipoPago es 'CREDITO', debe especificarse la entidad financiera (no puede ser vacía ni 'CONTADO').");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Servicio 2 — Stock (Sección 5)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Valida un lote de registros del Servicio 2 — Stock conforme a la Sección 5 y Sección 8.
    /// </summary>
    public static YamahaResultadoValidacionLoteDto ValidarLoteStock(IReadOnlyList<YamahaStockV3Dto> registros)
    {
        ArgumentNullException.ThrowIfNull(registros);

        var hallazgos = new List<YamahaValidacionHallazgoDto>();

        for (var i = 0; i < registros.Count; i++)
        {
            var r = registros[i];
            if (r is null)
            {
                AgregarError(hallazgos, i, $"Fila-{i + 1}", "Registro", "YAM-GEN-NULL-001",
                    $"El elemento en la posición {i} del lote de Stock es nulo.");
                continue;
            }

            var idReg = ObtenerIdentificadorSeguro(r.CodigoYamaha, r.VIN, null, i);

            ValidarCabeceraConcesionario(i, idReg, r.RazonSocial, r.RUC, r.CodigoPuntoVenta, r.NombreComercialPuntoVenta, hallazgos);

            // LineaNegocio
            if (string.IsNullOrWhiteSpace(r.LineaNegocio))
            {
                AgregarError(hallazgos, i, idReg, "LineaNegocio", "YAM-S-LINEA-001",
                    "El campo LineaNegocio es obligatorio en Stock (MC, ATV, OM, SP).");
            }
            else if (!YamahaV3Catalogos.LineasVentasYStock.Contains(r.LineaNegocio))
            {
                AgregarError(hallazgos, i, idReg, "LineaNegocio", "YAM-S-LINEA-002",
                    $"El valor '{r.LineaNegocio}' no es válido en LineaNegocio para Stock. Valores permitidos: MC, ATV, OM, SP.");
            }
            else
            {
                // Sección 5.2 — Regla de Stock en Cero por Línea de Negocio y VIN condicional
                var esUnidadConVin = r.LineaNegocio is YamahaV3Catalogos.LineaMotocicletas
                    or YamahaV3Catalogos.LineaCuatrimotos
                    or YamahaV3Catalogos.LineaMotoresFueraDeBorda;

                if (esUnidadConVin)
                {
                    if (r.StockDisponible <= 0)
                    {
                        AgregarError(hallazgos, i, idReg, "StockDisponible", "YAM-S-STOCK-001",
                            $"Según la Sección 5.2, para la línea '{r.LineaNegocio}' no se permiten registros con StockDisponible menor o igual a 0.");
                    }

                    if (string.IsNullOrWhiteSpace(r.VIN))
                    {
                        AgregarError(hallazgos, i, idReg, "VIN", "YAM-S-VIN-001",
                            $"El campo VIN es obligatorio en Stock para la línea '{r.LineaNegocio}'.");
                    }
                }
                else if (r.LineaNegocio == YamahaV3Catalogos.LineaSpareParts)
                {
                    if (r.StockDisponible < 0)
                    {
                        AgregarError(hallazgos, i, idReg, "StockDisponible", "YAM-S-STOCK-002",
                            "El campo StockDisponible para repuestos (SP) no puede ser negativo.");
                    }

                    if (!string.IsNullOrEmpty(r.VIN))
                    {
                        AgregarAdvertencia(hallazgos, i, idReg, "VIN", "YAM-S-VIN-002",
                            "El campo VIN no aplica para la línea 'SP' en Stock; debe enviarse como cadena vacía.");
                    }
                }
            }

            // CodigoYamaha (obligatorio en Stock)
            if (string.IsNullOrWhiteSpace(r.CodigoYamaha))
            {
                AgregarError(hallazgos, i, idReg, "CodigoYamaha", "YAM-S-SKU-001",
                    "El campo CodigoYamaha es obligatorio en el Servicio de Stock.");
            }
            else
            {
                ValidarLongitudVarchar(i, idReg, "CodigoYamaha", r.CodigoYamaha, hallazgos);
                if (!AlfanumericoYGuionSinEspaciosRegex().IsMatch(r.CodigoYamaha))
                {
                    AgregarError(hallazgos, i, idReg, "CodigoYamaha", "YAM-S-SKU-002",
                        "El campo CodigoYamaha solo acepta letras, números y guión (-), sin espacios.");
                }
            }

            // Validación de VIN si está presente y restricción VIN != CodigoYamaha
            if (!string.IsNullOrEmpty(r.VIN))
            {
                if (r.VIN.Length > YamahaV3Catalogos.LongitudMaximaVin)
                {
                    AgregarError(hallazgos, i, idReg, "VIN", "YAM-S-VIN-003",
                        $"El campo VIN no debe exceder {YamahaV3Catalogos.LongitudMaximaVin} caracteres.");
                }

                if (!AlfanumericoYGuionSinEspaciosRegex().IsMatch(r.VIN))
                {
                    AgregarError(hallazgos, i, idReg, "VIN", "YAM-S-VIN-004",
                        "El campo VIN solo acepta letras, números y guión (-), sin espacios.");
                }

                if (!string.IsNullOrEmpty(r.CodigoYamaha) &&
                    string.Equals(r.VIN, r.CodigoYamaha, StringComparison.OrdinalIgnoreCase))
                {
                    AgregarError(hallazgos, i, idReg, "VIN", "YAM-S-VIN-005",
                        "El campo VIN no puede tener el mismo valor que CodigoYamaha (Sección 5.3 y 8).");
                }
            }

            // Descripcion (obligatoria)
            ValidarObligatorioVarchar(i, idReg, "Descripcion", r.Descripcion, "YAM-S-DESC-001", hallazgos);

            // Costos y precisión DECIMAL(10,2)
            ValidarDecimal10_2(i, idReg, "CostoVentaSinIGV", r.CostoVentaSinIGV, hallazgos);
            ValidarDecimal10_2(i, idReg, "CostoVentaConIGV", r.CostoVentaConIGV, hallazgos);

            if (r.CostoVentaSinIGV < 0m)
            {
                AgregarError(hallazgos, i, idReg, "CostoVentaSinIGV", "YAM-S-COSTO-001",
                    "El campo CostoVentaSinIGV no puede ser negativo.");
            }

            if (r.CostoVentaConIGV < 0m)
            {
                AgregarError(hallazgos, i, idReg, "CostoVentaConIGV", "YAM-S-COSTO-002",
                    "El campo CostoVentaConIGV no puede ser negativo.");
            }
            else if (r.CostoVentaConIGV < r.CostoVentaSinIGV)
            {
                AgregarError(hallazgos, i, idReg, "CostoVentaConIGV", "YAM-S-COSTO-003",
                    "El campo CostoVentaConIGV no puede ser menor que CostoVentaSinIGV.");
            }

            // FechaIngreso (opcional, DD/MM/YYYY)
            if (!string.IsNullOrEmpty(r.FechaIngreso) && !EsFechaValidaDdMmYyyy(r.FechaIngreso))
            {
                AgregarError(hallazgos, i, idReg, "FechaIngreso", "YAM-S-FECHA-001",
                    "El campo FechaIngreso debe tener el formato exacto DD/MM/YYYY.");
            }
        }

        return ConstruirResultado("Stock", registros.Count, hallazgos);
    }

    // -------------------------------------------------------------------------
    // Servicio 3 — Servicio Técnico (Sección 6)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Valida un lote de registros del Servicio 3 — Servicio Técnico conforme a la Sección 6 y Sección 8,
    /// incluyendo la regla 6.1 de consistencia de los 6 campos de cabecera entre líneas de la misma orden.
    /// </summary>
    public static YamahaResultadoValidacionLoteDto ValidarLoteServicioTecnico(
        IReadOnlyList<YamahaServicioTecnicoV3Dto> registros)
    {
        ArgumentNullException.ThrowIfNull(registros);

        var hallazgos = new List<YamahaValidacionHallazgoDto>();

        for (var i = 0; i < registros.Count; i++)
        {
            var r = registros[i];
            if (r is null)
            {
                AgregarError(hallazgos, i, $"Fila-{i + 1}", "Registro", "YAM-GEN-NULL-001",
                    $"El elemento en la posición {i} del lote de Servicio Técnico es nulo.");
                continue;
            }

            var idReg = ObtenerIdentificadorSeguro(r.NumeroOrdenTrabajo, r.Placa, r.CodigoYamaha, i);

            // LineaNegocio fijo STE
            if (!string.Equals(r.LineaNegocio, YamahaV3Catalogos.LineaServicioTecnico, StringComparison.Ordinal))
            {
                AgregarError(hallazgos, i, idReg, "LineaNegocio", "YAM-ST-LINEA-001",
                    "En Servicio Técnico, el campo LineaNegocio debe tener el valor fijo 'STE'.");
            }

            // SubLineaNegocio (SP / STE)
            if (string.IsNullOrWhiteSpace(r.SubLineaNegocio) ||
                !YamahaV3Catalogos.SubLineasServicioTecnico.Contains(r.SubLineaNegocio))
            {
                AgregarError(hallazgos, i, idReg, "SubLineaNegocio", "YAM-ST-SUBLINEA-001",
                    "El campo SubLineaNegocio es obligatorio y solo admite los valores 'SP' (Repuestos) o 'STE' (Mano de Obra).");
            }

            ValidarCabeceraConcesionario(i, idReg, r.RazonSocial, r.RUC, r.CodigoPuntoVenta, r.NombreComercialPuntoVenta, hallazgos);

            // NumeroOrdenTrabajo (Sección 6.2: Debe iniciar con OT- seguido de números)
            if (string.IsNullOrWhiteSpace(r.NumeroOrdenTrabajo))
            {
                AgregarError(hallazgos, i, idReg, "NumeroOrdenTrabajo", "YAM-ST-OT-001",
                    "El campo NumeroOrdenTrabajo es obligatorio.");
            }
            else
            {
                ValidarLongitudVarchar(i, idReg, "NumeroOrdenTrabajo", r.NumeroOrdenTrabajo, hallazgos);
                if (!NumeroOrdenTrabajoOtRegex().IsMatch(r.NumeroOrdenTrabajo))
                {
                    AgregarError(hallazgos, i, idReg, "NumeroOrdenTrabajo", "YAM-ST-OT-002",
                        $"El valor '{r.NumeroOrdenTrabajo}' es inválido. Según la Sección 6.2, NumeroOrdenTrabajo debe iniciar con 'OT-' seguido de números (ej. OT-2025-001); cualquier otro formato es rechazado.");
                }
            }

            // FechaFacturacion (obligatoria, DD/MM/YYYY)
            ValidarFechaObligatoria(i, idReg, "FechaFacturacion", r.FechaFacturacion, "YAM-ST-FECHA-001", hallazgos);

            // Estado (valor fijo FACTURADO)
            if (!string.Equals(r.Estado, YamahaV3Catalogos.EstadoOrdenFacturado, StringComparison.Ordinal))
            {
                AgregarError(hallazgos, i, idReg, "Estado", "YAM-ST-ESTADO-001",
                    "Según la Sección 6.2, el campo Estado debe tener el valor fijo 'FACTURADO'. No se aceptan órdenes abiertas, pendientes ni cerradas sin facturar.");
            }

            // Cliente (Sección 6.2 y 6.4)
            ValidarDocumentoCliente(
                i,
                idReg,
                r.TipoDocumentoCliente,
                r.NumeroDocumentoCliente,
                "YAM-ST-DOCCLI",
                hallazgos);

            ValidarObligatorioVarchar(i, idReg, "NombreCliente", r.NombreCliente, "YAM-ST-NOMCLI-001", hallazgos);
            ValidarCelularOpcional(i, idReg, "Celular", r.Celular, "YAM-ST-CEL-001", hallazgos);
            ValidarCorreoOpcional(i, idReg, "Correo", r.Correo, "YAM-ST-MAIL-001", hallazgos);

            // Placa (Sección 6.2: sin espacios internos, 4 a 10 caracteres alfanuméricos más guión, sin --)
            ValidarPlacaServicioTecnico(i, idReg, r.Placa, hallazgos);

            // Chasis (Sección 6.2: 15 a 20 caracteres alfanuméricos y guión, sin espacios, != CodigoYamaha)
            ValidarChasisServicioTecnico(i, idReg, r.Chasis, r.CodigoYamaha, hallazgos);

            // Marca (valor fijo YAMAHA)
            if (!string.Equals(r.Marca, YamahaV3Catalogos.MarcaFijaYamaha, StringComparison.Ordinal))
            {
                AgregarError(hallazgos, i, idReg, "Marca", "YAM-ST-MARCA-001",
                    "En Servicio Técnico, el campo Marca es obligatorio y su valor fijo debe ser 'YAMAHA'.");
            }

            ValidarLongitudVarchar(i, idReg, "Modelo", r.Modelo, hallazgos);
            ValidarLongitudVarchar(i, idReg, "Version", r.Version, hallazgos);

            // Kilometraje (INT obligatorio)
            if (r.Kilometraje < 0)
            {
                AgregarError(hallazgos, i, idReg, "Kilometraje", "YAM-ST-KM-001",
                    "El campo Kilometraje no puede ser negativo.");
            }

            // TipoServicio y Actividad (Sección 6.2 y 6.3)
            ValidarTipoServicioYActividad(i, idReg, r.SubLineaNegocio, r.TipoServicio, r.Actividad, hallazgos);

            // Condicionales por SubLineaNegocio (SP vs STE)
            if (string.Equals(r.SubLineaNegocio, YamahaV3Catalogos.LineaSpareParts, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(r.CodigoYamaha))
                {
                    AgregarError(hallazgos, i, idReg, "CodigoYamaha", "YAM-ST-SKU-001",
                        "El campo CodigoYamaha es obligatorio cuando SubLineaNegocio es 'SP'.");
                }
                else
                {
                    if (r.CodigoYamaha.Length > YamahaV3Catalogos.LongitudMaximaCodigoYamahaSte)
                    {
                        AgregarError(hallazgos, i, idReg, "CodigoYamaha", "YAM-ST-SKU-002",
                            $"En Servicio Técnico, CodigoYamaha no debe exceder {YamahaV3Catalogos.LongitudMaximaCodigoYamahaSte} caracteres.");
                    }

                    if (!AlfanumericoYGuionSinEspaciosRegex().IsMatch(r.CodigoYamaha))
                    {
                        AgregarError(hallazgos, i, idReg, "CodigoYamaha", "YAM-ST-SKU-003",
                            "El campo CodigoYamaha solo acepta letras, números y guión (-), sin espacios.");
                    }

                    if (!string.IsNullOrEmpty(r.Chasis) &&
                        string.Equals(r.CodigoYamaha, r.Chasis, StringComparison.OrdinalIgnoreCase))
                    {
                        AgregarError(hallazgos, i, idReg, "CodigoYamaha", "YAM-ST-SKU-004",
                            "El campo CodigoYamaha no puede tener el mismo valor que Chasis.");
                    }
                }

                ValidarObligatorioVarchar(i, idReg, "Descripcion", r.Descripcion, "YAM-ST-DESC-001", hallazgos);

                if (!r.Cantidad.HasValue || r.Cantidad.Value <= 0)
                {
                    AgregarError(hallazgos, i, idReg, "Cantidad", "YAM-ST-CANT-001",
                        "El campo Cantidad es obligatorio y debe ser mayor a 0 cuando SubLineaNegocio es 'SP'.");
                }

                if (r.PrecioUnit.HasValue)
                {
                    ValidarDecimal10_2(i, idReg, "PrecioUnit", r.PrecioUnit.Value, hallazgos);
                    if (r.PrecioUnit.Value < 0m)
                    {
                        AgregarError(hallazgos, i, idReg, "PrecioUnit", "YAM-ST-PRECIO-001",
                            "El campo PrecioUnit no puede ser negativo.");
                    }
                }
            }
            else if (string.Equals(r.SubLineaNegocio, YamahaV3Catalogos.LineaServicioTecnico, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(r.CodigoYamaha))
                {
                    AgregarAdvertencia(hallazgos, i, idReg, "CodigoYamaha", "YAM-ST-SKU-005",
                        "Según la Sección 6.2 y 3.4, el campo CodigoYamaha no aplica para SubLineaNegocio = 'STE' (mano de obra) y debe enviarse vacío.");
                }

                if (!string.IsNullOrEmpty(r.Descripcion))
                {
                    AgregarAdvertencia(hallazgos, i, idReg, "Descripcion", "YAM-ST-DESC-003",
                        "Según la Sección 6.2 y 3.4, el campo Descripcion no aplica para SubLineaNegocio = 'STE' (mano de obra) y debe enviarse vacío.");
                }

                if (r.Cantidad.HasValue)
                {
                    AgregarAdvertencia(hallazgos, i, idReg, "Cantidad", "YAM-ST-CANT-002",
                        "Según la Sección 6.2, el campo Cantidad no aplica para SubLineaNegocio = 'STE' (mano de obra).");
                }

                if (r.PrecioUnit.HasValue)
                {
                    AgregarAdvertencia(hallazgos, i, idReg, "PrecioUnit", "YAM-ST-PRECIO-002",
                        "Según la Sección 6.2, el campo PrecioUnit solo aplica para SubLineaNegocio = 'SP'.");
                }
            }

            // Montos de línea y precisión DECIMAL(10,2) / DECIMAL(5,2)
            ValidarDecimal10_2(i, idReg, "MontoSinIGV", r.MontoSinIGV, hallazgos);
            ValidarDecimal10_2(i, idReg, "MontoConIGV", r.MontoConIGV, hallazgos);
            ValidarEscalaDosDecimales(i, idReg, "Descuento", r.Descuento, hallazgos);
            ValidarDecimal10_2(i, idReg, "TotalConIGV", r.TotalConIGV, hallazgos);

            if (r.MontoSinIGV < 0m)
            {
                AgregarError(hallazgos, i, idReg, "MontoSinIGV", "YAM-ST-MONTO-001",
                    "El campo MontoSinIGV no puede ser negativo.");
            }

            if (r.MontoConIGV < 0m)
            {
                AgregarError(hallazgos, i, idReg, "MontoConIGV", "YAM-ST-MONTO-002",
                    "El campo MontoConIGV no puede ser negativo.");
            }

            if (r.Descuento < 0m || r.Descuento > 100m)
            {
                AgregarError(hallazgos, i, idReg, "Descuento", "YAM-ST-DESC-002",
                    "El campo Descuento debe estar entre 0.00 y 100.00.");
            }

            if (r.TotalConIGV < 0m)
            {
                AgregarError(hallazgos, i, idReg, "TotalConIGV", "YAM-ST-MONTO-003",
                    "El campo TotalConIGV no puede ser negativo.");
            }
        }

        // Sección 6.1 — Consistencia obligatoria de los 6 campos de cabecera por NumeroOrdenTrabajo
        ValidarConsistenciaCabeceraPorOrdenTrabajo(registros, hallazgos);

        return ConstruirResultado("ServicioTecnico", registros.Count, hallazgos);
    }

    private static void ValidarPlacaServicioTecnico(
        int indice,
        string idReg,
        string? placa,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (string.IsNullOrWhiteSpace(placa))
        {
            AgregarError(hallazgos, indice, idReg, "Placa", "YAM-ST-PLACA-001",
                "El campo Placa es obligatorio en Servicio Técnico.");
            return;
        }

        if (placa.Any(char.IsWhiteSpace) || placa.Contains("--", StringComparison.Ordinal))
        {
            AgregarError(hallazgos, indice, idReg, "Placa", "YAM-ST-PLACA-002",
                "El campo Placa no debe contener espacios ni placeholders como '--'.");
            return;
        }

        if (!placa.Contains('-', StringComparison.Ordinal) ||
            placa.StartsWith('-') ||
            placa.EndsWith('-') ||
            !AlfanumericoYGuionSinEspaciosRegex().IsMatch(placa))
        {
            AgregarError(hallazgos, indice, idReg, "Placa", "YAM-ST-PLACA-003",
                "El campo Placa debe contener solo caracteres alfanuméricos y guión (-), sin espacios (ej. ABC-123).");
            return;
        }

        var conteoAlfanumericos = 0;
        foreach (var ch in placa)
        {
            if (char.IsLetterOrDigit(ch))
            {
                conteoAlfanumericos++;
            }
        }

        if (conteoAlfanumericos < 4 || conteoAlfanumericos > 10)
        {
            AgregarError(hallazgos, indice, idReg, "Placa", "YAM-ST-PLACA-004",
                "Según la Sección 6.2, el campo Placa debe tener entre 4 y 10 caracteres alfanuméricos más guión (-).");
        }
    }

    private static void ValidarChasisServicioTecnico(
        int indice,
        string idReg,
        string? chasis,
        string? codigoYamaha,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (string.IsNullOrWhiteSpace(chasis))
        {
            AgregarError(hallazgos, indice, idReg, "Chasis", "YAM-ST-CHASIS-001",
                "El campo Chasis es obligatorio en Servicio Técnico.");
            return;
        }

        if (chasis.Length < YamahaV3Catalogos.LongitudMinimaChasis ||
            chasis.Length > YamahaV3Catalogos.LongitudMaximaChasis)
        {
            AgregarError(hallazgos, indice, idReg, "Chasis", "YAM-ST-CHASIS-002",
                $"Según la Sección 6.2, el campo Chasis debe tener entre {YamahaV3Catalogos.LongitudMinimaChasis} y {YamahaV3Catalogos.LongitudMaximaChasis} caracteres.");
        }

        if (!AlfanumericoYGuionSinEspaciosRegex().IsMatch(chasis))
        {
            AgregarError(hallazgos, indice, idReg, "Chasis", "YAM-ST-CHASIS-003",
                "El campo Chasis solo acepta letras, números y guión (-), sin espacios.");
        }

        if (!string.IsNullOrEmpty(codigoYamaha) &&
            string.Equals(chasis, codigoYamaha, StringComparison.OrdinalIgnoreCase))
        {
            AgregarError(hallazgos, indice, idReg, "Chasis", "YAM-ST-CHASIS-004",
                "El campo Chasis no puede tener el mismo valor que CodigoYamaha.");
        }
    }

    private static void ValidarTipoServicioYActividad(
        int indice,
        string idReg,
        string? subLineaNegocio,
        string? tipoServicio,
        string? actividad,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (string.IsNullOrWhiteSpace(tipoServicio))
        {
            AgregarError(hallazgos, indice, idReg, "TipoServicio", "YAM-ST-TSERV-001",
                "El campo TipoServicio es obligatorio.");
            return;
        }

        if (!YamahaV3Catalogos.TiposServicioPermitidos.Contains(tipoServicio))
        {
            AgregarError(hallazgos, indice, idReg, "TipoServicio", "YAM-ST-TSERV-002",
                $"El valor '{tipoServicio}' no es válido en TipoServicio. Valores permitidos: MP, D, R, MP+R, RC, G, I, L, SG, -.");
            return;
        }

        // Sección 6.3: El valor '-' es válido únicamente para líneas de repuesto (SubLineaNegocio = SP).
        if (string.Equals(tipoServicio, YamahaV3Catalogos.TipoServicioNoAplica, StringComparison.Ordinal) &&
            string.Equals(subLineaNegocio, YamahaV3Catalogos.LineaServicioTecnico, StringComparison.Ordinal))
        {
            AgregarError(hallazgos, indice, idReg, "TipoServicio", "YAM-ST-TSERV-003",
                "Según la Sección 6.3, el valor '-' en TipoServicio es válido únicamente para líneas de repuesto (SubLineaNegocio = 'SP') y no debe usarse en líneas de mano de obra ('STE').");
        }

        if (!string.Equals(tipoServicio, YamahaV3Catalogos.TipoServicioNoAplica, StringComparison.Ordinal))
        {
            ValidarObligatorioVarchar(indice, idReg, "Actividad", actividad, "YAM-ST-ACT-001", hallazgos);
        }
        else
        {
            ValidarLongitudVarchar(indice, idReg, "Actividad", actividad, hallazgos);
        }
    }

    private static void ValidarConsistenciaCabeceraPorOrdenTrabajo(
        IReadOnlyList<YamahaServicioTecnicoV3Dto> registros,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        var primerasLineasPorOrden = new Dictionary<string, (int Indice, YamahaServicioTecnicoV3Dto Registro)>(StringComparer.Ordinal);

        for (var i = 0; i < registros.Count; i++)
        {
            var actual = registros[i];
            if (actual is null || string.IsNullOrWhiteSpace(actual.NumeroOrdenTrabajo))
            {
                continue;
            }

            if (!primerasLineasPorOrden.TryGetValue(actual.NumeroOrdenTrabajo, out var baseOrden))
            {
                primerasLineasPorOrden[actual.NumeroOrdenTrabajo] = (i, actual);
                continue;
            }

            var primero = baseOrden.Registro;
            var idReg = actual.NumeroOrdenTrabajo;

            VerificarCampoCabeceraConsistente(i, idReg, "RUC", primero.RUC, actual.RUC, hallazgos);
            VerificarCampoCabeceraConsistente(i, idReg, "Chasis", primero.Chasis, actual.Chasis, hallazgos);
            VerificarCampoCabeceraConsistente(i, idReg, "Placa", primero.Placa, actual.Placa, hallazgos);
            VerificarCampoCabeceraConsistente(i, idReg, "NombreCliente", primero.NombreCliente, actual.NombreCliente, hallazgos);
            VerificarCampoCabeceraConsistente(i, idReg, "NumeroDocumentoCliente", primero.NumeroDocumentoCliente, actual.NumeroDocumentoCliente, hallazgos);
            VerificarCampoCabeceraConsistente(i, idReg, "FechaFacturacion", primero.FechaFacturacion, actual.FechaFacturacion, hallazgos);
        }
    }

    private static void VerificarCampoCabeceraConsistente(
        int indiceActual,
        string numeroOrdenTrabajo,
        string nombreCampo,
        string? valorBase,
        string? valorActual,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (!string.Equals(valorBase ?? string.Empty, valorActual ?? string.Empty, StringComparison.Ordinal))
        {
            AgregarError(
                hallazgos,
                indiceActual,
                numeroOrdenTrabajo,
                nombreCampo,
                "YAM-ST-CONSISTENCIA-001",
                $"Inconsistencia en la orden '{numeroOrdenTrabajo}': todas las líneas que comparten el mismo NumeroOrdenTrabajo deben tener el mismo valor en '{nombreCampo}' (Sección 6.1).");
        }
    }

    // -------------------------------------------------------------------------
    // Reglas comunes y utilitarios internos
    // -------------------------------------------------------------------------

    private static void ValidarDecimal10_2(
        int indice,
        string idReg,
        string campo,
        decimal valor,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (Math.Abs(valor) > YamahaV3Catalogos.MaximoDecimal10_2)
        {
            AgregarError(
                hallazgos,
                indice,
                idReg,
                campo,
                "YAM-GEN-DEC-10-2",
                $"El campo {campo} excede la precisión máxima permitida para DECIMAL(10,2) (máximo 8 dígitos enteros y 2 decimales).");
        }

        ValidarEscalaDosDecimales(indice, idReg, campo, valor, hallazgos);
    }

    private static void ValidarEscalaDosDecimales(
        int indice,
        string idReg,
        string campo,
        decimal valor,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (decimal.Round(valor, 2) != valor)
        {
            AgregarError(
                hallazgos,
                indice,
                idReg,
                campo,
                "YAM-GEN-DEC-ESCALA",
                $"El campo {campo} contiene más de 2 posiciones decimales; la especificación v3 exige exactamente dos decimales (0.00).");
        }
    }

    private static void ValidarCabeceraConcesionario(
        int indice,
        string idReg,
        string? razonSocial,
        string? ruc,
        string? codigoPuntoVenta,
        string? nombreComercialPuntoVenta,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        ValidarObligatorioVarchar(indice, idReg, "RazonSocial", razonSocial, "YAM-GEN-RAZON-001", hallazgos);

        if (string.IsNullOrWhiteSpace(ruc) || !Ruc11DigitosRegex().IsMatch(ruc))
        {
            AgregarError(hallazgos, indice, idReg, "RUC", "YAM-GEN-RUC-001",
                "El campo RUC del concesionario es obligatorio y debe contener exactamente 11 dígitos numéricos.");
        }

        ValidarObligatorioVarchar(indice, idReg, "CodigoPuntoVenta", codigoPuntoVenta, "YAM-GEN-CPV-001", hallazgos);
        ValidarLongitudVarchar(indice, idReg, "NombreComercialPuntoVenta", nombreComercialPuntoVenta, hallazgos);
    }

    private static void ValidarDocumentoCliente(
        int indice,
        string idReg,
        string? tipoDocumentoCliente,
        string? numeroDocumentoCliente,
        string prefijoRegla,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (string.IsNullOrWhiteSpace(numeroDocumentoCliente))
        {
            AgregarError(hallazgos, indice, idReg, "NumeroDocumentoCliente", $"{prefijoRegla}-001",
                "El campo NumeroDocumentoCliente es obligatorio.");
            return;
        }

        ValidarLongitudVarchar(indice, idReg, "NumeroDocumentoCliente", numeroDocumentoCliente, hallazgos);

        if (!string.IsNullOrEmpty(tipoDocumentoCliente) &&
            !YamahaV3Catalogos.TiposDocumentoClientePermitidos.Contains(tipoDocumentoCliente))
        {
            AgregarError(hallazgos, indice, idReg, "TipoDocumentoCliente", $"{prefijoRegla}-002",
                $"El valor '{tipoDocumentoCliente}' no es válido en TipoDocumentoCliente. Valores permitidos: DNI, CE, RUC, DOC NO DOMICILIADO.");
            return;
        }

        if (string.Equals(tipoDocumentoCliente, YamahaV3Catalogos.DocClienteDni, StringComparison.Ordinal))
        {
            if (!Dni8DigitosRegex().IsMatch(numeroDocumentoCliente))
            {
                AgregarError(hallazgos, indice, idReg, "NumeroDocumentoCliente", $"{prefijoRegla}-003",
                    "Cuando TipoDocumentoCliente es 'DNI', NumeroDocumentoCliente debe tener exactamente 8 dígitos numéricos.");
            }
        }
        else if (string.Equals(tipoDocumentoCliente, YamahaV3Catalogos.DocClienteCe, StringComparison.Ordinal))
        {
            if (!Ce9CaracteresRegex().IsMatch(numeroDocumentoCliente))
            {
                AgregarError(hallazgos, indice, idReg, "NumeroDocumentoCliente", $"{prefijoRegla}-004",
                    "Cuando TipoDocumentoCliente es 'CE', NumeroDocumentoCliente debe tener exactamente 9 caracteres alfanuméricos.");
            }
        }
        else if (string.Equals(tipoDocumentoCliente, YamahaV3Catalogos.DocClienteRuc, StringComparison.Ordinal))
        {
            if (!Ruc11DigitosRegex().IsMatch(numeroDocumentoCliente))
            {
                AgregarError(hallazgos, indice, idReg, "NumeroDocumentoCliente", $"{prefijoRegla}-005",
                    "Cuando TipoDocumentoCliente es 'RUC', NumeroDocumentoCliente debe tener exactamente 11 dígitos numéricos.");
            }
        }
        else if (string.IsNullOrEmpty(tipoDocumentoCliente))
        {
            // Sección 6.2 / 6.4: Si no se envía TipoDocumentoCliente, solo se puede inferir si mide 8 (DNI), 9 (CE) u 11 (RUC).
            var esInferible = Dni8DigitosRegex().IsMatch(numeroDocumentoCliente)
                || Ce9CaracteresRegex().IsMatch(numeroDocumentoCliente)
                || Ruc11DigitosRegex().IsMatch(numeroDocumentoCliente);

            if (!esInferible)
            {
                AgregarError(hallazgos, indice, idReg, "TipoDocumentoCliente", $"{prefijoRegla}-006",
                    "Para documentos cuyo formato no corresponde a DNI (8 dígitos), CE (9 caracteres) o RUC (11 dígitos), debe enviarse explícitamente TipoDocumentoCliente = 'DOC NO DOMICILIADO'.");
            }
        }
    }

    private static void ValidarObligatorioVarchar(
        int indice,
        string idReg,
        string campo,
        string? valor,
        string codigoRegla,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            AgregarError(hallazgos, indice, idReg, campo, codigoRegla, $"El campo {campo} es obligatorio.");
            return;
        }

        ValidarLongitudVarchar(indice, idReg, campo, valor, hallazgos);
    }

    private static void ValidarLongitudVarchar(
        int indice,
        string idReg,
        string campo,
        string? valor,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (!string.IsNullOrEmpty(valor) && valor.Length > YamahaV3Catalogos.LongitudMaximaVarchar)
        {
            AgregarError(hallazgos, indice, idReg, campo, "YAM-GEN-LEN-255",
                $"El campo {campo} excede la longitud máxima permitida de {YamahaV3Catalogos.LongitudMaximaVarchar} caracteres (Sección 8).");
        }
    }

    private static void ValidarFechaObligatoria(
        int indice,
        string idReg,
        string campo,
        string? valor,
        string codigoRegla,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            AgregarError(hallazgos, indice, idReg, campo, codigoRegla,
                $"El campo {campo} es obligatorio (formato DD/MM/YYYY).");
            return;
        }

        if (!EsFechaValidaDdMmYyyy(valor))
        {
            AgregarError(hallazgos, indice, idReg, campo, $"{codigoRegla}-FMT",
                $"El campo {campo} debe tener el formato de fecha exacto DD/MM/YYYY (ej. 15/05/2025).");
        }
    }

    private static void ValidarCelularOpcional(
        int indice,
        string idReg,
        string campo,
        string? valor,
        string codigoRegla,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (!string.IsNullOrEmpty(valor) && !Celular9DigitosRegex().IsMatch(valor))
        {
            AgregarError(hallazgos, indice, idReg, campo, codigoRegla,
                $"El campo {campo} debe contener exactamente 9 dígitos numéricos sin espacios.");
        }
    }

    private static void ValidarCorreoOpcional(
        int indice,
        string idReg,
        string campo,
        string? valor,
        string codigoRegla,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return;
        }

        ValidarLongitudVarchar(indice, idReg, campo, valor, hallazgos);

        if (!EmailBasicoRegex().IsMatch(valor))
        {
            AgregarError(hallazgos, indice, idReg, campo, codigoRegla,
                $"El campo {campo} no tiene un formato de correo electrónico válido.");
        }
    }

    public static bool EsFechaValidaDdMmYyyy(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Length != 10)
        {
            return false;
        }

        return DateTime.TryParseExact(
            valor,
            YamahaV3Catalogos.FormatoFecha,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static string ObtenerIdentificadorSeguro(
        string? primario,
        string? secundario,
        string? terciario,
        int indice)
    {
        if (!string.IsNullOrWhiteSpace(primario))
        {
            return primario.Trim();
        }

        if (!string.IsNullOrWhiteSpace(secundario))
        {
            return secundario.Trim();
        }

        if (!string.IsNullOrWhiteSpace(terciario))
        {
            return terciario.Trim();
        }

        return $"Fila-{indice + 1}";
    }

    private static void AgregarError(
        List<YamahaValidacionHallazgoDto> hallazgos,
        int indice,
        string idReg,
        string campo,
        string codigoRegla,
        string mensaje)
    {
        hallazgos.Add(new YamahaValidacionHallazgoDto(
            indice,
            idReg,
            campo,
            codigoRegla,
            SeveridadValidacionYamaha.Error,
            mensaje));
    }

    private static void AgregarAdvertencia(
        List<YamahaValidacionHallazgoDto> hallazgos,
        int indice,
        string idReg,
        string campo,
        string codigoRegla,
        string mensaje)
    {
        hallazgos.Add(new YamahaValidacionHallazgoDto(
            indice,
            idReg,
            campo,
            codigoRegla,
            SeveridadValidacionYamaha.Advertencia,
            mensaje));
    }

    private static YamahaResultadoValidacionLoteDto ConstruirResultado(
        string servicio,
        int totalRegistros,
        List<YamahaValidacionHallazgoDto> hallazgos)
    {
        if (totalRegistros == 0)
        {
            AgregarError(
                hallazgos,
                -1,
                "Lote",
                "Lote",
                "YAM-GEN-LOTE-001",
                "El lote de registros está vacío; debe contener al menos un registro para ser válido.");
        }

        var indicesConError = new HashSet<int>();
        var totalErrores = 0;
        var totalAdvertencias = 0;

        foreach (var h in hallazgos)
        {
            if (h.Severidad == SeveridadValidacionYamaha.Error)
            {
                totalErrores++;
                if (h.IndiceRegistro >= 0)
                {
                    indicesConError.Add(h.IndiceRegistro);
                }
            }
            else
            {
                totalAdvertencias++;
            }
        }

        var registrosConErrores = indicesConError.Count;
        var registrosValidos = Math.Max(0, totalRegistros - registrosConErrores);

        return new YamahaResultadoValidacionLoteDto(
            Servicio: servicio,
            TotalRegistros: totalRegistros,
            RegistrosValidos: registrosValidos,
            RegistrosConErrores: registrosConErrores,
            TotalErroresBloqueantes: totalErrores,
            TotalAdvertencias: totalAdvertencias,
            EsLoteValido: totalRegistros > 0 && totalErrores == 0,
            Hallazgos: hallazgos);
    }
}
