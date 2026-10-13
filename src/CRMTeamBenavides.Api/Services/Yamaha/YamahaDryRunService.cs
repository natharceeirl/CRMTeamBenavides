using CRMTeamBenavides.Api.Features.Yamaha;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Describe una decisión funcional, contable o técnica pendiente de confirmación
/// antes de habilitar la extracción y el envío productivo hacia Yamaha v3.
/// </summary>
public sealed record YamahaDecisionPendienteDto(
    string CodigoDecision,
    string ServicioAfectado,
    string Descripcion);

/// <summary>
/// Resultado de evaluar un registro individual en modo de simulación local (Dry-Run).
/// </summary>
public sealed record YamahaRegistroDryRunEvaluadoDto<TRegistro>(
    int IndiceRegistro,
    string IdentificadorRegistro,
    bool EsValidoLocalmente,
    TRegistro RegistroOriginalSinAlterar,
    IReadOnlyList<YamahaValidacionHallazgoDto> ErroresBloqueantes,
    IReadOnlyList<YamahaValidacionHallazgoDto> Advertencias);

/// <summary>
/// Resultado completo de una simulación local (Dry-Run) para un lote de Yamaha v3.
/// Garantiza trazablemente que ningún dato fue modificado, persistido ni enviado al API de Yamaha.
/// </summary>
public sealed record YamahaDryRunResultadoDto<TRegistro>(
    string Servicio,
    bool EsSimulacionDryRun,
    bool EnviadoAYamaha,
    bool PersistidoEnBaseDeDatos,
    string EstadoSimulacion,
    string AvisoSimulacion,
    DateTime FechaEvaluacionUtc,
    YamahaResultadoValidacionLoteDto ResumenValidacion,
    IReadOnlyList<YamahaRegistroDryRunEvaluadoDto<TRegistro>> RegistrosEvaluados,
    IReadOnlyList<YamahaDecisionPendienteDto> DecisionesPendientes,
    string JsonSimulado);

/// <summary>
/// Abstracción del servicio de simulación local (Dry-Run) para los contratos de Yamaha v3.
/// </summary>
public interface IYamahaDryRunService
{
    /// <summary>
    /// Evalúa un lote de Ventas en memoria sin enviar tráfico HTTP ni escribir en base de datos.
    /// </summary>
    YamahaDryRunResultadoDto<YamahaVentaV3Dto> SimularLoteVentas(
        IReadOnlyList<YamahaVentaV3Dto> registros,
        bool indentarJson = false);

    /// <summary>
    /// Evalúa un lote de Stock en memoria sin enviar tráfico HTTP ni escribir en base de datos.
    /// </summary>
    YamahaDryRunResultadoDto<YamahaStockV3Dto> SimularLoteStock(
        IReadOnlyList<YamahaStockV3Dto> registros,
        bool indentarJson = false);

    /// <summary>
    /// Evalúa un lote de Servicio Técnico en memoria sin enviar tráfico HTTP ni escribir en base de datos.
    /// </summary>
    YamahaDryRunResultadoDto<YamahaServicioTecnicoV3Dto> SimularLoteServicioTecnico(
        IReadOnlyList<YamahaServicioTecnicoV3Dto> registros,
        bool indentarJson = false);
}

/// <summary>
/// Implementación pura del modo de simulación local (Dry-Run) para Yamaha v3.
///
/// Reglas de seguridad e integridad:
/// 1. No realiza llamadas HTTP a los endpoints de Yamaha.
/// 2. No crea ni modifica ventas, comprobantes, notas de crédito, inventario ni órdenes de servicio.
/// 3. No transforma ni corrige silenciosamente los datos de entrada para forzar su aprobación.
/// 4. Nunca reporta un registro como enviado o aceptado por Yamaha.
/// </summary>
public sealed class YamahaDryRunService : IYamahaDryRunService
{
    public const string EstadoValidadoLocalmente = "VALIDADO_LOCALMENTE_SIN_ENVIO_CON_DECISIONES_PENDIENTES";
    public const string EstadoRechazadoLocalmente = "RECHAZADO_EN_VALIDACION_LOCAL_SIN_ENVIO";

