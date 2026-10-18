using System.Text.Json.Serialization;
using CRMTeamBenavides.Api.Features.Yamaha;

namespace CRMTeamBenavides.Domain.Entities;

/// <summary>
/// Identifica los tres servicios independientes de integración definidos en Yamaha v3.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum YamahaServicioSincronizacion
{
    Ventas = 1,
    Stock = 2,
    ServicioTecnico = 3
}

/// <summary>
/// Estado del ciclo de vida de una ejecución de sincronización con Yamaha v3.
/// Permite registrar ejecuciones en curso sin falsos positivos de éxito.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum YamahaEstadoSincronizacion
{
    EnProceso = 1,
    Completado = 2,
    Fallido = 3,

    /// <summary>
    /// No había registros que enviar y no se llamó a Yamaha. Cuenta como ejecución correcta
    /// para el watermark y el cierre diario de Stock (plan v3, sección 5.2).
    /// </summary>
    SinCambios = 4
}

/// <summary>
/// Registro de auditoría, trazabilidad e idempotencia exclusivo de las sincronizaciones con Yamaha Motor del Perú v3.
/// Es un registro de sólo inserción (append-only) para diagnósticos, monitoreo de latencia y trazabilidad de lotes.
/// No hereda de BaseEntity ya que un registro de auditoría técnica no requiere soft-delete ni auditoría propia.
/// </summary>
public class YamahaSyncLog
{
    public const int MaxRespuestaCuerpoLongitud = 4000;
    public const int MaxMensajeErrorLongitud = 2000;
    public const int MaxPayloadHashLongitud = 128;
    public const int MaxModoEnvioLongitud = 50;
    public const string MensajeSinCambios = "Sin registros para enviar; no se llamó a Yamaha.";

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Servicio Yamaha que se ejecutó: Ventas, Stock o ServicioTecnico.</summary>
    public YamahaServicioSincronizacion Servicio { get; set; }

    /// <summary>Estado actual de la ejecución: EnProceso, Completado o Fallido.</summary>
    public YamahaEstadoSincronizacion Estado { get; set; } = YamahaEstadoSincronizacion.EnProceso;

    /// <summary>Marca temporal UTC de inicio de la sincronización.</summary>
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;

    /// <summary>Marca temporal UTC de culminación (nulo mientras esté EnProceso).</summary>
    public DateTime? FechaFin { get; set; }

    /// <summary>
    /// Inicio (exclusivo) del rango de fechas exportado en Ventas y Servicio Técnico.
    /// Nulo en Stock, que siempre envía la foto completa.
    /// </summary>
    public DateTime? RangoDesdeUtc { get; set; }

    /// <summary>
    /// Fin (inclusivo) del rango exportado. El siguiente lote incremental parte exactamente de aquí,
    /// de modo que ningún registro queda entre dos lotes.
    /// </summary>
    public DateTime? RangoHastaUtc { get; set; }

    /// <summary>Total de registros leídos y procesados del CRM.</summary>
    public int RegistrosProcesados { get; set; }

    /// <summary>Total de registros validados que cumplieron la especificación Yamaha v3.</summary>
    public int RegistrosValidos { get; set; }

    /// <summary>Total de registros rechazados por validaciones previas o errores estructurales.</summary>
    public int RegistrosRechazados { get; set; }

    /// <summary>Hash SHA-256 del payload serializado, para control de idempotencia y no repudio.</summary>
    public string? PayloadHash { get; set; }

    /// <summary>Código HTTP de la respuesta de Azure Functions (nulo para Dry-Run o si falló antes de la llamada de red).</summary>
    public int? CodigoHttp { get; set; }

    /// <summary>Cuerpo de la respuesta de Yamaha o resumen diagnóstico (sanitizado de secretos y truncado a 4000 caracteres).</summary>
    public string? RespuestaCuerpo { get; set; }

    /// <summary>Mensaje de error detallado en caso de fallo técnico, de validación o de red.</summary>
    public string? MensajeError { get; set; }

