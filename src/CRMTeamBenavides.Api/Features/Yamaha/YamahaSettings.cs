namespace CRMTeamBenavides.Api.Features.Yamaha;

/// <summary>
/// Configuración tipada para el conector Yamaha API (incluyendo los tres servicios de datos v3).
/// Los secretos (<see cref="FunctionKey"/>, <see cref="VentasFunctionKey"/>,
/// <see cref="StockFunctionKey"/>, <see cref="ServicioTecnicoFunctionKey"/> y <see cref="AuthHeaderValue"/>)
/// deben proveerse exclusivamente mediante User Secrets o variables de entorno,
/// nunca en <c>appsettings.json</c> ni en el control de versiones.
/// </summary>
public class YamahaSettings
{
    public const string SectionName = "YamahaApi";

    /// <summary>
    /// Indica si la integración saliente hacia Yamaha está habilitada.
    /// Permanece en <c>false</c> por defecto hasta completar la certificación y aprobación de negocio.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>URL base de la API de Yamaha, sin barra final. Ej: "https://api.yamaha.example.com/v1"</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Tiempo de espera por petición, en segundos. Valor por defecto: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Nombre del encabezado HTTP de autenticación (infraestructura base).
    /// </summary>
    public string AuthHeaderName { get; set; } = string.Empty;

    /// <summary>
    /// Valor del encabezado HTTP de autenticación.
    /// DEBE proveerse únicamente en User Secrets o variables de entorno.
    /// </summary>
    public string AuthHeaderValue { get; set; } = string.Empty;

    // -------------------------------------------------------------------------
    // Identificación del punto de venta (Especificación Yamaha v3 — Secciones 4, 5 y 6)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Código asignado por Yamaha Motor del Perú al punto de venta (<c>CodigoPuntoVenta</c>).
    /// Obligatorio en los servicios de Ventas, Stock y Servicio Técnico.
    /// </summary>
    public string CodigoPuntoVenta { get; set; } = string.Empty;

