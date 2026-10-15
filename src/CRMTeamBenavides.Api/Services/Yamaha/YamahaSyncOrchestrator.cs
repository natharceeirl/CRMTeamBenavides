using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha.Exportadores;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Implementación del orquestador de sincronización y auditoría para Yamaha v3.
/// Garantiza:
/// 1. Registro obligatorio del ciclo de vida en YamahaSyncLog (inicia en EnProceso, culmina en Completado/Fallido).
/// 2. Extracción de datos limpia mediante EF Core AsNoTracking con una única consulta de extracción reutilizada en validación y envío.
/// 3. Validación previa estricta con YamahaV3Validator.
/// 4. Generación y cálculo SHA-256 del payload para control de idempotencia y trazabilidad.
/// 5. Protección estricta contra envíos accidentales (si Enabled == false o esSimulacion, jamás hace llamadas HTTP).
/// 6. Exclusión mutua por servicio en memoria para prevenir solapamiento de ejecuciones manuales y programadas.
/// </summary>
public sealed class YamahaSyncOrchestrator : IYamahaSyncOrchestrator
{
    /// <summary>
    /// Semáforos estáticos en memoria para garantizar exclusión mutua por servicio dentro de la instancia de la aplicación.
    /// Evita que dos sincronizaciones (manual o programada) del mismo servicio se ejecuten simultáneamente sobre el mismo rango de datos.
    /// Nota de arquitectura: Esta exclusión mutua coordina procesos dentro de la misma instancia (.NET process).
    /// Si la API se escala a múltiples réplicas en balanceador, se debe incorporar bloqueo distribuido
    /// a nivel de base de datos (pg_advisory_lock en PostgreSQL) o cache distribuido (Redis).
    /// </summary>
    private static readonly ConcurrentDictionary<YamahaServicioSincronizacion, SemaphoreSlim> _serviceSemaphores = new()
    {
        [YamahaServicioSincronizacion.Ventas] = new SemaphoreSlim(1, 1),
        [YamahaServicioSincronizacion.Stock] = new SemaphoreSlim(1, 1),
        [YamahaServicioSincronizacion.ServicioTecnico] = new SemaphoreSlim(1, 1)
    };

    private readonly ApplicationDbContext _context;
    private readonly IYamahaVentasExporter _ventasExporter;
    private readonly IYamahaStockExporter _stockExporter;
    private readonly IYamahaServicioTecnicoExporter _servicioTecnicoExporter;
    private readonly IYamahaWatermarkService _watermarkService;
    private readonly IYamahaApiClient _apiClient;
    private readonly YamahaSettings _settings;
    private readonly ILogger<YamahaSyncOrchestrator> _logger;

