using System.Text.RegularExpressions;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMTeamBenavides.Api.Tests;

/// <summary>
/// Pruebas unitarias de la entidad YamahaSyncLog y su mapeo en EF Core.
/// Valida el ciclo de vida, trazabilidad, sanitización de secretos, consistencia lógica e índices.
/// </summary>
public class YamahaSyncLogTests
{
    [Fact]
    public void Iniciar_DebeInicializarEnEstadoEnProcesoSinFechaFinNiExito()
    {
        var usuarioId = Guid.NewGuid();
        var antes = DateTime.UtcNow;

        var log = YamahaSyncLog.Iniciar(
            YamahaServicioSincronizacion.Ventas,
            esSimulacion: true,
            usuarioId: usuarioId,
            modoEnvio: "DryRun");

        var despues = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(YamahaServicioSincronizacion.Ventas, log.Servicio);
        Assert.Equal(YamahaEstadoSincronizacion.EnProceso, log.Estado);
        Assert.True(log.FechaInicio >= antes && log.FechaInicio <= despues);
        Assert.Null(log.FechaFin);
        Assert.False(log.Exitoso);
        Assert.True(log.EsSimulacion);
        Assert.Equal(usuarioId, log.UsuarioId);
        Assert.Equal("DryRun", log.ModoEnvio);
        Assert.Equal(0, log.RegistrosProcesados);
        Assert.Equal(0, log.RegistrosValidos);
        Assert.Equal(0, log.RegistrosRechazados);
        Assert.Null(log.PayloadHash);
        Assert.Null(log.CodigoHttp);
        Assert.Null(log.RespuestaCuerpo);
        Assert.Null(log.MensajeError);

        // La entidad en estado inicial debe ser consistente
        log.ValidarConsistencia();
    }

    [Fact]
    public void Iniciar_ModoEnvioMuyLargo_DebeTruncarAMaxModoEnvioLongitud()
    {
        var modoLargo = new string('M', YamahaSyncLog.MaxModoEnvioLongitud + 20);

        var log = YamahaSyncLog.Iniciar(
            YamahaServicioSincronizacion.Stock,
            modoEnvio: modoLargo);

        Assert.NotNull(log.ModoEnvio);
        Assert.Equal(YamahaSyncLog.MaxModoEnvioLongitud, log.ModoEnvio.Length);
    }

    [Fact]
    public void RegistrarExito_DebeCompletarLogConValoresEsperados()
    {
        var log = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.ServicioTecnico);
        var antesFin = DateTime.UtcNow;

        log.RegistrarExito(
            procesados: 10,
            validos: 8,
            rechazados: 2,
            payloadHash: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            codigoHttp: 200,
            respuestaCuerpo: "{\"status\":\"OK\",\"count\":8}");

        var despuesFin = DateTime.UtcNow;

        Assert.Equal(YamahaEstadoSincronizacion.Completado, log.Estado);
        Assert.True(log.Exitoso);
        Assert.NotNull(log.FechaFin);
        Assert.True(log.FechaFin >= antesFin && log.FechaFin <= despuesFin);
        Assert.Equal(10, log.RegistrosProcesados);
        Assert.Equal(8, log.RegistrosValidos);
        Assert.Equal(2, log.RegistrosRechazados);
        Assert.Equal(200, log.CodigoHttp);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", log.PayloadHash);
        Assert.Equal("{\"status\":\"OK\",\"count\":8}", log.RespuestaCuerpo);
        Assert.Null(log.MensajeError);

