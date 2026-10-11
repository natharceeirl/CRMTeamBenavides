using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRMTeamBenavides.Api.Services.Notificaciones;

/// <summary>
/// Envía en segundo plano los avisos de la cola. Cada vuelta reserva un lote con
/// FOR UPDATE SKIP LOCKED (dos instancias de la API no mandan el mismo aviso),
/// lo marca por unos minutos y lo envía fuera de la transacción: la llamada a
/// Firebase no deja filas bloqueadas. Si la API se cae a mitad, la reserva vence
/// y el aviso vuelve a la cola.
/// </summary>
public sealed class EnvioNotificacionesService : BackgroundService
{
    private const int TamanoLote = 50;
    private static readonly TimeSpan Reserva = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _ambitos;
    private readonly IProveedorPush _push;
    private readonly IOptionsMonitor<NotificacionesOptions> _opciones;
    private readonly ILogger<EnvioNotificacionesService> _logger;

    public EnvioNotificacionesService(
        IServiceScopeFactory ambitos,
        IProveedorPush push,
        IOptionsMonitor<NotificacionesOptions> opciones,
        ILogger<EnvioNotificacionesService> logger)
    {
        _ambitos = ambitos;
        _push = push;
        _opciones = opciones;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Vacía la cola antes de esperar: si llegó un lote lleno, puede haber más.
                while (await ProcesarLoteAsync(stoppingToken) == TamanoLote) { }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Notificaciones: falló una vuelta del envío; se reintenta en la siguiente.");
            }

            var intervalo = Math.Max(2, _opciones.CurrentValue.IntervaloSegundos);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalo), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Envía un lote y devuelve cuántos avisos tomó.</summary>
    public async Task<int> ProcesarLoteAsync(CancellationToken ct)
    {
        var opciones = _opciones.CurrentValue;
        using var ambito = _ambitos.CreateScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lote = await ReservarLoteAsync(contexto, opciones, ct);
        if (lote.Count == 0) return 0;

        var usuarios = lote.Select(n => n.UsuarioId).Distinct().ToList();
        var dispositivos = await contexto.DispositivosNotificacion
            .Where(d => usuarios.Contains(d.UsuarioId) && d.Activo)
            .ToListAsync(ct);

        foreach (var notificacion in lote)
        {
            await EnviarAsync(notificacion, dispositivos.Where(d => d.UsuarioId == notificacion.UsuarioId && d.Activo).ToList(), opciones, ct);
            notificacion.FechaModificacion = DateTime.UtcNow;
        }

        await contexto.SaveChangesAsync(ct);
        return lote.Count;
    }

    private static async Task<List<Notificacion>> ReservarLoteAsync(
        ApplicationDbContext contexto, NotificacionesOptions opciones, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        // Lo agrupable espera a que dejen de llegar avisos del mismo asunto, sin pasar la espera máxima.
        var sinCambiosDesde = ahora.AddSeconds(-opciones.EsperaAgrupacionSegundos);
        var creadoAntesDe = ahora.AddMinutes(-opciones.EsperaMaximaMinutos);

        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
        var lote = await contexto.Notificaciones
            .FromSqlInterpolated($"""
                SELECT * FROM "Notificaciones"
                WHERE "Activo" AND "EstadoEnvio" = {(int)EstadoEnvioNotificacion.Pendiente}
                  AND ("ProximoIntento" IS NULL OR "ProximoIntento" <= {ahora})
                  AND ("ClaveAgrupacion" IS NULL
                       OR COALESCE("FechaModificacion", "FechaCreacion") <= {sinCambiosDesde}
                       OR "FechaCreacion" <= {creadoAntesDe})
                ORDER BY "FechaCreacion"
                LIMIT {TamanoLote}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        foreach (var notificacion in lote)
        {
            notificacion.ProximoIntento = ahora.Add(Reserva);
        }

        await contexto.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return lote;
    }

    private async Task EnviarAsync(
        Notificacion notificacion, List<DispositivoNotificacion> dispositivos, NotificacionesOptions opciones, CancellationToken ct)
    {
        if (dispositivos.Count == 0)
        {
            // Sin teléfonos registrados el aviso queda solo en la bandeja.
            notificacion.EstadoEnvio = EstadoEnvioNotificacion.SinDispositivo;
            notificacion.ProximoIntento = null;
            return;
        }

        var datos = new Dictionary<string, string>
        {
            ["notificacionId"] = notificacion.Id.ToString(),
            ["tipo"] = notificacion.Tipo.ToString(),
            ["entidad"] = notificacion.Entidad ?? string.Empty,
            ["entidadId"] = notificacion.EntidadId?.ToString() ?? string.Empty
        };

        var enviado = false;
        string? errorTemporal = null;
        foreach (var dispositivo in dispositivos)
        {
            var respuesta = await _push.EnviarAsync(
                new MensajePush(dispositivo.Token, notificacion.Titulo, notificacion.Mensaje, datos), ct);

            switch (respuesta.Resultado)
            {
                case ResultadoPush.Enviado:
                    enviado = true;
                    break;
                case ResultadoPush.TokenInvalido:
                    dispositivo.Activo = false;
                    dispositivo.FechaModificacion = DateTime.UtcNow;
                    break;
                default:
                    errorTemporal = respuesta.Error ?? "Error del proveedor de notificaciones.";
                    break;
            }
        }

        if (enviado)
        {
            notificacion.EstadoEnvio = EstadoEnvioNotificacion.Enviada;
            notificacion.FechaEnvio = DateTime.UtcNow;
            notificacion.ProximoIntento = null;
            notificacion.UltimoError = null;
            return;
        }

        if (errorTemporal is null)
        {
            // Todos sus teléfonos resultaron inválidos.
            notificacion.EstadoEnvio = EstadoEnvioNotificacion.SinDispositivo;
            notificacion.ProximoIntento = null;
            return;
        }

        notificacion.Intentos += 1;
        notificacion.UltimoError = errorTemporal.Length > 500 ? errorTemporal[..500] : errorTemporal;
        if (notificacion.Intentos >= opciones.MaximoIntentos)
        {
            notificacion.EstadoEnvio = EstadoEnvioNotificacion.Fallida;
            notificacion.ProximoIntento = null;
            _logger.LogWarning("Notificaciones: el aviso {Id} se dio por fallido tras {Intentos} intentos: {Error}",
                notificacion.Id, notificacion.Intentos, notificacion.UltimoError);
        }
        else
        {
            // 30 s, 1 min, 2 min, 4 min…
            notificacion.ProximoIntento = DateTime.UtcNow.AddSeconds(30 * Math.Pow(2, notificacion.Intentos - 1));
        }
    }
}