    private const string TextoAvisoSimulacion =
        "SIMULACIÓN LOCAL (DRY-RUN): Los registros solo han sido evaluados contra las reglas sintácticas locales. " +
        "Ningún dato ha sido enviado ni aceptado por Yamaha Motor del Perú, y no se ha realizado ningún cambio en la base de datos.";

    private static readonly IReadOnlyList<YamahaDecisionPendienteDto> DecisionesPendientesVentas =
    [
        new("D-01", "Ventas", "Confirmar si el CRM reportará únicamente repuestos (SP) o también venta de unidades (MC, ATV, OM)."),
        new("D-02", "Ventas", "Confirmar con Yamaha si los repuestos facturados dentro de una Orden de Servicio van solo en Servicio Técnico o también en Ventas con Canal = TALLER."),
        new("D-04", "Ventas", "Definir el flujo contable y de facturación para Notas de Crédito y anulaciones posteriores al envío."),
        new("D-05", "Ventas", "Confirmar si todas las ventas son al CONTADO o si se registrarán ventas a CREDITO con entidades de la tabla 4.1."),
        new("D-06", "Ventas", "Confirmar si se enviará 0.00 en DescuentoPorcentaje y DescuentoSoles o si se persistirán descuentos por línea."),
        new("AMB-3.4-8", "Ventas", "Confirmar con Yamaha si el campo entero condicional AnioModelo debe enviarse como cadena vacía (\"\") o como 0 cuando no aplica (SP/OM).")
    ];

    private static readonly IReadOnlyList<YamahaDecisionPendienteDto> DecisionesPendientesStock =
    [
        new("D-01", "Stock", "Confirmar alcance de líneas de negocio en inventario (solo SP o también unidades MC, ATV, OM)."),
        new("D-03", "Stock", "Confirmar con Yamaha si CostoVentaSinIGV y CostoVentaConIGV corresponden al costo de compra del concesionario (Costo) o al precio de venta al público (PrecioVenta)."),
        new("D-07", "Stock", "Confirmar el criterio de filtrado de productos Yamaha dentro del catálogo general de Productos.")
    ];

    private static readonly IReadOnlyList<YamahaDecisionPendienteDto> DecisionesPendientesServicioTecnico =
    [
        new("D-06", "ServicioTecnico", "Confirmar si el campo Descuento se enviará siempre como 0.00 o se persistirá porcentaje de descuento en DetalleServicio."),
        new("D-08", "ServicioTecnico", "Confirmar la regla de proyección no destructiva del correlativo interno OS- hacia el formato obligatorio OT- exigido por la Sección 6.2."),
        new("D-09", "ServicioTecnico", "Confirmar el catálogo de códigos TipoServicio (MP, D, R, MP+R, RC, G, I, L, SG, -) frente al enum TipoAtencion actual."),
        new("AMB-3.4-8", "ServicioTecnico", "Confirmar con Yamaha si los campos numéricos condicionales Cantidad y PrecioUnit deben enviarse como cadena vacía (\"\") o como 0/0.00 cuando SubLineaNegocio es STE.")
    ];

    /// <inheritdoc />
    public YamahaDryRunResultadoDto<YamahaVentaV3Dto> SimularLoteVentas(
        IReadOnlyList<YamahaVentaV3Dto> registros,
        bool indentarJson = false)
    {
        ArgumentNullException.ThrowIfNull(registros);

        var resumen = YamahaV3Validator.ValidarLoteVentas(registros);
        var evaluados = DesglosarPorRegistro(
            registros,
            resumen.Hallazgos,
            (r, idx) => ObtenerIdentificadorDisplay(r.NumeroComprobante, r.CodigoYamaha, r.VIN, idx));

        var json = YamahaJsonSerializer.SerializarVentas(registros, indentarJson);

        return ConstruirResultadoDryRun(
            "Ventas",
            resumen,
            evaluados,
            DecisionesPendientesVentas,
            json);
    }