    /// <summary>Indica si el proceso culminó con éxito en todas sus fases (siempre false mientras esté EnProceso).</summary>
    public bool Exitoso { get; set; }

    /// <summary>True si fue una ejecución simulada (Dry-Run); False si involucró llamadas HTTP reales a Yamaha.</summary>
    public bool EsSimulacion { get; set; }

    /// <summary>Modo de disparo: "DryRun", "Manual", "Programado", etc.</summary>
    public string? ModoEnvio { get; set; }

    /// <summary>Usuario que disparó manualmente la sincronización (nulo si fue un background job automático).</summary>
    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    // -------------------------------------------------------------------------
    // Fábricas y Métodos de Control de Estado
    // -------------------------------------------------------------------------

    /// <summary>
    /// Inicia un registro de sincronización en estado EnProceso con FechaFin nula y Exitoso = false.
    /// Garantiza que nunca se registre falsamente una ejecución exitosa mientras esté en curso.
    /// </summary>
    public static YamahaSyncLog Iniciar(
        YamahaServicioSincronizacion servicio,
        bool esSimulacion = false,
        Guid? usuarioId = null,
        string? modoEnvio = null,
        DateTime? rangoDesdeUtc = null,
        DateTime? rangoHastaUtc = null)
    {
        return new YamahaSyncLog
        {
            Id = Guid.NewGuid(),
            Servicio = servicio,
            Estado = YamahaEstadoSincronizacion.EnProceso,
            FechaInicio = DateTime.UtcNow,
            FechaFin = null,
            RangoDesdeUtc = rangoDesdeUtc,
            RangoHastaUtc = rangoHastaUtc,
            EsSimulacion = esSimulacion,
            Exitoso = false,
            UsuarioId = usuarioId,
            ModoEnvio = TruncarCadena(modoEnvio, MaxModoEnvioLongitud)
        };
    }

    /// <summary>
    /// Registra la finalización exitosa de la sincronización.
    /// </summary>
    public void RegistrarExito(
        int procesados,
        int validos,
        int rechazados,
        string? payloadHash = null,
        int? codigoHttp = null,
        string? respuestaCuerpo = null)
    {
        Estado = YamahaEstadoSincronizacion.Completado;
        FechaFin = DateTime.UtcNow;
        Exitoso = true;
        RegistrosProcesados = procesados >= 0 ? procesados : 0;
        RegistrosValidos = validos >= 0 ? validos : 0;
        RegistrosRechazados = rechazados >= 0 ? rechazados : 0;
        PayloadHash = TruncarCadena(payloadHash, MaxPayloadHashLongitud);
        CodigoHttp = codigoHttp;
        RespuestaCuerpo = TruncarCadena(YamahaSecurityHelper.SanitizarTexto(respuestaCuerpo), MaxRespuestaCuerpoLongitud);
        MensajeError = null;
    }

    /// <summary>
    /// Registra el fallo de la sincronización con su mensaje descriptivo y opcionalmente código HTTP.
    /// </summary>
    public void RegistrarFallo(
        string mensajeError,
        int? codigoHttp = null,
        string? respuestaCuerpo = null,
        int procesados = 0,
        int validos = 0,
        int rechazados = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mensajeError);

