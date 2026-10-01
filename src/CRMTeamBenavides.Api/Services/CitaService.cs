using CRMTeamBenavides.Api.Features.Citas;
using CRMTeamBenavides.Api.Services.Exportacion;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRMTeamBenavides.Api.Services;

public class CitaService : ICitaService
{
    private readonly ApplicationDbContext _context;
    private readonly IExportacionExcelService _excelService;

    public CitaService(ApplicationDbContext context, IExportacionExcelService excelService)
    {
        _context = context;
        _excelService = excelService;
    }

    private async Task<string> GenerarNumeroCitaAsync()
    {
        var seqVal = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('\"CitaNumeroSeq\"') AS \"Value\"")
            .SingleAsync();
        return $"CIT-{seqVal:D6}";
    }

    private static DateTime NormalizarUtc(DateTime fecha)
    {
        return fecha.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(fecha, DateTimeKind.Utc),
            DateTimeKind.Local => fecha.ToUniversalTime(),
            _ => fecha
        };
    }

    public async Task<List<CitaListResponse>> GetAllAsync(
        Guid? soloClienteId = null,
        Guid? clienteId = null,
        Guid? vehiculoId = null,
        EstadoCita? estado = null,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        var query = _context.Citas
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Include(c => c.Vehiculo)
            .Include(c => c.OrdenServicio)
            .Where(c => c.Activo);

        if (soloClienteId.HasValue)
        {
            query = query.Where(c => c.ClienteId == soloClienteId.Value);
        }
        else if (clienteId.HasValue)
        {
            query = query.Where(c => c.ClienteId == clienteId.Value);
        }

        if (vehiculoId.HasValue)
        {
            query = query.Where(c => c.VehiculoId == vehiculoId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(c => c.Estado == estado.Value);
        }

        if (fechaInicio.HasValue)
        {
            var inicioUtc = NormalizarUtc(fechaInicio.Value);
            query = query.Where(c => c.FechaHoraProgramada >= inicioUtc);
        }

        if (fechaFin.HasValue)
        {
            var finUtc = NormalizarUtc(fechaFin.Value);
            query = query.Where(c => c.FechaHoraProgramada <= finUtc);
        }

        var citas = await query
            .OrderByDescending(c => c.FechaHoraProgramada)
            .ToListAsync(ct);

        return citas.Select(MapToListResponse).ToList();
    }

    public async Task<ServiceResult<CitaDetalleResponse>> GetByIdAsync(
        Guid id,
        Guid? soloClienteId = null,
        CancellationToken ct = default)
    {
        var cita = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Include(c => c.Vehiculo)
            .Include(c => c.OrdenServicio)
            .Include(c => c.HistorialEstados.OrderByDescending(h => h.Fecha))
                .ThenInclude(h => h.Usuario)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);

        if (cita is null)
        {
            return ServiceResult<CitaDetalleResponse>.NotFound("Cita no encontrada.");
        }

        if (soloClienteId.HasValue && cita.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<CitaDetalleResponse>.NotFound("Cita no encontrada.");
        }

        return ServiceResult<CitaDetalleResponse>.Success(MapToDetalleResponse(cita));
    }

    public async Task<ServiceResult<CitaDetalleResponse>> CrearAsync(
        CrearCitaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return ServiceResult<CitaDetalleResponse>.Invalid("El motivo de la cita es obligatorio.");
        }

        var clienteEfectivoId = soloClienteId ?? request.ClienteId;
        if (!clienteEfectivoId.HasValue)
        {
            return ServiceResult<CitaDetalleResponse>.Invalid("Debe especificar el cliente para la cita.");
        }

        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clienteEfectivoId.Value && c.Activo, ct);

        if (cliente is null)
        {
            return ServiceResult<CitaDetalleResponse>.Invalid("El cliente especificado no existe o está inactivo.");
        }

        var vehiculo = await _context.Vehiculos
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.VehiculoId && v.Activo, ct);

        if (vehiculo is null)
        {
            return ServiceResult<CitaDetalleResponse>.Invalid("La unidad especificada no existe o está inactiva.");
        }

        if (vehiculo.ClienteId != clienteEfectivoId.Value)
        {
            if (soloClienteId.HasValue)
            {
                return ServiceResult<CitaDetalleResponse>.Forbidden("La unidad seleccionada no pertenece al cliente autenticado.");
            }
            return ServiceResult<CitaDetalleResponse>.Invalid("La unidad seleccionada no pertenece al cliente especificado.");
        }

        var fechaInicio = NormalizarUtc(request.FechaHoraProgramada);
        var duracion = request.DuracionMinutos is > 0 ? request.DuracionMinutos.Value : 60;
        var fechaFin = fechaInicio.AddMinutes(duracion);

        // Validación de conflictos de horario por unidad
        var conflicto = await VerificarConflictosHorarioAsync(null, request.VehiculoId, fechaInicio, fechaFin, ct);
        if (conflicto is not null)
        {
            return ServiceResult<CitaDetalleResponse>.Conflict(conflicto);
        }

        var numeroCita = await GenerarNumeroCitaAsync();

        var cita = new Cita
        {
            NumeroCita = numeroCita,
            ClienteId = clienteEfectivoId.Value,
            VehiculoId = request.VehiculoId,
            FechaHoraProgramada = fechaInicio,
            DuracionMinutos = duracion,
            Motivo = request.Motivo.Trim(),
            Observaciones = request.Observaciones?.Trim(),
            Estado = EstadoCita.Pendiente,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        var historial = new HistorialEstadoCita
        {
            CitaId = cita.Id,
            EstadoAnterior = null,
            EstadoNuevo = EstadoCita.Pendiente,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Observacion = "Cita registrada inicialmente en agenda",
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        _context.HistorialEstadosCita.Add(historial);
        _context.Citas.Add(cita);
        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(cita.Id, soloClienteId, ct);
    }

    public async Task<ServiceResult<CitaDetalleResponse>> ActualizarAsync(
        Guid id,
        ActualizarCitaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default)
    {
        var cita = await _context.Citas
            .Include(c => c.Cliente)
            .Include(c => c.Vehiculo)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);

        if (cita is null)
        {
            return ServiceResult<CitaDetalleResponse>.NotFound("Cita no encontrada.");
        }

        if (soloClienteId.HasValue && cita.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<CitaDetalleResponse>.NotFound("Cita no encontrada.");
        }

        if (cita.Estado is EstadoCita.Cancelada or EstadoCita.Completada or EstadoCita.NoAsistio)
        {
            return ServiceResult<CitaDetalleResponse>.Invalid($"No se puede modificar una cita con estado {cita.Estado}.");
        }

        var vehiculo = await _context.Vehiculos
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.VehiculoId && v.Activo, ct);

        if (vehiculo is null || vehiculo.ClienteId != cita.ClienteId)
        {
            return ServiceResult<CitaDetalleResponse>.Invalid("La unidad especificada no pertenece al cliente de la cita.");
        }

        var fechaInicio = NormalizarUtc(request.FechaHoraProgramada);
        var duracion = request.DuracionMinutos is > 0 ? request.DuracionMinutos.Value : cita.DuracionMinutos;
        var fechaFin = fechaInicio.AddMinutes(duracion);

        var conflicto = await VerificarConflictosHorarioAsync(cita.Id, request.VehiculoId, fechaInicio, fechaFin, ct);
        if (conflicto is not null)
        {
            return ServiceResult<CitaDetalleResponse>.Conflict(conflicto);
        }

        cita.VehiculoId = request.VehiculoId;
        cita.FechaHoraProgramada = fechaInicio;
        cita.DuracionMinutos = duracion;
        cita.Motivo = request.Motivo.Trim();
        cita.Observaciones = request.Observaciones?.Trim();
        cita.ModificadoPorId = usuarioId;
        cita.FechaModificacion = DateTime.UtcNow;

        _context.HistorialEstadosCita.Add(new HistorialEstadoCita
        {
            CitaId = cita.Id,
            EstadoAnterior = cita.Estado,
            EstadoNuevo = cita.Estado,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Observacion = "Datos de la cita modificados",
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        });

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(cita.Id, soloClienteId, ct);
    }

    public async Task<ServiceResult<CitaDetalleResponse>> ReprogramarAsync(
        Guid id,
        ReprogramarCitaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default)
    {
        var cita = await _context.Citas
            .Include(c => c.Cliente)
            .Include(c => c.Vehiculo)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);

        if (cita is null)
        {
            return ServiceResult<CitaDetalleResponse>.NotFound("Cita no encontrada.");
        }

        if (soloClienteId.HasValue && cita.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<CitaDetalleResponse>.NotFound("Cita no encontrada.");
        }

        if (cita.Estado is EstadoCita.Cancelada or EstadoCita.Completada or EstadoCita.NoAsistio)
        {
            return ServiceResult<CitaDetalleResponse>.Invalid($"No se puede reprogramar una cita con estado {cita.Estado}.");
        }

        var fechaInicio = NormalizarUtc(request.NuevaFechaHoraProgramada);
        var duracion = request.NuevaDuracionMinutos is > 0 ? request.NuevaDuracionMinutos.Value : cita.DuracionMinutos;
        var fechaFin = fechaInicio.AddMinutes(duracion);

        var conflicto = await VerificarConflictosHorarioAsync(cita.Id, cita.VehiculoId, fechaInicio, fechaFin, ct);
        if (conflicto is not null)
        {
            return ServiceResult<CitaDetalleResponse>.Conflict(conflicto);
        }

        var fechaAnterior = cita.FechaHoraProgramada;
        cita.FechaHoraProgramada = fechaInicio;
        cita.DuracionMinutos = duracion;
        cita.ModificadoPorId = usuarioId;
        cita.FechaModificacion = DateTime.UtcNow;

        var detalleObs = string.IsNullOrWhiteSpace(request.MotivoReprogramacion)
            ? $"Cita reprogramada de {fechaAnterior:yyyy-MM-dd HH:mm} a {fechaInicio:yyyy-MM-dd HH:mm}"
            : $"Cita reprogramada de {fechaAnterior:yyyy-MM-dd HH:mm} a {fechaInicio:yyyy-MM-dd HH:mm}. Motivo: {request.MotivoReprogramacion.Trim()}";

        _context.HistorialEstadosCita.Add(new HistorialEstadoCita
        {
            CitaId = cita.Id,
            EstadoAnterior = cita.Estado,
            EstadoNuevo = cita.Estado,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Observacion = detalleObs,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        });

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(cita.Id, soloClienteId, ct);
    }

    public async Task<ServiceResult<CitaDetalleResponse>> CambiarEstadoAsync(
        Guid id,
        CambiarEstadoCitaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default)
    {
        var cita = await _context.Citas
            .Include(c => c.Cliente)
            .Include(c => c.Vehiculo)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);

        if (cita is null)
        {
            return ServiceResult<CitaDetalleResponse>.NotFound("Cita no encontrada.");
        }

        if (soloClienteId.HasValue && cita.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<CitaDetalleResponse>.NotFound("Cita no encontrada.");
        }

        if (cita.Estado == request.NuevoEstado)
        {
            return await GetByIdAsync(cita.Id, soloClienteId, ct);
        }

        // Validar transiciones permitidas
        if (cita.Estado is EstadoCita.Cancelada or EstadoCita.Completada or EstadoCita.NoAsistio)
        {
            return ServiceResult<CitaDetalleResponse>.Invalid($"No se puede cambiar el estado de una cita en estado terminal ({cita.Estado}).");
        }

        if (request.NuevoEstado == EstadoCita.Cancelada)
        {
            return await CancelarAsync(id, new CancelarCitaRequest(request.Observacion ?? "Cancelación de cita"), usuarioId, soloClienteId, ct);
        }

        if (cita.Estado == EstadoCita.EnTaller && request.NuevoEstado is EstadoCita.Pendiente or EstadoCita.Confirmada)
        {
            return ServiceResult<CitaDetalleResponse>.Invalid("No se puede retroceder una unidad que ya está en taller a pendiente o confirmada.");
        }

        if (cita.Estado == EstadoCita.Pendiente && request.NuevoEstado == EstadoCita.Completada)
        {
            return ServiceResult<CitaDetalleResponse>.Invalid("Una cita pendiente no puede completarse sin ingresar antes al taller.");
        }

        var estadoAnterior = cita.Estado;
        cita.Estado = request.NuevoEstado;
        cita.ModificadoPorId = usuarioId;
        cita.FechaModificacion = DateTime.UtcNow;

        _context.HistorialEstadosCita.Add(new HistorialEstadoCita
        {
            CitaId = cita.Id,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = request.NuevoEstado,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Observacion = request.Observacion?.Trim() ?? $"Cambio de estado a {request.NuevoEstado}",
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        });

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(cita.Id, soloClienteId, ct);
    }

    public async Task<ServiceResult<CitaDetalleResponse>> CancelarAsync(
        Guid id,
        CancelarCitaRequest request,
        Guid? usuarioId,
        Guid? soloClienteId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.MotivoCancelacion))
        {
            return ServiceResult<CitaDetalleResponse>.Invalid("El motivo de cancelación es obligatorio.");
        }

        var cita = await _context.Citas
            .Include(c => c.Cliente)
            .Include(c => c.Vehiculo)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);

        if (cita is null)
        {
            return ServiceResult<CitaDetalleResponse>.NotFound("Cita no encontrada.");
        }

        if (soloClienteId.HasValue && cita.ClienteId != soloClienteId.Value)
        {
            return ServiceResult<CitaDetalleResponse>.NotFound("Cita no encontrada.");
        }

        if (cita.Estado == EstadoCita.Cancelada)
        {
            return ServiceResult<CitaDetalleResponse>.Conflict("La cita ya se encuentra cancelada.");
        }

        if (cita.Estado == EstadoCita.Completada)
        {
            return ServiceResult<CitaDetalleResponse>.Conflict("No se puede cancelar una cita que ya fue completada.");
        }

        if (soloClienteId.HasValue && cita.Estado == EstadoCita.EnTaller)
        {
            return ServiceResult<CitaDetalleResponse>.Forbidden("No puede cancelar una cita que ya se encuentra en proceso dentro del taller.");
        }

        var estadoAnterior = cita.Estado;
        cita.Estado = EstadoCita.Cancelada;
        cita.MotivoCancelacion = request.MotivoCancelacion.Trim();
        cita.ModificadoPorId = usuarioId;
        cita.FechaModificacion = DateTime.UtcNow;

        _context.HistorialEstadosCita.Add(new HistorialEstadoCita
        {
            CitaId = cita.Id,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = EstadoCita.Cancelada,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Observacion = $"Cancelada: {request.MotivoCancelacion.Trim()}",
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        });

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(cita.Id, soloClienteId, ct);
    }

    public async Task<byte[]> ExportarExcelAsync(
        Guid? soloClienteId = null,
        Guid? clienteId = null,
        Guid? vehiculoId = null,
        EstadoCita? estado = null,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        var citas = await GetAllAsync(soloClienteId, clienteId, vehiculoId, estado, fechaInicio, fechaFin, ct);

        var dtos = citas.Select(c => new CitaExcelDto(
            NumeroCita: c.NumeroCita,
            FechaHoraProgramada: c.FechaHoraProgramada,
            ClienteNombre: c.ClienteNombre,
            ClienteDocumento: string.Empty,
            VehiculoInfo: c.VehiculoModelo,
            Placa: c.VehiculoPlaca ?? "SIN PLACA",
            Motivo: c.Motivo,
            Estado: c.EstadoDescripcion,
            DuracionMinutos: c.DuracionMinutos,
            Observaciones: null
        )).ToList();

        return _excelService.GenerarExcelCitas(dtos);
    }

    private async Task<string?> VerificarConflictosHorarioAsync(
        Guid? citaActualId,
        Guid vehiculoId,
        DateTime inicio,
        DateTime fin,
        CancellationToken ct)
    {
        // Citas activas que solapan con la ventana temporal solicitada para la misma unidad
        var citasActivasSolapadas = await _context.Citas
            .AsNoTracking()
            .Where(c => c.Activo
                && c.VehiculoId == vehiculoId
                && (!citaActualId.HasValue || c.Id != citaActualId.Value)
                && (c.Estado == EstadoCita.Pendiente || c.Estado == EstadoCita.Confirmada || c.Estado == EstadoCita.EnTaller))
            .ToListAsync(ct);

        var solapadas = citasActivasSolapadas
            .Where(c => c.FechaHoraProgramada < fin && c.FechaHoraProgramada.AddMinutes(c.DuracionMinutos) > inicio)
            .ToList();

        if (solapadas.Count > 0)
        {
            return "La unidad seleccionada ya cuenta con una cita activa programada en ese horario.";
        }

        return null;
    }

    private static CitaListResponse MapToListResponse(Cita c)
    {
        return new CitaListResponse(
            c.Id,
            c.NumeroCita ?? string.Empty,
            c.ClienteId,
            c.Cliente?.NombreCompleto ?? "Cliente no registrado",
            c.VehiculoId,
            c.Vehiculo?.Placa,
            $"{c.Vehiculo?.Marca} {c.Vehiculo?.Modelo}".Trim(),
            c.FechaHoraProgramada,
            c.DuracionMinutos,
            c.Motivo,
            c.Estado,
            c.Estado.ToString(),
            c.OrdenServicioId,
            c.OrdenServicio?.NumeroOrden);
    }

    private static CitaDetalleResponse MapToDetalleResponse(Cita c)
    {
        return new CitaDetalleResponse(
            c.Id,
            c.NumeroCita ?? string.Empty,
            c.ClienteId,
            c.Cliente?.NombreCompleto ?? "Cliente no registrado",
            c.Cliente?.NumeroDocumento ?? c.Cliente?.DocumentoIdentidad,
            c.Cliente?.Telefono,
            c.Cliente?.Email,
            c.VehiculoId,
            c.Vehiculo?.Marca ?? string.Empty,
            c.Vehiculo?.Modelo ?? string.Empty,
            c.Vehiculo?.Placa,
            c.Vehiculo?.Anio,
            c.FechaHoraProgramada,
            c.DuracionMinutos,
            c.FechaHoraProgramada.AddMinutes(c.DuracionMinutos),
            c.Motivo,
            c.Observaciones,
            c.Estado,
            c.Estado.ToString(),
            c.MotivoCancelacion,
            c.OrdenServicioId,
            c.OrdenServicio?.NumeroOrden,
            c.HistorialEstados.Select(h => new HistorialEstadoCitaResponse(
                h.Id,
                h.EstadoAnterior,
                h.EstadoAnterior?.ToString(),
                h.EstadoNuevo,
                h.EstadoNuevo.ToString(),
                h.UsuarioId,
                h.Usuario?.NombreCompleto ?? h.Usuario?.UserName,
                h.Fecha,
                h.Observacion
            )).ToList(),
            c.FechaCreacion);
    }
}
