using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Catálogos y constantes literales definidas en la Especificación Técnica de Servicios de Datos
/// de Yamaha Motor del Perú S.A. (YMDP) — versión 3.
/// </summary>
public static class YamahaV3Catalogos
{
    public const string FormatoFecha = "dd/MM/yyyy";
    public const int LongitudMaximaVarchar = 255;
    public const int LongitudExactaRuc = 11;
    public const int LongitudMaximaVin = 20;
    public const int LongitudMinimaChasis = 15;
    public const int LongitudMaximaChasis = 20;
    public const int LongitudMaximaCodigoYamahaSte = 50;
    public const decimal MaximoDecimal10_2 = 99_999_999.99m;

    // Sección 3.3 — Líneas de Negocio
    public const string LineaMotocicletas = "MC";
    public const string LineaCuatrimotos = "ATV";
    public const string LineaMotoresFueraDeBorda = "OM";
    public const string LineaSpareParts = "SP";
    public const string LineaServicioTecnico = "STE";

    public static readonly IReadOnlySet<string> LineasVentasYStock = new HashSet<string>(StringComparer.Ordinal)
    {
        LineaMotocicletas,
        LineaCuatrimotos,
        LineaMotoresFueraDeBorda,
        LineaSpareParts
    };

    public static readonly IReadOnlySet<string> SubLineasServicioTecnico = new HashSet<string>(StringComparer.Ordinal)
    {
        LineaSpareParts,
        LineaServicioTecnico
    };

    // Sección 4.1 — Tipo de Pago y Entidad Financiera
    public const string TipoPagoContado = "CONTADO";
    public const string TipoPagoCredito = "CREDITO";

    public static readonly IReadOnlySet<string> TiposPagoPermitidos = new HashSet<string>(StringComparer.Ordinal)
    {
        TipoPagoContado,
        TipoPagoCredito
    };

    public static readonly IReadOnlySet<string> EntidadesFinancierasCatalogo = new HashSet<string>(StringComparer.Ordinal)
    {
        "CONTADO",
        "EFECTIVA",
        "INTEGRA",
        "MERCADO PAGO",
        "GLOBAL GO",
        "CREDITO DIRECTO",
        "FONBIENES",
        "CALIDDA",
        "BCP",
        "BBVA"
    };

    // Sección 4.3 — Tipo de Documento de Venta
    public const string DocumentoVentaFactura = "FACTURA";
    public const string DocumentoVentaBoleta = "BOLETA DE VENTA";
    public const string DocumentoVentaNotaCredito = "NOTA DE CREDITO";
    public const string DocumentoVentaNotaDebito = "NOTA DE DEBITO";

    public static readonly IReadOnlySet<string> TiposDocumentoVentaPermitidos = new HashSet<string>(StringComparer.Ordinal)
    {
        DocumentoVentaFactura,
        DocumentoVentaBoleta,
        DocumentoVentaNotaCredito,
        DocumentoVentaNotaDebito
    };

    // Secciones 4.4 y 6.4 — Tipo de Documento (Cliente)
    public const string DocClienteDni = "DNI";
    public const string DocClienteCe = "CE";
    public const string DocClienteRuc = "RUC";
    public const string DocClienteNoDomiciliado = "DOC NO DOMICILIADO";

    public static readonly IReadOnlySet<string> TiposDocumentoClientePermitidos = new HashSet<string>(StringComparer.Ordinal)
    {
        DocClienteDni,
        DocClienteCe,
        DocClienteRuc,
        DocClienteNoDomiciliado
    };

    // Sección 4 — Otros catálogos de Ventas
    public const string CanalMostrador = "MOSTRADOR";
    public const string CanalTaller = "TALLER";

    public static readonly IReadOnlySet<string> CanalesVentaPermitidos = new HashSet<string>(StringComparer.Ordinal)
    {
        CanalMostrador,
        CanalTaller
    };

    public const string TipoPuntoVentaFijo = "3S";

    public static readonly IReadOnlySet<string> MonedasPermitidas = new HashSet<string>(StringComparer.Ordinal)
    {
        "PEN",
        "USD"
    };