    /// <inheritdoc />
    public YamahaDryRunResultadoDto<YamahaStockV3Dto> SimularLoteStock(
        IReadOnlyList<YamahaStockV3Dto> registros,
        bool indentarJson = false)
    {
        ArgumentNullException.ThrowIfNull(registros);

        var resumen = YamahaV3Validator.ValidarLoteStock(registros);
        var evaluados = DesglosarPorRegistro(
            registros,
            resumen.Hallazgos,
            (r, idx) => ObtenerIdentificadorDisplay(r.CodigoYamaha, r.VIN, null, idx));

        var json = YamahaJsonSerializer.SerializarStock(registros, indentarJson);

        return ConstruirResultadoDryRun(
            "Stock",
            resumen,
            evaluados,
            DecisionesPendientesStock,
            json);
    }

    /// <inheritdoc />
    public YamahaDryRunResultadoDto<YamahaServicioTecnicoV3Dto> SimularLoteServicioTecnico(
        IReadOnlyList<YamahaServicioTecnicoV3Dto> registros,
        bool indentarJson = false)
    {
        ArgumentNullException.ThrowIfNull(registros);

        var resumen = YamahaV3Validator.ValidarLoteServicioTecnico(registros);
        var evaluados = DesglosarPorRegistro(
            registros,
            resumen.Hallazgos,
            (r, idx) => ObtenerIdentificadorDisplay(r.NumeroOrdenTrabajo, r.Placa, r.CodigoYamaha, idx));

        var json = YamahaJsonSerializer.SerializarServicioTecnico(registros, indentarJson);

        return ConstruirResultadoDryRun(
            "ServicioTecnico",
            resumen,
            evaluados,
            DecisionesPendientesServicioTecnico,
            json);
    }

    private static IReadOnlyList<YamahaRegistroDryRunEvaluadoDto<TRegistro>> DesglosarPorRegistro<TRegistro>(
        IReadOnlyList<TRegistro> registros,
        IReadOnlyList<YamahaValidacionHallazgoDto> hallazgos,
        Func<TRegistro, int, string> selectorIdentificador)
    {
        var resultado = new List<YamahaRegistroDryRunEvaluadoDto<TRegistro>>(registros.Count);

        for (var i = 0; i < registros.Count; i++)
        {
            var registro = registros[i];
            var errores = new List<YamahaValidacionHallazgoDto>();
            var advertencias = new List<YamahaValidacionHallazgoDto>();

            foreach (var h in hallazgos)
            {
                if (h.IndiceRegistro != i)
                {
                    continue;
                }

                if (h.Severidad == SeveridadValidacionYamaha.Error)
                {
                    errores.Add(h);
                }
                else
                {
                    advertencias.Add(h);
                }
            }

            var identificador = registro is null
                ? $"Fila-{i + 1}"
                : selectorIdentificador(registro, i);

            resultado.Add(new YamahaRegistroDryRunEvaluadoDto<TRegistro>(
                IndiceRegistro: i,
                IdentificadorRegistro: identificador,
                EsValidoLocalmente: registro is not null && errores.Count == 0,
                RegistroOriginalSinAlterar: registro,
                ErroresBloqueantes: errores,
                Advertencias: advertencias));
        }

        return resultado;
    }

    private static YamahaDryRunResultadoDto<TRegistro> ConstruirResultadoDryRun<TRegistro>(
        string servicio,
        YamahaResultadoValidacionLoteDto resumen,
        IReadOnlyList<YamahaRegistroDryRunEvaluadoDto<TRegistro>> evaluados,
        IReadOnlyList<YamahaDecisionPendienteDto> decisionesPendientes,
        string jsonSimulado)
    {
        var estado = resumen.EsLoteValido
            ? EstadoValidadoLocalmente
            : EstadoRechazadoLocalmente;

        return new YamahaDryRunResultadoDto<TRegistro>(
            Servicio: servicio,
            EsSimulacionDryRun: true,
            EnviadoAYamaha: false,
            PersistidoEnBaseDeDatos: false,
            EstadoSimulacion: estado,
            AvisoSimulacion: TextoAvisoSimulacion,
            FechaEvaluacionUtc: DateTime.UtcNow,
            ResumenValidacion: resumen,
            RegistrosEvaluados: evaluados,
            DecisionesPendientes: decisionesPendientes,
            JsonSimulado: jsonSimulado);
    }

    private static string ObtenerIdentificadorDisplay(
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
}
