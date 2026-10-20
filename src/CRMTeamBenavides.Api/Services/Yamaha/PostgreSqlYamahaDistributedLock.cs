using System.Data;
using System.Diagnostics;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CRMTeamBenavides.Api.Services.Yamaha;

/// <summary>
/// Claves estables de 64 bits para advisory locks de PostgreSQL en Yamaha v3.
/// Utiliza un prefijo de espacio de nombres "YAM\0" (0x59414D00_00000000) para evitar colisiones con otros bloqueos.
/// </summary>
public static class YamahaLockKeys
{
    public const long BaseNamespace = 0x59414D00_00000000L;

    public static long ObtenerClaveServicio(YamahaServicioSincronizacion servicio) => servicio switch
    {
        YamahaServicioSincronizacion.Ventas => BaseNamespace | 1L,
        YamahaServicioSincronizacion.Stock => BaseNamespace | 2L,
        YamahaServicioSincronizacion.ServicioTecnico => BaseNamespace | 3L,
        _ => BaseNamespace | 99L
    };
}

/// <summary>
/// Implementación de bloqueo distribuido mediante PostgreSQL session-level advisory locks (<c>pg_try_advisory_lock</c>).
///
/// Características de seguridad y resiliencia:
/// 1. Exclusión mutua entre procesos independientes que comparten la misma base de datos.
/// 2. Ámbito por servicio: Stock, Ventas y Servicio Técnico poseen claves independientes.
/// 3. Sin transacciones de larga duración: no mantiene transacciones abiertas durante llamadas HTTP externas.
/// 4. Liberación automática ante caídas: si el proceso host muere o la conexión se interrumpe,
///    PostgreSQL libera inmediatamente todos los advisory locks de la sesión.
/// 5. Compatibilidad InMemory: si la base no es PostgreSQL (ej. pruebas unitarias InMemory), actúa de forma no bloqueante.
/// </summary>
public sealed class PostgreSqlYamahaDistributedLock : IYamahaDistributedLock
{
    private readonly ApplicationDbContext _context;
    private readonly string? _connectionString;
    private readonly ILogger<PostgreSqlYamahaDistributedLock> _logger;

    public PostgreSqlYamahaDistributedLock(
        ApplicationDbContext context,
        ILogger<PostgreSqlYamahaDistributedLock> logger,
        string? connectionStringOverride = null)
    {
        _context = context;
        _logger = logger;
        _connectionString = connectionStringOverride;
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable?> IntentarAdquirirBloqueoAsync(
        YamahaServicioSincronizacion servicio,
        TimeSpan tiempoEspera,
        CancellationToken cancellationToken = default)
    {
        // En entornos sin PostgreSQL (ej. InMemoryDatabase en pruebas unitarias), no bloquear a nivel de BD
        if (!EsPostgreSql())
        {
            return new NoOpLockHandle();
        }

        var key = YamahaLockKeys.ObtenerClaveServicio(servicio);
        var connectionString = ObtenerCadenaConexion();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("No se pudo obtener la cadena de conexión de PostgreSQL para adquirir el advisory lock.");
            return null;
        }

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var adquirido = false;

        try
        {
            while (!adquirido && !cancellationToken.IsCancellationRequested)
            {
                await using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT pg_try_advisory_lock(@key);";
                    cmd.Parameters.AddWithValue("key", key);

                    var res = await cmd.ExecuteScalarAsync(cancellationToken);
                    adquirido = res is true || (res is bool b && b);
                }

                if (adquirido || tiempoEspera <= TimeSpan.Zero || stopwatch.Elapsed >= tiempoEspera)
                {
                    break;
                }

                var delayRestante = tiempoEspera - stopwatch.Elapsed;
                var delayActual = delayRestante > TimeSpan.FromMilliseconds(200)
                    ? TimeSpan.FromMilliseconds(200)
                    : delayRestante;

                if (delayActual > TimeSpan.Zero)
                {
                    await Task.Delay(delayActual, cancellationToken);
                }
            }

            if (!adquirido)
            {
                await connection.DisposeAsync();
                return null;
            }

            _logger.LogInformation(
                "Advisory lock adquirido en PostgreSQL para Yamaha [{Servicio}] (clave {Key}) tras {ElapsedMs} ms.",
                servicio,
                key,
                stopwatch.ElapsedMilliseconds);

            return new PostgreSqlLockHandle(connection, key, servicio, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al intentar adquirir el advisory lock para Yamaha [{Servicio}].", servicio);
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> EstaBloqueadoAsync(
        YamahaServicioSincronizacion servicio,
        CancellationToken cancellationToken = default)
    {
        if (!EsPostgreSql())
        {
            return false;
        }

        var key = YamahaLockKeys.ObtenerClaveServicio(servicio);
        var connectionString = ObtenerCadenaConexion();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT pg_try_advisory_lock(@key);";
        cmd.Parameters.AddWithValue("key", key);

        var res = await cmd.ExecuteScalarAsync(cancellationToken);
        var pudoAdquirir = res is true || (res is bool b && b);

        if (pudoAdquirir)
        {
            // Nadie lo tenía: liberar inmediatamente
            cmd.CommandText = "SELECT pg_advisory_unlock(@key);";
            await cmd.ExecuteNonQueryAsync(CancellationToken.None);
            return false;
        }

        // No se pudo adquirir => otra conexión/sesión lo tiene retenido
        return true;
    }

    private bool EsPostgreSql()
    {
        try
        {
            return _context.Database.IsNpgsql();
        }
        catch
        {
            return false;
        }
    }

    private string? ObtenerCadenaConexion()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            return _connectionString;
        }

        return _context.Database.GetConnectionString();
    }

    private sealed class PostgreSqlLockHandle : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly long _key;
        private readonly YamahaServicioSincronizacion _servicio;
        private readonly ILogger _logger;
        private int _disposed;

        public PostgreSqlLockHandle(
            NpgsqlConnection connection,
            long key,
            YamahaServicioSincronizacion servicio,
            ILogger logger)
        {
            _connection = connection;
            _key = key;
            _servicio = servicio;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                if (_connection.State == ConnectionState.Open)
                {
                    await using var cmd = _connection.CreateCommand();
                    cmd.CommandText = "SELECT pg_advisory_unlock(@key);";
                    cmd.Parameters.AddWithValue("key", _key);

                    await cmd.ExecuteNonQueryAsync(CancellationToken.None);
                    _logger.LogInformation("Advisory lock liberado en PostgreSQL para Yamaha [{Servicio}] (clave {Key}).", _servicio, _key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Excepción al ejecutar pg_advisory_unlock para {Key}. El cierre de la conexión liberará el lock.", _key);
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }

    private sealed class NoOpLockHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