    public static readonly IReadOnlySet<string> TiposClientePermitidos = new HashSet<string>(StringComparer.Ordinal)
    {
        "CLIENTE FINAL",
        "TALLERISTA",
        "MINORISTA",
        "CORPORATIVO"
    };

    public static readonly IReadOnlySet<string> CanalesContactoClientePermitidos = new HashSet<string>(StringComparer.Ordinal)
    {
        "PRESENCIAL",
        "WEB",
        "TELEFÓNICO"
    };

    // Sección 6.2 y 6.3 — Servicio Técnico
    public const string EstadoOrdenFacturado = "FACTURADO";
    public const string MarcaFijaYamaha = "YAMAHA";
    public const string TipoServicioNoAplica = "-";

    public static readonly IReadOnlySet<string> TiposServicioPermitidos = new HashSet<string>(StringComparer.Ordinal)
    {
        "MP",
        "D",
        "R",
        "MP+R",
        "RC",
        "G",
        "I",
        "L",
        "SG",
        TipoServicioNoAplica
    };
}

/// <summary>
/// Contrato oficial del Servicio 1 — Ventas (Especificación Técnica Yamaha v3, Sección 4).
/// Contiene exactamente los 39 campos definidos en el contrato de datos.
/// Cualquier campo no contemplado en este contrato es rechazado por el API de Yamaha.
/// </summary>
public sealed record YamahaVentaV3Dto
{
    [JsonPropertyName("RazonSocial")]
    public string RazonSocial { get; init; } = string.Empty;

    [JsonPropertyName("RUC")]
    public string RUC { get; init; } = string.Empty;

    [JsonPropertyName("CodigoPuntoVenta")]
    public string CodigoPuntoVenta { get; init; } = string.Empty;

    [JsonPropertyName("NombreComercialPuntoVenta")]
    public string NombreComercialPuntoVenta { get; init; } = string.Empty;

    [JsonPropertyName("VIN")]
    public string VIN { get; init; } = string.Empty;

    [JsonPropertyName("Modelo")]
    public string Modelo { get; init; } = string.Empty;

    [JsonPropertyName("Color")]
    public string Color { get; init; } = string.Empty;

    /// <summary>
    /// Año modelo del vehículo (INT, Condicional: obligatorio para MC y ATV; no aplica para OM ni SP).
    /// Cuando es <c>null</c> (no aplica), el convertidor escribe <c>""</c> según la Sección 3.4.
    /// </summary>
    [JsonPropertyName("AnioModelo")]
    [JsonConverter(typeof(YamahaNullableIntJsonConverter))]
    public int? AnioModelo { get; init; }

    [JsonPropertyName("FechaFacturacion")]
    public string FechaFacturacion { get; init; } = string.Empty;

    [JsonPropertyName("NumeroComprobante")]
    public string NumeroComprobante { get; init; } = string.Empty;

    [JsonPropertyName("TipoDocumentoVenta")]
    public string TipoDocumentoVenta { get; init; } = string.Empty;

    [JsonPropertyName("TipoPago")]
    public string TipoPago { get; init; } = string.Empty;

    [JsonPropertyName("EntidadFinanciera")]
    public string EntidadFinanciera { get; init; } = string.Empty;