        log.ValidarConsistencia();
    }

    [Fact]
    public void RegistrarFallo_DebeEstablecerEstadoFallidoConMensajeYFechaFin()
    {
        var log = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Ventas);
        var antesFin = DateTime.UtcNow;

        log.RegistrarFallo(
            mensajeError: "Error de conexión HTTP 500: Internal Server Error",
            codigoHttp: 500,
            respuestaCuerpo: "{\"error\":\"Timeout al persistir en base de datos central\"}",
            procesados: 15,
            validos: 15,
            rechazados: 0);

        var despuesFin = DateTime.UtcNow;

        Assert.Equal(YamahaEstadoSincronizacion.Fallido, log.Estado);
        Assert.False(log.Exitoso);
        Assert.NotNull(log.FechaFin);
        Assert.True(log.FechaFin >= antesFin && log.FechaFin <= despuesFin);
        Assert.Equal(500, log.CodigoHttp);
        Assert.Equal("Error de conexión HTTP 500: Internal Server Error", log.MensajeError);
        Assert.Equal("{\"error\":\"Timeout al persistir en base de datos central\"}", log.RespuestaCuerpo);
        Assert.Equal(15, log.RegistrosProcesados);

        log.ValidarConsistencia();
    }

    [Fact]
    public void RegistrarExito_ConRespuestaMuyLarga_DebeTruncarAMaxRespuestaCuerpoLongitud()
    {
        var log = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Stock);
        var respuestaGigante = new string('X', YamahaSyncLog.MaxRespuestaCuerpoLongitud + 500);

        log.RegistrarExito(
            procesados: 5,
            validos: 5,
            rechazados: 0,
            codigoHttp: 200,
            respuestaCuerpo: respuestaGigante);

        Assert.NotNull(log.RespuestaCuerpo);
        Assert.Equal(YamahaSyncLog.MaxRespuestaCuerpoLongitud, log.RespuestaCuerpo.Length);
    }

    [Fact]
    public void RegistrarFallo_ConMensajeMuyLargo_DebeTruncarAMaxMensajeErrorLongitud()
    {
        var log = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Ventas);
        var errorGigante = new string('E', YamahaSyncLog.MaxMensajeErrorLongitud + 300);

        log.RegistrarFallo(mensajeError: errorGigante);

        Assert.NotNull(log.MensajeError);
        Assert.Equal(YamahaSyncLog.MaxMensajeErrorLongitud, log.MensajeError.Length);
    }

    [Fact]
    public void RegistrarExitoYFallo_DebeSanitizarSecretosEnCuerpoYMensajeError()
    {
        var logExito = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Ventas);
        var respuestaConSecreto = "POST https://yamaha.azurewebsites.net/api/ReceiveRetailVentas?code=SECRET_API_KEY_SUPER_CONFIDENCIAL responded with 200";

        logExito.RegistrarExito(
            procesados: 1,
            validos: 1,
            rechazados: 0,
            respuestaCuerpo: respuestaConSecreto);

        Assert.DoesNotContain("SECRET_API_KEY_SUPER_CONFIDENCIAL", logExito.RespuestaCuerpo);
        Assert.Contains("***REDACTED***", logExito.RespuestaCuerpo);

        var logFallo = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.ServicioTecnico);
        var errorConSecreto = "Failed to call https://yamaha.azurewebsites.net/api/ReceiveRetailServicioTecnico?code=SECRET_FUNCTION_KEY_99999 : timeout";

        logFallo.RegistrarFallo(mensajeError: errorConSecreto);

        Assert.DoesNotContain("SECRET_FUNCTION_KEY_99999", logFallo.MensajeError);
        Assert.Contains("***REDACTED***", logFallo.MensajeError);
    }

    [Fact]
    public void ValidarConsistencia_EnProcesoConFechaFin_DebeLanzarExcepcion()
    {
        var log = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Ventas);
        log.FechaFin = DateTime.UtcNow; // Asignación indebida mientras sigue EnProceso

        Assert.Throws<InvalidOperationException>(() => log.ValidarConsistencia());
    }

    [Fact]
    public void ValidarConsistencia_EnProcesoConExitosoTrue_DebeLanzarExcepcion()
    {
        var log = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Ventas);
        log.Exitoso = true; // No puede reportar éxito mientras esté EnProceso

        Assert.Throws<InvalidOperationException>(() => log.ValidarConsistencia());
    }

    [Fact]
    public void ValidarConsistencia_CompletadoSinFechaFin_DebeLanzarExcepcion()
    {
        var log = new YamahaSyncLog
        {
            Estado = YamahaEstadoSincronizacion.Completado,
            Exitoso = true,
            FechaFin = null // Falta fecha fin
        };

        Assert.Throws<InvalidOperationException>(() => log.ValidarConsistencia());
    }

    [Fact]
    public void ValidarConsistencia_CompletadoConExitosoFalse_DebeLanzarExcepcion()
    {
        var log = new YamahaSyncLog
        {
            Estado = YamahaEstadoSincronizacion.Completado,
            Exitoso = false, // Inconsistente con Completado
            FechaFin = DateTime.UtcNow
        };

        Assert.Throws<InvalidOperationException>(() => log.ValidarConsistencia());
    }

    [Fact]
    public void ValidarConsistencia_FallidoSinMensajeError_DebeLanzarExcepcion()
    {
        var log = new YamahaSyncLog
        {
            Estado = YamahaEstadoSincronizacion.Fallido,
            Exitoso = false,
            FechaFin = DateTime.UtcNow,
            MensajeError = "" // Falta mensaje descriptivo
        };

        Assert.Throws<InvalidOperationException>(() => log.ValidarConsistencia());
    }

    [Fact]
    public void RegistrarSinCambios_DebeCerrarComoEjecucionCorrectaSinEnvio()
    {
        var log = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Stock);

        log.RegistrarSinCambios(procesados: 3, rechazados: 3);

        Assert.Equal(YamahaEstadoSincronizacion.SinCambios, log.Estado);
        Assert.True(log.Exitoso);
        Assert.NotNull(log.FechaFin);
        Assert.Equal(3, log.RegistrosProcesados);
        Assert.Equal(0, log.RegistrosValidos);
        Assert.Equal(3, log.RegistrosRechazados);
        Assert.Null(log.PayloadHash);
        Assert.Null(log.CodigoHttp);
        Assert.Null(log.MensajeError);
        Assert.Equal(YamahaSyncLog.MensajeSinCambios, log.RespuestaCuerpo);

        log.ValidarConsistencia();
    }

    [Fact]
    public void ValidarConsistencia_SinCambiosConExitosoFalse_DebeLanzarExcepcion()
    {
        var log = new YamahaSyncLog
        {
            Estado = YamahaEstadoSincronizacion.SinCambios,
            Exitoso = false,
            FechaFin = DateTime.UtcNow
        };

        Assert.Throws<InvalidOperationException>(() => log.ValidarConsistencia());
    }

    [Fact]
    public void ValidarConsistencia_ContadoresInconsistentes_DebeLanzarExcepcion()
    {
        var log = YamahaSyncLog.Iniciar(YamahaServicioSincronizacion.Ventas);
        log.RegistrosProcesados = 5;
        log.RegistrosValidos = 4;
        log.RegistrosRechazados = 3; // 4 + 3 = 7 > 5 procesados

        Assert.Throws<InvalidOperationException>(() => log.ValidarConsistencia());
    }

    // =========================================================================
    // Pruebas de Configuración del Modelo EF Core (ApplicationDbContext)
    // =========================================================================

    [Fact]
    public void DbContext_ConfiguracionYamahaSyncLog_DebeMapearTablaIndicesYPropiedadesCorrectamente()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=fake_model_test")
            .Options;

        using var context = new ApplicationDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(YamahaSyncLog));

        Assert.NotNull(entityType);
        Assert.Equal("YamahaSyncLogs", entityType.GetTableName());

        // Llave primaria
        var primaryKey = entityType.FindPrimaryKey();
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey.Properties);
        Assert.Equal("Id", primaryKey.Properties[0].Name);

        // Propiedad Servicio con conversión a string
        var propServicio = entityType.FindProperty(nameof(YamahaSyncLog.Servicio));
        Assert.NotNull(propServicio);
        Assert.False(propServicio.IsNullable);
        Assert.Equal(30, propServicio.GetMaxLength());

        // Propiedad Estado con conversión a string
        var propEstado = entityType.FindProperty(nameof(YamahaSyncLog.Estado));
        Assert.NotNull(propEstado);
        Assert.False(propEstado.IsNullable);
        Assert.Equal(30, propEstado.GetMaxLength());

        // Longitudes máximas configuradas
        Assert.Equal(YamahaSyncLog.MaxPayloadHashLongitud, entityType.FindProperty(nameof(YamahaSyncLog.PayloadHash))?.GetMaxLength());
        Assert.Equal(YamahaSyncLog.MaxRespuestaCuerpoLongitud, entityType.FindProperty(nameof(YamahaSyncLog.RespuestaCuerpo))?.GetMaxLength());
        Assert.Equal(YamahaSyncLog.MaxMensajeErrorLongitud, entityType.FindProperty(nameof(YamahaSyncLog.MensajeError))?.GetMaxLength());
        Assert.Equal(YamahaSyncLog.MaxModoEnvioLongitud, entityType.FindProperty(nameof(YamahaSyncLog.ModoEnvio))?.GetMaxLength());

        // Índices configurados
        var indices = entityType.GetIndexes().ToList();

        // 1. Índice compuesto (Servicio, FechaInicio)
        var indiceServicioFecha = indices.FirstOrDefault(i =>
            i.Properties.Count == 2 &&
            i.Properties[0].Name == nameof(YamahaSyncLog.Servicio) &&
            i.Properties[1].Name == nameof(YamahaSyncLog.FechaInicio));
        Assert.NotNull(indiceServicioFecha);
        Assert.False(indiceServicioFecha.IsUnique); // Ningún índice debe impedir reintentos

        // 2. Índice por Estado
        var indiceEstado = indices.FirstOrDefault(i =>
            i.Properties.Count == 1 && i.Properties[0].Name == nameof(YamahaSyncLog.Estado));
        Assert.NotNull(indiceEstado);
        Assert.False(indiceEstado.IsUnique);

        // 3. Índice por FechaInicio
        var indiceFechaInicio = indices.FirstOrDefault(i =>
            i.Properties.Count == 1 && i.Properties[0].Name == nameof(YamahaSyncLog.FechaInicio));
        Assert.NotNull(indiceFechaInicio);
        Assert.False(indiceFechaInicio.IsUnique);

        // 4. Índice por Exitoso
        var indiceExitoso = indices.FirstOrDefault(i =>
            i.Properties.Count == 1 && i.Properties[0].Name == nameof(YamahaSyncLog.Exitoso));
        Assert.NotNull(indiceExitoso);
        Assert.False(indiceExitoso.IsUnique);
    }
}
