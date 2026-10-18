using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Servicio de fondo (BackgroundService) para la ejecucion programada de sincronizaciones con Yamaha v3.
/// Reglas operativas:
/// 1. Permanece ESTRICTAMENTE DESACTIVADO mientras YamahaSettings.Enabled == false.
/// 2. Ventas y Servicio Tecnico se ejecutan periodicamente cada 4 horas (Seccion 3.1).
/// 3. Stock se ejecuta diariamente al cierre de operaciones (hora local de Peru America/Lima, configurable).
///    Si la jornada no se pudo cerrar antes de medianoche, se reintenta hasta la hora limite de recuperacion
///    de la manana siguiente y cuenta para la jornada anterior.
/// 4. Control de concurrencia mediante SemaphoreSlim para prevenir ejecuciones superpuestas.
/// 5. Recuperacion segura de ejecuciones 'EnProceso' abandonadas (mayores a 30 min) al inicio y periodicamente.
/// 6. Prevencion de duplicados: verifica en base de datos si el cierre de Stock de la jornada ya se ejecuto;
///    si no se puede verificar, no envia en ese minuto.
/// La hora se obtiene de <see cref="TimeProvider"/> para poder probar la agenda sin esperar en tiempo real.
/// </summary>
public sealed class YamahaSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<YamahaSettings> _settingsMonitor;
    private readonly ILogger<YamahaSyncBackgroundService> _logger;
    private readonly TimeProvider _tiempo;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly TimeSpan _intervaloInactivo = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _intervaloEvaluacionActivo = TimeSpan.FromMinutes(1);

    private DateTime? _ultimaEjecucionVentasSteUtc;
    private DateOnly? _ultimaJornadaStockCerrada;
    private DateOnly? _ultimaJornadaStockIntentada;
    private DateOnly? _ultimaJornadaPerdidaReportada;
    private DateTime? _ultimoIntentoStockFallidoUtc;
    private DateTime? _ultimaRecuperacionAbandonadasUtc;
    private bool _advertenciaRecuperacionInvalidaEmitida;

    public YamahaSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<YamahaSettings> settingsMonitor,
        ILogger<YamahaSyncBackgroundService> logger,
        TimeProvider? tiempo = null)
    {
        _scopeFactory = scopeFactory;
        _settingsMonitor = settingsMonitor;
        _logger = logger;
        _tiempo = tiempo ?? TimeProvider.System;
    }

    private DateTime AhoraUtc => _tiempo.GetUtcNow().UtcDateTime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("YamahaSyncBackgroundService iniciado. Estado Enabled: {Enabled}", _settingsMonitor.CurrentValue.Enabled);

        // Recuperacion inicial de posibles ejecuciones abandonadas por caidas previas
        await IntentarRecuperarAbandonadasAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = _settingsMonitor.CurrentValue;

            if (!settings.Enabled)
            {
                _logger.LogDebug(
                    "YamahaSyncBackgroundService: Integracion desactivada (YamahaSettings.Enabled == false). " +
                    "Permaneciendo en reposo.");

                try
                {
                    await Task.Delay(_intervaloInactivo, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                continue;
            }

            // Cuando este formalmente habilitado en produccion, evaluar agenda cada minuto
            try
            {
                await EvaluarYSincronizarSegunAgendaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado durante la evaluacion de agenda en YamahaSyncBackgroundService.");
            }

            try
            {
                await Task.Delay(_intervaloEvaluacionActivo, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("YamahaSyncBackgroundService detenido por senal de cancelacion.");
    }

    /// <summary>
    /// Evalua la agenda de sincronizaciones y ejecuta los servicios que correspondan segun el tiempo transcurrido
    /// y la hora local de Peru.
    /// </summary>
    public async Task EvaluarYSincronizarSegunAgendaAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsMonitor.CurrentValue;
        if (!settings.Enabled)
        {
            return;
        }

        // 1. Recuperacion periodica de abandonadas (cada 1 hora)
        if (!_ultimaRecuperacionAbandonadasUtc.HasValue ||
            AhoraUtc - _ultimaRecuperacionAbandonadasUtc.Value >= TimeSpan.FromHours(1))
        {
            await IntentarRecuperarAbandonadasAsync(cancellationToken);
            _ultimaRecuperacionAbandonadasUtc = AhoraUtc;
        }

        // 2. Evaluacion de Ventas y Servicio Tecnico (cada 4 horas)
        var intervaloHoras = settings.IntervaloVentasServicioTecnicoHoras > 0
            ? settings.IntervaloVentasServicioTecnicoHoras
            : 4;

        if (!_ultimaEjecucionVentasSteUtc.HasValue ||
            AhoraUtc - _ultimaEjecucionVentasSteUtc.Value >= TimeSpan.FromHours(intervaloHoras))
        {
            var resultadoCiclo = await EjecutarCicloProgramadoAsync(cancellationToken);

            // Un ciclo omitido por concurrencia no consume el turno: se reintenta en la siguiente evaluacion.
            // Un fallo real si espera el siguiente turno, sin perdida de datos (plan v3, seccion 5.2).
            if (resultadoCiclo != YamahaResultadoSincronizacion.OmitidoPorConcurrencia)
            {
                _ultimaEjecucionVentasSteUtc = AhoraUtc;
            }
        }

        // 3. Evaluacion diaria de Stock (cierre de operaciones hora Peru)
        await EvaluarCierreStockAsync(settings, cancellationToken);
    }

    /// <summary>
    /// Jornada de Stock pendiente segun la hora de Peru: la de hoy a partir de la hora de cierre; la de ayer,
    /// como recuperacion, antes de la hora limite de la manana; ninguna en el resto del dia.
    /// </summary>
    public static DateOnly? CalcularJornadaStockPendiente(DateTime ahoraUtc, TimeOnly horaCierre, TimeOnly? horaLimiteRecuperacion)
    {
        var ahoraPeru = HoraPeru.DesdeUtc(ahoraUtc);
        var hoyPeru = DateOnly.FromDateTime(ahoraPeru);
        var horaActualPeru = TimeOnly.FromDateTime(ahoraPeru);

        if (horaActualPeru >= horaCierre)
        {
            return hoyPeru;
        }

        if (horaLimiteRecuperacion.HasValue && horaActualPeru < horaLimiteRecuperacion.Value)
        {
            return hoyPeru.AddDays(-1);
        }

        return null;
    }

    /// <summary>La hora limite de recuperacion, o null si no es anterior a la hora de cierre.</summary>
    private TimeOnly? ObtenerLimiteRecuperacionValido(YamahaSettings settings, TimeOnly horaCierre)
    {
        var horaLimite = settings.ObtenerHoraLimiteRecuperacionStock();
        if (horaLimite < horaCierre)
        {
            return horaLimite;
        }

        if (!_advertenciaRecuperacionInvalidaEmitida)
        {
            _advertenciaRecuperacionInvalidaEmitida = true;
            _logger.LogWarning(
                "HoraLimiteRecuperacionStockPeru ({HoraLimite}) no es anterior a HoraCierreStockPeru ({HoraCierre}); la recuperacion del cierre de Stock tras medianoche queda desactivada.",
                horaLimite,
                horaCierre);
        }

        return null;
    }

    private async Task EvaluarCierreStockAsync(YamahaSettings settings, CancellationToken cancellationToken)
    {
        var horaCierre = settings.ObtenerHoraCierreStock();
        var horaLimite = ObtenerLimiteRecuperacionValido(settings, horaCierre);
        var jornada = CalcularJornadaStockPendiente(AhoraUtc, horaCierre, horaLimite);

        if (jornada is null)
        {
            await ReportarCierrePerdidoSiCorrespondeAsync(horaCierre, horaLimite, cancellationToken);
            return;
        }

        // A. Si la jornada ya se cerro en esta instancia, no repetir
        if (_ultimaJornadaStockCerrada == jornada)
        {
            return;
        }

        // B. Verificar en auditoria persistida si ya existe un cierre exitoso no simulado de la jornada.
        // Si no se puede verificar, no se envia en este minuto para no duplicar el cierre.
        var yaCerrada = await YaSeEjecutoStockCierreHoyAsync(jornada.Value, horaCierre, horaLimite, cancellationToken);
        if (yaCerrada is null)
        {
            _logger.LogWarning(
                "No se pudo verificar si el cierre de Stock de la jornada {Jornada} ya se envio; se omite este minuto para no duplicarlo.",
                jornada);
            return;
        }

        if (yaCerrada.Value)
        {
            _ultimaJornadaStockCerrada = jornada;
            _ultimoIntentoStockFallidoUtc = null;
            return;
        }

        // C. Si hubo un intento fallido previo, esperar el intervalo de reintento configurado
        var minutosReintento = settings.IntervaloReintentoStockMinutos >= 0 ? settings.IntervaloReintentoStockMinutos : 15;
        var intervaloReintento = TimeSpan.FromMinutes(minutosReintento);

        if (minutosReintento > 0 && _ultimoIntentoStockFallidoUtc.HasValue && AhoraUtc - _ultimoIntentoStockFallidoUtc.Value < intervaloReintento)
        {
            _logger.LogDebug(
                "YamahaSyncBackgroundService: Enfriamiento de reintento Stock activo. Siguiente intento en {MinutosRestantes:F1} min.",
                (intervaloReintento - (AhoraUtc - _ultimoIntentoStockFallidoUtc.Value)).TotalMinutes);
            return;
        }

        await IntentarCierreStockAsync(jornada.Value, horaCierre, minutosReintento, cancellationToken);
    }

    private async Task IntentarCierreStockAsync(
        DateOnly jornada,
        TimeOnly horaCierre,
        int minutosReintento,
        CancellationToken cancellationToken)
    {
        var esRecuperacion = jornada < DateOnly.FromDateTime(HoraPeru.DesdeUtc(AhoraUtc));
        _logger.LogInformation(
            "Iniciando intento de cierre diario de Stock para la jornada {Jornada} (Hora configurada: {HoraCierre}, recuperacion: {EsRecuperacion}).",
            jornada,
            horaCierre,
            esRecuperacion);

        _ultimaJornadaStockIntentada = jornada;
        var resultado = await EjecutarStockProgramadoAsync(cancellationToken);

        switch (resultado)
        {
            case YamahaResultadoSincronizacion.Exitoso:
            case YamahaResultadoSincronizacion.SinCambios:
            case YamahaResultadoSincronizacion.EnviadoSinRegistrar: // Yamaha ya lo recibió: no reenviar
                _ultimaJornadaStockCerrada = jornada;
                _ultimoIntentoStockFallidoUtc = null;
                _logger.LogInformation("Cierre diario de Stock completado ({Resultado}) para la jornada {Jornada}.", resultado, jornada);
                break;

            case YamahaResultadoSincronizacion.OmitidoPorConcurrencia:
                // No es un fallo: no se activa el enfriamiento y se reintenta en la siguiente evaluacion.
                _logger.LogInformation(
                    "Cierre diario de Stock para la jornada {Jornada} omitido porque hay otra sincronizacion en curso. Se reintentara en la siguiente evaluacion.",
                    jornada);
                break;

            default:
                _ultimoIntentoStockFallidoUtc = AhoraUtc;
                _logger.LogWarning(
                    "El intento de cierre diario de Stock para la jornada {Jornada} falló. Se reintentará en {Minutos} minutos.",
                    jornada,
                    minutosReintento);
                break;
        }
    }

    /// <summary>
    /// Fuera de la ventana de cierre y de recuperacion: si esta instancia intento la jornada anterior y no la
    /// cerro, lo deja registrado una sola vez como cierre perdido (tras confirmarlo en la auditoria).
    /// </summary>
    private async Task ReportarCierrePerdidoSiCorrespondeAsync(
        TimeOnly horaCierre,
        TimeOnly? horaLimite,
        CancellationToken cancellationToken)
    {
        var jornadaAnterior = DateOnly.FromDateTime(HoraPeru.DesdeUtc(AhoraUtc)).AddDays(-1);

        if (_ultimaJornadaStockIntentada != jornadaAnterior ||
            _ultimaJornadaStockCerrada == jornadaAnterior ||
            _ultimaJornadaPerdidaReportada == jornadaAnterior)
        {
            return;
        }

        var cerrada = await YaSeEjecutoStockCierreHoyAsync(jornadaAnterior, horaCierre, horaLimite, cancellationToken);
        if (cerrada is null)
        {
            return; // Se vuelve a comprobar en la siguiente evaluacion
        }

        if (cerrada.Value)
        {
            _ultimaJornadaStockCerrada = jornadaAnterior;
            return;
        }

        _ultimaJornadaPerdidaReportada = jornadaAnterior;
        _logger.LogError(
            "Cierre de Stock de la jornada {Jornada} perdido: no se pudo enviar entre las {HoraCierre} y la hora limite de recuperacion {HoraLimite}.",
            jornadaAnterior,
            horaCierre,
            horaLimite?.ToString() ?? "00:00");
    }

    /// <summary>
    /// Ejecuta el ciclo programado de Ventas y Servicio Tecnico garantizando exclusion mutua.
    /// Devuelve <see cref="YamahaResultadoSincronizacion.OmitidoPorConcurrencia"/> si el ciclo o alguno de sus
    /// servicios se omitio por otra ejecucion en curso; <see cref="YamahaResultadoSincronizacion.Fallido"/> si alguno fallo.
    /// </summary>
    public async Task<YamahaResultadoSincronizacion> EjecutarCicloProgramadoAsync(CancellationToken cancellationToken = default)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            _logger.LogInformation("YamahaSyncBackgroundService: Se omitio el ciclo programado de Ventas/STE porque ya existe una sincronizacion en curso. Se reintentara en la siguiente evaluacion.");
            return YamahaResultadoSincronizacion.OmitidoPorConcurrencia;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IYamahaSyncOrchestrator>();

            _logger.LogInformation("Iniciando ciclo programado de Ventas y Servicio Tecnico con Yamaha v3...");

            // Ventas
            var resVentas = await orchestrator.SincronizarServicioAsync(
                YamahaServicioSincronizacion.Ventas,
                esSimulacion: false,
                modoEnvio: "Programado",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Sincronizacion programada Ventas: Exitoso={Exitoso}, Validos={Validos}, Rechazados={Rechazados}",
                resVentas.Exitoso,
                resVentas.TotalValidos,
                resVentas.TotalRechazados);

            // Servicio Tecnico
            var resSte = await orchestrator.SincronizarServicioAsync(
                YamahaServicioSincronizacion.ServicioTecnico,
                esSimulacion: false,
                modoEnvio: "Programado",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Sincronizacion programada Servicio Tecnico: Exitoso={Exitoso}, Validos={Validos}, Rechazados={Rechazados}",
                resSte.Exitoso,
                resSte.TotalValidos,
                resSte.TotalRechazados);

            if (resVentas.Resultado == YamahaResultadoSincronizacion.OmitidoPorConcurrencia ||
                resSte.Resultado == YamahaResultadoSincronizacion.OmitidoPorConcurrencia)
            {
                return YamahaResultadoSincronizacion.OmitidoPorConcurrencia;
            }

            return resVentas.Exitoso && resSte.Exitoso
                ? YamahaResultadoSincronizacion.Exitoso
                : YamahaResultadoSincronizacion.Fallido;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Ciclo de sincronizacion cancelado.");
            return YamahaResultadoSincronizacion.Fallido;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el ciclo de sincronizacion de Ventas/STE en segundo plano.");
            return YamahaResultadoSincronizacion.Fallido;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Ejecuta el envio diario de Stock garantizando exclusion mutua.
    /// Devuelve el resultado del orquestador; <see cref="YamahaResultadoSincronizacion.OmitidoPorConcurrencia"/>
    /// si hay otra ejecucion en curso y <see cref="YamahaResultadoSincronizacion.Fallido"/> si fallo o se cancelo.
    /// </summary>
    public async Task<YamahaResultadoSincronizacion> EjecutarStockProgramadoAsync(CancellationToken cancellationToken = default)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            _logger.LogInformation("YamahaSyncBackgroundService: Se omitio el envio de Stock porque ya existe una sincronizacion en curso.");
            return YamahaResultadoSincronizacion.OmitidoPorConcurrencia;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IYamahaSyncOrchestrator>();

            _logger.LogInformation("Iniciando envio diario programado de Stock con Yamaha v3...");

            var resStock = await orchestrator.SincronizarServicioAsync(
                YamahaServicioSincronizacion.Stock,
                esSimulacion: false,
                modoEnvio: "Programado",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Sincronizacion programada Stock: Exitoso={Exitoso}, Validos={Validos}, Rechazados={Rechazados}",
                resStock.Exitoso,
                resStock.TotalValidos,
                resStock.TotalRechazados);

            return resStock.Resultado;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Envio de Stock cancelado.");
            return YamahaResultadoSincronizacion.Fallido;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el envio diario de Stock en segundo plano.");
            return YamahaResultadoSincronizacion.Fallido;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Comprueba si ya existe un registro exitoso no simulado de Stock para la jornada de Peru indicada:
    /// iniciado entre la hora de cierre de esa jornada y la hora limite de recuperacion del dia siguiente
    /// (o la medianoche, si la recuperacion esta desactivada).
    /// Devuelve null si no se pudo consultar, para que el llamador no envie a ciegas.
    /// </summary>
    public async Task<bool?> YaSeEjecutoStockCierreHoyAsync(
        DateOnly hoyPeru,
        TimeOnly horaCierre,
        TimeOnly? horaLimiteRecuperacion = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var inicioDiaUtc = HoraPeru.InicioDelDiaUtc(hoyPeru.ToDateTime(TimeOnly.MinValue));
            var desdeUtc = inicioDiaUtc.Add(horaCierre.ToTimeSpan());
            var hastaUtc = HoraPeru.InicioDelDiaUtc(hoyPeru.AddDays(1).ToDateTime(TimeOnly.MinValue))
                .Add((horaLimiteRecuperacion ?? TimeOnly.MinValue).ToTimeSpan());

            return await dbContext.YamahaSyncLogs
                .AsNoTracking()
                .AnyAsync(l => l.Servicio == YamahaServicioSincronizacion.Stock &&
                               l.Exitoso &&
                               !l.EsSimulacion &&
                               l.FechaInicio >= desdeUtc &&
                               l.FechaInicio < hastaUtc, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar si el cierre de Stock de la jornada {Jornada} ya se ejecuto en YamahaSyncLogs.", hoyPeru);
            return null;
        }
    }

    private async Task IntentarRecuperarAbandonadasAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IYamahaSyncOrchestrator>();
            await orchestrator.RecuperarEjecucionesAbandonadasAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Apagado normal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al recuperar ejecuciones abandonadas en YamahaSyncBackgroundService.");
        }
    }
}