    [JsonPropertyName("MontoSinIGV")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal MontoSinIGV { get; init; }

    [JsonPropertyName("MontoConIGV")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal MontoConIGV { get; init; }

    [JsonPropertyName("Canal")]
    public string Canal { get; init; } = string.Empty;

    [JsonPropertyName("LineaNegocio")]
    public string LineaNegocio { get; init; } = string.Empty;

    [JsonPropertyName("TipoPuntoVenta")]
    public string TipoPuntoVenta { get; init; } = YamahaV3Catalogos.TipoPuntoVentaFijo;

    [JsonPropertyName("NumeroMotor")]
    public string NumeroMotor { get; init; } = string.Empty;

    [JsonPropertyName("Moneda")]
    public string Moneda { get; init; } = "PEN";

    [JsonPropertyName("PrecioFinal")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal PrecioFinal { get; init; }

    [JsonPropertyName("CodigoYamaha")]
    public string CodigoYamaha { get; init; } = string.Empty;

    [JsonPropertyName("Descripcion")]
    public string Descripcion { get; init; } = string.Empty;

    [JsonPropertyName("CantidadVendida")]
    public int CantidadVendida { get; init; }

    [JsonPropertyName("DescuentoPorcentaje")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal DescuentoPorcentaje { get; init; }

    [JsonPropertyName("DescuentoSoles")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal DescuentoSoles { get; init; }

    [JsonPropertyName("PrecioUnitarioConIGV")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal PrecioUnitarioConIGV { get; init; }

    [JsonPropertyName("TipoCliente")]
    public string TipoCliente { get; init; } = string.Empty;

    [JsonPropertyName("NombreCompletoCliente")]
    public string NombreCompletoCliente { get; init; } = string.Empty;

    [JsonPropertyName("TipoDocumentoCliente")]
    public string TipoDocumentoCliente { get; init; } = string.Empty;

    [JsonPropertyName("NumeroDocumentoCliente")]
    public string NumeroDocumentoCliente { get; init; } = string.Empty;

    [JsonPropertyName("CanalContactoCliente")]
    public string CanalContactoCliente { get; init; } = string.Empty;

    [JsonPropertyName("DistritoCliente")]
    public string DistritoCliente { get; init; } = string.Empty;

    [JsonPropertyName("CiudadCliente")]
    public string CiudadCliente { get; init; } = string.Empty;

    [JsonPropertyName("DepartamentoCliente")]
    public string DepartamentoCliente { get; init; } = string.Empty;

    [JsonPropertyName("DireccionCliente")]
    public string DireccionCliente { get; init; } = string.Empty;

    [JsonPropertyName("MotoActualCliente")]
    public string MotoActualCliente { get; init; } = string.Empty;

    [JsonPropertyName("CelularCliente")]
    public string CelularCliente { get; init; } = string.Empty;

    [JsonPropertyName("CorreoCliente")]
    public string CorreoCliente { get; init; } = string.Empty;
}

/// <summary>
/// Contrato oficial del Servicio 2 — Stock (Especificación Técnica Yamaha v3, Sección 5.3).
/// Contiene exactamente los 12 campos vigentes. Excluye expresamente <c>SitioDealer</c> y <c>FechaVenta</c>,
/// eliminados en la versión 3.
/// </summary>
public sealed record YamahaStockV3Dto
{
    [JsonPropertyName("RazonSocial")]
    public string RazonSocial { get; init; } = string.Empty;

    [JsonPropertyName("RUC")]
    public string RUC { get; init; } = string.Empty;

    [JsonPropertyName("CodigoPuntoVenta")]
    public string CodigoPuntoVenta { get; init; } = string.Empty;

    [JsonPropertyName("NombreComercialPuntoVenta")]
    public string NombreComercialPuntoVenta { get; init; } = string.Empty;

    [JsonPropertyName("LineaNegocio")]
    public string LineaNegocio { get; init; } = string.Empty;

    [JsonPropertyName("CodigoYamaha")]
    public string CodigoYamaha { get; init; } = string.Empty;

    [JsonPropertyName("Descripcion")]
    public string Descripcion { get; init; } = string.Empty;

    [JsonPropertyName("StockDisponible")]
    public int StockDisponible { get; init; }

    [JsonPropertyName("CostoVentaSinIGV")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal CostoVentaSinIGV { get; init; }

    [JsonPropertyName("CostoVentaConIGV")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal CostoVentaConIGV { get; init; }

    [JsonPropertyName("FechaIngreso")]
    public string FechaIngreso { get; init; } = string.Empty;

    [JsonPropertyName("VIN")]
    public string VIN { get; init; } = string.Empty;
}

/// <summary>
/// Contrato oficial del Servicio 3 — Servicio Técnico (Especificación Técnica Yamaha v3, Sección 6.2).
/// Contiene exactamente los 30 campos vigentes. Excluye expresamente <c>FechaApertura</c>,
/// <c>FechaCierre</c>, <c>MOSinIGV</c>, <c>RepuestosSinIGV</c> e <c>IGV</c>, eliminados en la versión 3.
/// </summary>
public sealed record YamahaServicioTecnicoV3Dto
{
    [JsonPropertyName("LineaNegocio")]
    public string LineaNegocio { get; init; } = YamahaV3Catalogos.LineaServicioTecnico;

    [JsonPropertyName("SubLineaNegocio")]
    public string SubLineaNegocio { get; init; } = string.Empty;

    [JsonPropertyName("RazonSocial")]
    public string RazonSocial { get; init; } = string.Empty;

    [JsonPropertyName("RUC")]
    public string RUC { get; init; } = string.Empty;

    [JsonPropertyName("NombreComercialPuntoVenta")]
    public string NombreComercialPuntoVenta { get; init; } = string.Empty;

    [JsonPropertyName("CodigoPuntoVenta")]
    public string CodigoPuntoVenta { get; init; } = string.Empty;

    [JsonPropertyName("NumeroOrdenTrabajo")]
    public string NumeroOrdenTrabajo { get; init; } = string.Empty;

    [JsonPropertyName("FechaFacturacion")]
    public string FechaFacturacion { get; init; } = string.Empty;

    [JsonPropertyName("Estado")]
    public string Estado { get; init; } = YamahaV3Catalogos.EstadoOrdenFacturado;

    [JsonPropertyName("TipoDocumentoCliente")]
    public string TipoDocumentoCliente { get; init; } = string.Empty;

    [JsonPropertyName("NumeroDocumentoCliente")]
    public string NumeroDocumentoCliente { get; init; } = string.Empty;

    [JsonPropertyName("NombreCliente")]
    public string NombreCliente { get; init; } = string.Empty;

    [JsonPropertyName("Celular")]
    public string Celular { get; init; } = string.Empty;

    [JsonPropertyName("Correo")]
    public string Correo { get; init; } = string.Empty;

    [JsonPropertyName("Placa")]
    public string Placa { get; init; } = string.Empty;

    [JsonPropertyName("Chasis")]
    public string Chasis { get; init; } = string.Empty;

    [JsonPropertyName("Marca")]
    public string Marca { get; init; } = YamahaV3Catalogos.MarcaFijaYamaha;

    [JsonPropertyName("Modelo")]
    public string Modelo { get; init; } = string.Empty;

    [JsonPropertyName("Version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("Kilometraje")]
    public int Kilometraje { get; init; }

    [JsonPropertyName("TipoServicio")]
    public string TipoServicio { get; init; } = string.Empty;

    [JsonPropertyName("Actividad")]
    public string Actividad { get; init; } = string.Empty;

    [JsonPropertyName("CodigoYamaha")]
    public string CodigoYamaha { get; init; } = string.Empty;

    [JsonPropertyName("Descripcion")]
    public string Descripcion { get; init; } = string.Empty;

    /// <summary>
    /// Cantidad de unidades (INT, Condicional: obligatorio para SubLineaNegocio = SP; no aplica para STE).
    /// Cuando es <c>null</c> (no aplica), el convertidor escribe <c>""</c> según la Sección 3.4.
    /// </summary>
    [JsonPropertyName("Cantidad")]
    [JsonConverter(typeof(YamahaNullableIntJsonConverter))]
    public int? Cantidad { get; init; }

    [JsonPropertyName("MontoSinIGV")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal MontoSinIGV { get; init; }

    [JsonPropertyName("MontoConIGV")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal MontoConIGV { get; init; }

    /// <summary>
    /// Precio unitario del repuesto (DECIMAL(10,2), solo aplica para SubLineaNegocio = SP).
    /// Cuando es <c>null</c> (en mano de obra STE), escribe <c>""</c> según la Sección 3.4.
    /// </summary>
    [JsonPropertyName("PrecioUnit")]
    [JsonConverter(typeof(YamahaNullableDecimal2JsonConverter))]
    public decimal? PrecioUnit { get; init; }

    [JsonPropertyName("Descuento")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal Descuento { get; init; }

    [JsonPropertyName("TotalConIGV")]
    [JsonConverter(typeof(YamahaDecimal2JsonConverter))]
    public decimal TotalConIGV { get; init; }
}

/// <summary>
/// Serializa valores <see cref="decimal"/> siempre con punto decimal (<c>.</c>) y exactamente
/// dos posiciones decimales (<c>0.00</c>), independientemente de la configuración regional del servidor,
/// conforme a la Sección 8 de la especificación de Yamaha v3.
/// </summary>
public sealed class YamahaDecimal2JsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        throw new JsonException("Formato decimal inválido para contrato Yamaha v3.");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        var formatted = Math.Round(value, 2, MidpointRounding.AwayFromZero)
            .ToString("0.00", CultureInfo.InvariantCulture);
        writer.WriteRawValue(formatted, skipInputValidation: true);
    }
}

/// <summary>
/// Serializa un <see cref="decimal"/> condicional anulable: si tiene valor, escribe el número con
/// dos decimales (<c>0.00</c>); si es <c>null</c> (no aplica a la línea actual), escribe una cadena vacía <c>""</c>
/// conforme a la Sección 3.4 de la especificación de Yamaha v3.
/// </summary>
public sealed class YamahaNullableDecimal2JsonConverter : JsonConverter<decimal?>
{
    public override bool HandleNull => true;

    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        throw new JsonException("Formato decimal condicional inválido para contrato Yamaha v3.");
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteStringValue(string.Empty);
            return;
        }

        var formatted = Math.Round(value.Value, 2, MidpointRounding.AwayFromZero)
            .ToString("0.00", CultureInfo.InvariantCulture);
        writer.WriteRawValue(formatted, skipInputValidation: true);
    }
}

/// <summary>
/// Serializa un <see cref="int"/> condicional anulable (<c>AnioModelo</c> en Ventas y <c>Cantidad</c> en STE):
/// si tiene valor, escribe el entero JSON; si es <c>null</c> (no aplica a la línea actual),
/// escribe una cadena vacía <c>""</c> conforme a la Sección 3.4 de la especificación de Yamaha v3.
/// </summary>
public sealed class YamahaNullableIntJsonConverter : JsonConverter<int?>
{
    public override bool HandleNull => true;

    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        throw new JsonException("Formato entero condicional inválido para contrato Yamaha v3.");
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteStringValue(string.Empty);
            return;
        }

        writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// Garantiza que las propiedades <see cref="string"/> nunca se serialicen como <c>null</c> en JSON,
/// emitiendo siempre <c>""</c> cuando el valor sea nulo.
/// </summary>
public sealed class YamahaNonNullStringJsonConverter : JsonConverter<string>
{
    public override bool HandleNull => true;

    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Null
            ? string.Empty
            : (reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value ?? string.Empty);
    }
}

/// <summary>
/// Serializador JSON dedicado y determinista para los tres servicios de datos de Yamaha v3.
/// Respeta mayúsculas/minúsculas exactas, incluye todas las propiedades del contrato sin omisiones,
/// y formatea los decimales con separador punto (<c>.</c>) y dos posiciones (<c>0.00</c>).
/// </summary>
public static class YamahaJsonSerializer
{
    public static readonly JsonSerializerOptions Options = CrearOpciones(indented: false);
    public static readonly JsonSerializerOptions IndentedOptions = CrearOpciones(indented: true);

    private static JsonSerializerOptions CrearOpciones(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = indented,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        options.Converters.Add(new YamahaNonNullStringJsonConverter());
        options.Converters.Add(new YamahaDecimal2JsonConverter());
        options.Converters.Add(new YamahaNullableDecimal2JsonConverter());
        options.Converters.Add(new YamahaNullableIntJsonConverter());

        return options;
    }

    public static string SerializarVentas(IEnumerable<YamahaVentaV3Dto> registros, bool indented = false) =>
        JsonSerializer.Serialize(registros, indented ? IndentedOptions : Options);

    public static string SerializarStock(IEnumerable<YamahaStockV3Dto> registros, bool indented = false) =>
        JsonSerializer.Serialize(registros, indented ? IndentedOptions : Options);

    public static string SerializarServicioTecnico(IEnumerable<YamahaServicioTecnicoV3Dto> registros, bool indented = false) =>
        JsonSerializer.Serialize(registros, indented ? IndentedOptions : Options);
}
