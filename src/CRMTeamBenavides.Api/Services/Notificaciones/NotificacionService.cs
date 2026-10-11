using CRMTeamBenavides.Api.Features.Notificaciones;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRMTeamBenavides.Api.Services.Notificaciones;

public interface INotificacionService
{
    Task<ServiceResult<bool>> RegistrarDispositivoAsync(Guid usuarioId, RegistrarDispositivoRequest request, CancellationToken ct = default);
    Task DarDeBajaDispositivoAsync(Guid usuarioId, string token, CancellationToken ct = default);
    Task<List<NotificacionResponse>> ListarAsync(Guid usuarioId, bool soloNoLeidas, int limite, CancellationToken ct = default);
    Task<int> ContarNoLeidasAsync(Guid usuarioId, CancellationToken ct = default);
    Task<bool> MarcarLeidaAsync(Guid usuarioId, Guid id, CancellationToken ct = default);
    Task<int> MarcarTodasLeidasAsync(Guid usuarioId, CancellationToken ct = default);
}

/// <summary>La bandeja de cada usuario y los teléfonos donde recibe los avisos. Nadie ve los de otro.</summary>
public sealed class NotificacionService : INotificacionService
{
    private readonly ApplicationDbContext _context;

    public NotificacionService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Registra el teléfono al iniciar sesión o al renovarse el token. Un token es
    /// una instalación: si otra persona entra en ese teléfono, el token pasa a ella
    /// y la anterior deja de recibir ahí.
    /// </summary>
    public async Task<ServiceResult<bool>> RegistrarDispositivoAsync(
        Guid usuarioId, RegistrarDispositivoRequest request, CancellationToken ct = default)
    {
        var token = request.Token?.Trim() ?? string.Empty;
        if (token.Length is 0 or > 500)
            return ServiceResult<bool>.Invalid("El token del teléfono no es válido.");
        if (!Enum.IsDefined(request.Plataforma))
            return ServiceResult<bool>.Invalid("La plataforma del teléfono no es válida.");

        for (var intento = 0; intento < 2; intento++)
        {
            var dispositivo = await _context.DispositivosNotificacion.FirstOrDefaultAsync(d => d.Token == token, ct);
            if (dispositivo is null)
            {
                dispositivo = new DispositivoNotificacion { Token = token, FechaCreacion = DateTime.UtcNow };
                _context.DispositivosNotificacion.Add(dispositivo);
            }

            dispositivo.UsuarioId = usuarioId;
            dispositivo.Plataforma = request.Plataforma;
            dispositivo.Activo = true;
            dispositivo.UltimoRegistro = DateTime.UtcNow;
            dispositivo.FechaModificacion = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync(ct);
                return ServiceResult<bool>.Success(true);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // La app lo registró dos veces a la vez: la segunda vuelta actualiza el que quedó.
                _context.ChangeTracker.Clear();
            }
        }

        return ServiceResult<bool>.Conflict("No se pudo registrar el teléfono; vuelve a intentarlo.");
    }

    /// <summary>Al cerrar sesión el teléfono deja de recibir los avisos de ese usuario.</summary>
    public async Task DarDeBajaDispositivoAsync(Guid usuarioId, string token, CancellationToken ct = default)
    {
        var limpio = token?.Trim() ?? string.Empty;
        await _context.DispositivosNotificacion
            .Where(d => d.Token == limpio && d.UsuarioId == usuarioId && d.Activo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Activo, false)
                .SetProperty(d => d.FechaModificacion, DateTime.UtcNow), ct);
    }

    public async Task<List<NotificacionResponse>> ListarAsync(Guid usuarioId, bool soloNoLeidas, int limite, CancellationToken ct = default)
    {
        var query = _context.Notificaciones.AsNoTracking().Where(n => n.UsuarioId == usuarioId && n.Activo);
        if (soloNoLeidas) query = query.Where(n => n.FechaLeida == null);

        return await query
            .OrderByDescending(n => n.FechaCreacion)
            .Take(Math.Clamp(limite, 1, 100))
            .Select(n => new NotificacionResponse(
                n.Id, n.Tipo, n.Titulo, n.Mensaje, n.Entidad, n.EntidadId, n.Cantidad, n.FechaCreacion, n.FechaLeida))
            .ToListAsync(ct);
    }

    public Task<int> ContarNoLeidasAsync(Guid usuarioId, CancellationToken ct = default) =>
        _context.Notificaciones.CountAsync(n => n.UsuarioId == usuarioId && n.Activo && n.FechaLeida == null, ct);

    public async Task<bool> MarcarLeidaAsync(Guid usuarioId, Guid id, CancellationToken ct = default)
    {
        var notificacion = await _context.Notificaciones
            .FirstOrDefaultAsync(n => n.Id == id && n.UsuarioId == usuarioId && n.Activo, ct);
        if (notificacion is null) return false;

        if (notificacion.FechaLeida is null)
        {
            notificacion.FechaLeida = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return true;
    }

    public Task<int> MarcarTodasLeidasAsync(Guid usuarioId, CancellationToken ct = default) =>
        _context.Notificaciones
            .Where(n => n.UsuarioId == usuarioId && n.Activo && n.FechaLeida == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.FechaLeida, DateTime.UtcNow), ct);
}
