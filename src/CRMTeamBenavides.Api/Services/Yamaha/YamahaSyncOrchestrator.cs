using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services.Yamaha.Exportadores;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Implementación del orquestador de sincronización y auditoría para Yamaha v3.
/// Garantiza:
/// 1. Registro obligatorio del ciclo de vida en YamahaSyncLog (inicia en EnProceso, culmina en Completado/Fallido).
/// 2. Extracción de datos limpia mediante EF Core AsNoTracking.
/// 3. Validación previa estricta con YamahaV3Validator.
/// 4. Generación y cálculo SHA-256 del payload para control de idempotencia y trazabilidad.
/// 5. Protección estricta contra envíos accidentales (si Enabled == false o esSimulacion, jamás hace llamadas HTTP).
/// </summary>
public sealed class YamahaSyncOrchestrator : IYamahaSyncOrchestrator
{
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
            // 2. Extraer y mapear según el servicio
            IReadOnlyList<string> observaciones;
            int totalLeidos;
            int totalValidos;
            int totalOmitidos;
            string jsonPayload;
            IReadOnlyList<string> erroresValidacion;

            switch (servicio)
            {
                case YamahaServicioSincronizacion.Ventas:
                {
                    var exp = await _ventasExporter.ExportarVentasAsync(
                        desdeUtc: desdeUtc,
                        hastaUtc: rangoHastaUtc,
                        soloVentasMostrador: true,
                        cancellationToken: cancellationToken);

                    totalLeidos = exp.TotalEntidadesLeidas;
                    totalValidos = exp.TotalRegistrosValidos;
                    totalOmitidos = exp.TotalRegistrosOmitidos;
                    observaciones = exp.ObservacionesYAdvertencias;

                    var val = YamahaV3Validator.ValidarLoteVentas(exp.RegistrosValidos);
                    erroresValidacion = val.Hallazgos
                        .Where(h => h.Severidad == SeveridadValidacionYamaha.Error)
                        .Select(h => $"[{h.Campo}] {h.Mensaje}")
                        .ToList();
                    jsonPayload = YamahaJsonSerializer.SerializarVentas(exp.RegistrosValidos, indented: false);
                    break;
                }
                case YamahaServicioSincronizacion.Stock:
                {
                    var exp = await _stockExporter.ExportarStockAsync(
                        soloMarcaYamaha: false,
                        cancellationToken: cancellationToken);

                    totalLeidos = exp.TotalEntidadesLeidas;
                    totalValidos = exp.TotalRegistrosValidos;
                    totalOmitidos = exp.TotalRegistrosOmitidos;
                    observaciones = exp.ObservacionesYAdvertencias;

                    var val = YamahaV3Validator.ValidarLoteStock(exp.RegistrosValidos);
                    erroresValidacion = val.Hallazgos
                        .Where(h => h.Severidad == SeveridadValidacionYamaha.Error)
                        .Select(h => $"[{h.Campo}] {h.Mensaje}")
                        .ToList();
                    jsonPayload = YamahaJsonSerializer.SerializarStock(exp.RegistrosValidos, indented: false);
                    break;
                }
                case YamahaServicioSincronizacion.ServicioTecnico:
                {
                    var exp = await _servicioTecnicoExporter.ExportarServicioTecnicoAsync(
                        desdeUtc: desdeUtc,
                        hastaUtc: rangoHastaUtc,
                        cancellationToken: cancellationToken);

                    totalLeidos = exp.TotalEntidadesLeidas;
                    totalValidos = exp.TotalRegistrosValidos;
                    totalOmitidos = exp.TotalRegistrosOmitidos;
                    observaciones = exp.ObservacionesYAdvertencias;

                    var val = YamahaV3Validator.ValidarLoteServicioTecnico(exp.RegistrosValidos);
                    erroresValidacion = val.Hallazgos
                        .Where(h => h.Severidad == SeveridadValidacionYamaha.Error)
                        .Select(h => $"[{h.Campo}] {h.Mensaje}")
                        .ToList();
                    jsonPayload = YamahaJsonSerializer.SerializarServicioTecnico(exp.RegistrosValidos, indented: false);
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
            YamahaHttpRespuestaDto httpResp;
            switch (servicio)
            {
                case YamahaServicioSincronizacion.Ventas:
                {
                    var expVentas = await _ventasExporter.ExportarVentasAsync(desdeUtc, rangoHastaUtc, soloVentasMostrador: true, cancellationToken);
                    httpResp = await _apiClient.EnviarVentasAsync(expVentas.RegistrosValidos, cancellationToken);
                    break;
                }
                case YamahaServicioSincronizacion.Stock:
                {
                    var expStock = await _stockExporter.ExportarStockAsync(soloMarcaYamaha: false, cancellationToken);
                    httpResp = await _apiClient.EnviarStockAsync(expStock.RegistrosValidos, cancellationToken);
                    break;
                }
                case YamahaServicioSincronizacion.ServicioTecnico:
                {
                    var expSte = await _servicioTecnicoExporter.ExportarServicioTecnicoAsync(desdeUtc, rangoHastaUtc, cancellationToken);
                    httpResp = await _apiClient.EnviarServicioTecnicoAsync(expSte.RegistrosValidos, cancellationToken);
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
}