    /// <summary>
    /// Nombre comercial del punto de venta con ubicación (<c>NombreComercialPuntoVenta</c>).
    /// Opcional en los tres servicios. Ej: "Team Benavides - Arequipa".
    /// </summary>
    public string NombreComercialPuntoVenta { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de punto de venta (<c>TipoPuntoVenta</c>). Valor fijo en especificación v3: "3S".
    /// </summary>
    public string TipoPuntoVenta { get; set; } = "3S";

    // -------------------------------------------------------------------------
    // Programacion, Scheduler y Rendimiento (Seccion 3.1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Hora operativa local de Peru (America/Lima) para el envio diario de Stock al cierre de operaciones (formato "HH:mm").
    /// Valor por defecto: "22:00". El valor exacto debe ser confirmado con la gerencia del concesionario
    /// antes de activar la integracion productiva.
    /// </summary>
    public string HoraCierreStockPeru { get; set; } = "22:00";

    /// <summary>
    /// Intervalo en horas para la sincronizacion periodica de Ventas y Servicio Tecnico.
    /// Valor fijado por la Seccion 3.1 de la especificacion tecnica de Yamaha: 4 horas.
    /// </summary>
    public int IntervaloVentasServicioTecnicoHoras { get; set; } = 4;

    /// <summary>
    /// Tamano maximo de lote (chunk) para paginacion estable en memoria durante la extraccion desde PostgreSQL.
    /// Previene consumo excesivo de memoria en lecturas de varios meses. Valor por defecto: 500 registros.
    /// </summary>
    public int TamanioLoteExportacion { get; set; } = 500;

    /// <summary>
    /// Tiempo maximo en minutos para considerar una ejecucion 'EnProceso' como abandonada o interrumpida por caida de proceso.
    /// Valor por defecto: 30 minutos (ampliamente superior a cualquier timeout HTTP transitorio).
    /// </summary>
    public int TiempoExpiracionEnProcesoMinutos { get; set; } = 30;

    /// <summary>
    /// Intervalo en minutos para reintentar el envio diario de Stock en caso de fallo tecnico o de red (por defecto 15 minutos).
    /// Evita bucles intensivos mientras permite recuperar el cierre en la misma jornada.
    /// </summary>
    public int IntervaloReintentoStockMinutos { get; set; } = 15;

    /// <summary>
    /// Tiempo maximo en segundos de espera para adquirir la exclusion mutua por servicio antes de rechazar una sincronizacion concurrente (por defecto 5 segundos).
    /// </summary>
    public int ConcurrenciaEsperaSegundos { get; set; } = 5;

    /// <summary>
    /// Obtiene la hora de cierre de Stock parseada como TimeOnly (hora local de Peru).
    /// Si el valor configurado no es valido, retorna 22:00 por defecto.
    /// </summary>
    public TimeOnly ObtenerHoraCierreStock()
    {
        if (TimeOnly.TryParse(HoraCierreStockPeru, out var hora))
        {
            return hora;
        }
        return new TimeOnly(22, 0);
    }

    // -------------------------------------------------------------------------
    // Endpoints de los 3 servicios de datos (Especificación Yamaha v3 — Sección 7)
    // -------------------------------------------------------------------------

    /// <summary>
    /// URL base (sin <c>?code=...</c>) del endpoint HTTP POST para el Servicio 1 — Ventas (<c>ReceiveRetailVentas</c>).
    /// </summary>
    public string VentasEndpointUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL base (sin <c>?code=...</c>) del endpoint HTTP POST para el Servicio 2 — Stock (<c>ReceiveRetailStock</c>).
    /// </summary>
    public string StockEndpointUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL base (sin <c>?code=...</c>) del endpoint HTTP POST para el Servicio 3 — Servicio Técnico (<c>ReceiveRetailServicioTecnico</c>).
    /// </summary>
    public string ServicioTecnicoEndpointUrl { get; set; } = string.Empty;

    // -------------------------------------------------------------------------
    // Function Keys de Azure Functions (Sección 7) — SOLO en User Secrets / Variables de Entorno
    // -------------------------------------------------------------------------

    /// <summary>
    /// Function Key compartida para los endpoints de Yamaha cuando el concesionario recibe una clave única.
    /// NUNCA almacenar en <c>appsettings.json</c> ni registrar en logs.
    /// </summary>
    public string FunctionKey { get; set; } = string.Empty;

    /// <summary>
    /// Function Key específica para el Servicio de Ventas (si es distinta de <see cref="FunctionKey"/>).
    /// </summary>
    public string VentasFunctionKey { get; set; } = string.Empty;

    /// <summary>
    /// Function Key específica para el Servicio de Stock (si es distinta de <see cref="FunctionKey"/>).
    /// </summary>
    public string StockFunctionKey { get; set; } = string.Empty;

    /// <summary>
    /// Function Key específica para el Servicio Técnico (si es distinta de <see cref="FunctionKey"/>).
    /// </summary>
    public string ServicioTecnicoFunctionKey { get; set; } = string.Empty;

    /// <summary>
    /// Resuelve la Function Key efectiva para el Servicio de Ventas.
    /// </summary>
    public string ObtenerFunctionKeyVentas() =>
        !string.IsNullOrWhiteSpace(VentasFunctionKey) ? VentasFunctionKey.Trim() : FunctionKey.Trim();

    /// <summary>
    /// Resuelve la Function Key efectiva para el Servicio de Stock.
    /// </summary>
    public string ObtenerFunctionKeyStock() =>
        !string.IsNullOrWhiteSpace(StockFunctionKey) ? StockFunctionKey.Trim() : FunctionKey.Trim();

    /// <summary>
    /// Resuelve la Function Key efectiva para el Servicio Técnico.
    /// </summary>
    public string ObtenerFunctionKeyServicioTecnico() =>
        !string.IsNullOrWhiteSpace(ServicioTecnicoFunctionKey) ? ServicioTecnicoFunctionKey.Trim() : FunctionKey.Trim();

    /// <summary>
    /// Devuelve todos los valores secretos no vacíos configurados actualmente para poder redactarlos en logs.
    /// </summary>
    public IReadOnlyList<string> ObtenerSecretosConfigurados()
    {
        var secretos = new List<string>(5);
        AgregarSiNoVacio(secretos, FunctionKey);
        AgregarSiNoVacio(secretos, VentasFunctionKey);
        AgregarSiNoVacio(secretos, StockFunctionKey);
        AgregarSiNoVacio(secretos, ServicioTecnicoFunctionKey);
        AgregarSiNoVacio(secretos, AuthHeaderValue);
        return secretos;
    }

    private static void AgregarSiNoVacio(List<string> lista, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            lista.Add(valor.Trim());
        }
    }
}