    public YamahaSyncOrchestrator(
        ApplicationDbContext context,
        IYamahaVentasExporter ventasExporter,
        IYamahaStockExporter stockExporter,
        IYamahaServicioTecnicoExporter servicioTecnicoExporter,
        IYamahaWatermarkService watermarkService,
        IYamahaApiClient apiClient,
        IOptions<YamahaSettings> settings,
        ILogger<YamahaSyncOrchestrator> logger)
    {
        _context = context;
        _ventasExporter = ventasExporter;
        _stockExporter = stockExporter;
        _servicioTecnicoExporter = servicioTecnicoExporter;
        _watermarkService = watermarkService;
        _apiClient = apiClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<YamahaSyncEjecucionResultado> SincronizarServicioAsync(
        YamahaServicioSincronizacion servicio,
        bool esSimulacion = false,
        string modoEnvio = "Manual",
        Guid? usuarioId = null,
        DateTime? rangoDesdeUtc = null,
        DateTime? rangoHastaUtc = null,
        CancellationToken cancellationToken = default)
    {
        var semaphore = _serviceSemaphores.GetOrAdd(servicio, _ => new SemaphoreSlim(1, 1));
        var segundosEspera = _settings.ConcurrenciaEsperaSegundos >= 0 ? _settings.ConcurrenciaEsperaSegundos : 5;
        var tiempoEspera = TimeSpan.FromSeconds(segundosEspera);

        if (!await semaphore.WaitAsync(tiempoEspera, cancellationToken))
        {
            _logger.LogWarning("Se intentó sincronizar [{Servicio}], pero ya existe otra sincronización activa para este servicio.", servicio);
            return new YamahaSyncEjecucionResultado(
                SyncLogId: Guid.Empty,
                Servicio: servicio,
                Exitoso: false,
                EsSimulacion: esSimulacion,
                TotalLeidos: 0,
                TotalValidos: 0,
                TotalRechazados: 0,
                PayloadHash: null,
                CodigoHttp: null,
                Mensaje: $"Ya existe una sincronización en curso para el servicio {servicio}. Se omitió la ejecución para evitar solapamiento.",
                ErroresOAdvertencias: new[] { $"Concurrencia detectada en servicio {servicio}." },
                Duracion: TimeSpan.Zero);
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            rangoHastaUtc ??= DateTime.UtcNow;

            DateTime desdeUtc;
            if (rangoDesdeUtc.HasValue)
            {
                desdeUtc = rangoDesdeUtc.Value;
            }
            else
            {
                desdeUtc = await _watermarkService.ObtenerPuntoCorteDesdeUtcAsync(servicio, cancellationToken);
            }

            // 1. Iniciar registro de auditoría append-only en estado EnProceso
            var syncLog = YamahaSyncLog.Iniciar(
                servicio: servicio,
                esSimulacion: esSimulacion || !_settings.Enabled,
                usuarioId: usuarioId,
                modoEnvio: modoEnvio);

            _context.YamahaSyncLogs.Add(syncLog);
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                // 2. Extraer y mapear según el servicio (única extracción de datos reutilizada en validación y envío)
                IReadOnlyList<string> observaciones;
                int totalLeidos;
                int totalValidos;
                int totalOmitidos;
                string jsonPayload;
                IReadOnlyList<string> erroresValidacion;

                IReadOnlyList<YamahaVentaV3Dto>? loteVentasValidos = null;
                IReadOnlyList<YamahaStockV3Dto>? loteStockValidos = null;
                IReadOnlyList<YamahaServicioTecnicoV3Dto>? loteServicioTecnicoValidos = null;

                switch (servicio)
                {
                    case YamahaServicioSincronizacion.Ventas:
                    {
                        var exp = await _ventasExporter.ExportarVentasAsync(
                            desdeUtc: desdeUtc,
                            hastaUtc: rangoHastaUtc,
                            soloVentasMostrador: true,
                            cancellationToken: cancellationToken);

                        loteVentasValidos = exp.RegistrosValidos;
                        totalLeidos = exp.TotalEntidadesLeidas;
                        totalValidos = exp.TotalRegistrosValidos;
                        totalOmitidos = exp.TotalRegistrosOmitidos;
                        observaciones = exp.ObservacionesYAdvertencias;

                        var val = YamahaV3Validator.ValidarLoteVentas(loteVentasValidos);
                        erroresValidacion = val.Hallazgos
                            .Where(h => h.Severidad == SeveridadValidacionYamaha.Error)
                            .Select(h => $"[{h.Campo}] {h.Mensaje}")
                            .ToList();
                        jsonPayload = YamahaJsonSerializer.SerializarVentas(loteVentasValidos, indented: false);
                        break;
                    }
                    case YamahaServicioSincronizacion.Stock:
                    {
                        var exp = await _stockExporter.ExportarStockAsync(
                            soloMarcaYamaha: false,
                            cancellationToken: cancellationToken);

                        loteStockValidos = exp.RegistrosValidos;
                        totalLeidos = exp.TotalEntidadesLeidas;
                        totalValidos = exp.TotalRegistrosValidos;
                        totalOmitidos = exp.TotalRegistrosOmitidos;
                        observaciones = exp.ObservacionesYAdvertencias;

                        var val = YamahaV3Validator.ValidarLoteStock(loteStockValidos);
                        erroresValidacion = val.Hallazgos
                            .Where(h => h.Severidad == SeveridadValidacionYamaha.Error)
                            .Select(h => $"[{h.Campo}] {h.Mensaje}")
                            .ToList();
                        jsonPayload = YamahaJsonSerializer.SerializarStock(loteStockValidos, indented: false);
                        break;
                    }
                    case YamahaServicioSincronizacion.ServicioTecnico:
                    {
                        var exp = await _servicioTecnicoExporter.ExportarServicioTecnicoAsync(
                            desdeUtc: desdeUtc,
                            hastaUtc: rangoHastaUtc,
                            cancellationToken: cancellationToken);

                        loteServicioTecnicoValidos = exp.RegistrosValidos;
                        totalLeidos = exp.TotalEntidadesLeidas;
                        totalValidos = exp.TotalRegistrosValidos;
                        totalOmitidos = exp.TotalRegistrosOmitidos;
                        observaciones = exp.ObservacionesYAdvertencias;

                        var val = YamahaV3Validator.ValidarLoteServicioTecnico(loteServicioTecnicoValidos);
                        erroresValidacion = val.Hallazgos
                            .Where(h => h.Severidad == SeveridadValidacionYamaha.Error)
                            .Select(h => $"[{h.Campo}] {h.Mensaje}")
                            .ToList();
                        jsonPayload = YamahaJsonSerializer.SerializarServicioTecnico(loteServicioTecnicoValidos, indented: false);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(servicio), servicio, "Servicio Yamaha no reconocido.");
                }

                // 3. Calcular hash SHA-256 del payload serializado
                var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(jsonPayload));
                var payloadHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

                // 4. Verificar si hubo errores de validación de contrato
                if (erroresValidacion.Count > 0)
                {
                    var resumenErrores = string.Join(" | ", erroresValidacion.Take(5));
                    syncLog.RegistrarFallo(
                        mensajeError: $"Validación de contrato Yamaha v3 rechazó el lote: {resumenErrores}",
                        codigoHttp: null,
                        respuestaCuerpo: null,
                        procesados: totalLeidos,
                        validos: 0,
                        rechazados: totalOmitidos + erroresValidacion.Count);

                    await _context.SaveChangesAsync(cancellationToken);

                    return new YamahaSyncEjecucionResultado(
                        SyncLogId: syncLog.Id,
                        Servicio: servicio,
                        Exitoso: false,
                        EsSimulacion: syncLog.EsSimulacion,
                        TotalLeidos: totalLeidos,
                        TotalValidos: 0,
                        TotalRechazados: totalOmitidos + erroresValidacion.Count,
                        PayloadHash: payloadHash,
                        CodigoHttp: null,
                        Mensaje: "Lote rechazado por validador de contrato Yamaha v3.",
                        ErroresOAdvertencias: erroresValidacion,
                        Duracion: stopwatch.Elapsed);
                }

                // 5. Despacho real vs modo simulación (Dry-Run)
                bool debeSimular = esSimulacion || !_settings.Enabled;

                if (debeSimular)
                {
                    var cuerpoSimulado = "{\"status\":\"ok\",\"modo\":\"simulacion\",\"mensaje\":\"Ejecución en modo simulación (Dry-Run). No se enviaron peticiones HTTP a Yamaha.\"}";

                    syncLog.RegistrarExito(
                        procesados: totalLeidos,
                        validos: totalValidos,
                        rechazados: totalOmitidos,
                        payloadHash: payloadHash,
                        codigoHttp: 200,
                        respuestaCuerpo: cuerpoSimulado);

                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Sincronización simulada [{Servicio}] culminada: {Validos} válidos, {Omitidos} omitidos en {Ms}ms.",
                        servicio,
                        totalValidos,
                        totalOmitidos,
                        stopwatch.ElapsedMilliseconds);

                    return new YamahaSyncEjecucionResultado(
                        SyncLogId: syncLog.Id,
                        Servicio: servicio,
                        Exitoso: true,
                        EsSimulacion: true,
                        TotalLeidos: totalLeidos,
                        TotalValidos: totalValidos,
                        TotalRechazados: totalOmitidos,
                        PayloadHash: payloadHash,
                        CodigoHttp: 200,
                        Mensaje: "Sincronización simulada exitosa.",
                        ErroresOAdvertencias: observaciones,
                        Duracion: stopwatch.Elapsed);
                }

                // 6. Envío real a través de IYamahaApiClient (solo si Enabled == true y !esSimulacion)
                // Se reutilizan exactamente los DTOs validados en el paso 2 (H-01: cero segunda extracción)
                YamahaHttpRespuestaDto httpResp;
                switch (servicio)
                {
                    case YamahaServicioSincronizacion.Ventas:
                    {
                        httpResp = await _apiClient.EnviarVentasAsync(loteVentasValidos!, cancellationToken);
                        break;
                    }
                    case YamahaServicioSincronizacion.Stock:
                    {
                        httpResp = await _apiClient.EnviarStockAsync(loteStockValidos!, cancellationToken);
                        break;
                    }
                    case YamahaServicioSincronizacion.ServicioTecnico:
                    {
                        httpResp = await _apiClient.EnviarServicioTecnicoAsync(loteServicioTecnicoValidos!, cancellationToken);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(servicio), servicio, null);
                }

                if (httpResp.EsExitoso)
                {
                    syncLog.RegistrarExito(
                        procesados: totalLeidos,
                        validos: totalValidos,
                        rechazados: totalOmitidos,
                        payloadHash: payloadHash,
                        codigoHttp: httpResp.CodigoHttp,
                        respuestaCuerpo: httpResp.CuerpoRespuesta);
                }
                else
                {
                    syncLog.RegistrarFallo(
                        mensajeError: httpResp.MensajeError ?? $"Yamaha API respondió código HTTP {httpResp.CodigoHttp}",
                        codigoHttp: httpResp.CodigoHttp,
                        respuestaCuerpo: httpResp.CuerpoRespuesta,
                        procesados: totalLeidos,
                        validos: 0,
                        rechazados: totalOmitidos);
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new YamahaSyncEjecucionResultado(
                    SyncLogId: syncLog.Id,
                    Servicio: servicio,
                    Exitoso: httpResp.EsExitoso,
                    EsSimulacion: false,
                    TotalLeidos: totalLeidos,
                    TotalValidos: totalValidos,
                    TotalRechazados: totalOmitidos,
                    PayloadHash: payloadHash,
                    CodigoHttp: httpResp.CodigoHttp,
                    Mensaje: httpResp.EsExitoso ? "Sincronización enviada con éxito a Yamaha." : httpResp.MensajeError,
                    ErroresOAdvertencias: observaciones,
                    Duracion: stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                var secretos = _settings.ObtenerSecretosConfigurados();
                var errorSanitizado = YamahaSecurityHelper.SanitizarTexto(ex.Message, secretos);

                _logger.LogError(ex, "Fallo inesperado durante la orquestación de Yamaha [{Servicio}]: {Error}", servicio, errorSanitizado);

                syncLog.RegistrarFallo(
                    mensajeError: $"Excepción en orquestador: {errorSanitizado}",
                    codigoHttp: null,
                    respuestaCuerpo: null,
                    procesados: 0,
                    validos: 0,
                    rechazados: 0);

                await _context.SaveChangesAsync(CancellationToken.None);

                return new YamahaSyncEjecucionResultado(
                    SyncLogId: syncLog.Id,
                    Servicio: servicio,
                    Exitoso: false,
                    EsSimulacion: syncLog.EsSimulacion,
                    TotalLeidos: 0,
                    TotalValidos: 0,
                    TotalRechazados: 0,
                    PayloadHash: null,
                    CodigoHttp: null,
                    Mensaje: $"Error en orquestación: {errorSanitizado}",
                    ErroresOAdvertencias: new[] { errorSanitizado },
                    Duracion: stopwatch.Elapsed);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }


    /// <inheritdoc />
    public async Task<int> RecuperarEjecucionesAbandonadasAsync(CancellationToken cancellationToken = default)
    {
        var minutosExpiracion = _settings.TiempoExpiracionEnProcesoMinutos > 0
            ? _settings.TiempoExpiracionEnProcesoMinutos
            : 30;

        var umbralUtc = DateTime.UtcNow.AddMinutes(-minutosExpiracion);

        // Selecciona exclusivamente registros EnProceso cuya FechaInicio sea anterior al umbral de expiración
        // No afecta ejecuciones recientes (que podrían pertenecer a un proceso concurrentemente activo)
        var logsAbandonados = await _context.YamahaSyncLogs
            .Where(l => l.Estado == YamahaEstadoSincronizacion.EnProceso && l.FechaInicio <= umbralUtc)
            .ToListAsync(cancellationToken);

        if (logsAbandonados.Count == 0)
        {
            return 0;
        }

        foreach (var log in logsAbandonados)
        {
            log.RegistrarFallo(
                mensajeError: $"Ejecucion interrumpida o abandonada. Supero el tiempo limite de expiracion de {minutosExpiracion} minutos (iniciada {log.FechaInicio:u}). Marcada como fallida automaticamente para permitir reintentos seguros.",
                codigoHttp: null,
                respuestaCuerpo: null,
                procesados: log.RegistrosProcesados,
                validos: 0,
                rechazados: log.RegistrosRechazados);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Se recuperaron {Count} ejecuciones de YamahaSyncLog abandonadas en estado 'EnProceso' anteriores a {UmbralUtc:u}.",
            logsAbandonados.Count,
            umbralUtc);

        return logsAbandonados.Count;
    }
}