        Estado = YamahaEstadoSincronizacion.Fallido;
        FechaFin = DateTime.UtcNow;
        Exitoso = false;
        MensajeError = TruncarCadena(YamahaSecurityHelper.SanitizarTexto(mensajeError), MaxMensajeErrorLongitud);
        CodigoHttp = codigoHttp;
        RespuestaCuerpo = TruncarCadena(YamahaSecurityHelper.SanitizarTexto(respuestaCuerpo), MaxRespuestaCuerpoLongitud);
        RegistrosProcesados = procesados >= 0 ? procesados : 0;
        RegistrosValidos = validos >= 0 ? validos : 0;
        RegistrosRechazados = rechazados >= 0 ? rechazados : 0;
    }

    /// <summary>
    /// Registra una ejecución sin registros que enviar: no hubo llamada HTTP a Yamaha.
    /// </summary>
    /// <param name="procesados">Entidades leídas del CRM (pueden existir aunque ninguna sea proyectable).</param>
    /// <param name="rechazados">Entidades leídas que no se pudieron proyectar al contrato v3.</param>
    /// <param name="resumen">Detalle opcional para el historial; por defecto <see cref="MensajeSinCambios"/>.</param>
    public void RegistrarSinCambios(int procesados = 0, int rechazados = 0, string? resumen = null)
    {
        Estado = YamahaEstadoSincronizacion.SinCambios;
        FechaFin = DateTime.UtcNow;
        Exitoso = true;
        RegistrosProcesados = procesados >= 0 ? procesados : 0;
        RegistrosValidos = 0;
        RegistrosRechazados = rechazados >= 0 ? rechazados : 0;
        PayloadHash = null;
        CodigoHttp = null;
        RespuestaCuerpo = TruncarCadena(YamahaSecurityHelper.SanitizarTexto(resumen ?? MensajeSinCambios), MaxRespuestaCuerpoLongitud);
        MensajeError = null;
    }

    /// <summary>
    /// Valida la consistencia lógica interna de la entidad.
    /// </summary>
    public void ValidarConsistencia()
    {
        if (Estado == YamahaEstadoSincronizacion.EnProceso)
        {
            if (FechaFin.HasValue)
                throw new InvalidOperationException("Un log en estado 'EnProceso' no puede tener FechaFin asignada.");
            if (Exitoso)
                throw new InvalidOperationException("Un log en estado 'EnProceso' no puede marcarse como Exitoso.");
        }
        else if (Estado == YamahaEstadoSincronizacion.Completado)
        {
            if (!FechaFin.HasValue)
                throw new InvalidOperationException("Un log en estado 'Completado' debe tener FechaFin asignada.");
            if (FechaFin.Value < FechaInicio)
                throw new InvalidOperationException("FechaFin no puede ser anterior a FechaInicio.");
            if (!Exitoso)
                throw new InvalidOperationException("Un log en estado 'Completado' debe tener Exitoso = true.");
        }
        else if (Estado == YamahaEstadoSincronizacion.Fallido)
        {
            if (!FechaFin.HasValue)
                throw new InvalidOperationException("Un log en estado 'Fallido' debe tener FechaFin asignada.");
            if (Exitoso)
                throw new InvalidOperationException("Un log en estado 'Fallido' no puede tener Exitoso = true.");
            if (string.IsNullOrWhiteSpace(MensajeError))
                throw new InvalidOperationException("Un log en estado 'Fallido' debe contener un MensajeError descriptivo.");
        }
        else if (Estado == YamahaEstadoSincronizacion.SinCambios)
        {
            ValidarConsistenciaSinCambios();
        }

        if (RegistrosProcesados < 0 || RegistrosValidos < 0 || RegistrosRechazados < 0)
        {
            throw new InvalidOperationException("Las cantidades de registros no pueden ser negativas.");
        }

        if (RegistrosProcesados < RegistrosValidos + RegistrosRechazados)
        {
            throw new InvalidOperationException("El total de registros procesados no puede ser menor a la suma de válidos y rechazados.");
        }
    }

    private void ValidarConsistenciaSinCambios()
    {
        if (!FechaFin.HasValue)
            throw new InvalidOperationException("Un log en estado 'SinCambios' debe tener FechaFin asignada.");
        if (!Exitoso)
            throw new InvalidOperationException("Un log en estado 'SinCambios' debe tener Exitoso = true.");
        if (RegistrosValidos != 0)
            throw new InvalidOperationException("Un log en estado 'SinCambios' no puede tener registros válidos enviados.");
    }

    private static string? TruncarCadena(string? texto, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        texto = texto.Trim();
        return texto.Length <= longitudMaxima ? texto : texto[..longitudMaxima];
    }
}
